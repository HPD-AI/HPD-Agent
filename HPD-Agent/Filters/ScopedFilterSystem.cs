using Microsoft.Extensions.AI;
using HPD.Agent.Internal.Filters;

namespace HPD.Agent.Internal.Filters;

/// <summary>
/// Represents the scope of a filter - what functions it applies to.
/// Internal enum for HPD-Agent internals.
/// </summary>
internal enum FilterScope
{
    /// <summary>Filter applies to all functions globally</summary>
    Global,
    /// <summary>Filter applies to all functions from a specific plugin</summary>
    Plugin,
    /// <summary>Filter applies to skill container and functions called by a specific skill</summary>
    Skill,
    /// <summary>Filter applies to a specific function only</summary>
    Function
}

/// <summary>
/// Associates a filter with its scope and target.
/// Internal class for HPD-Agent internals.
/// </summary>
internal class ScopedFilter
{
    public IAiFunctionFilter Filter { get; }
    public FilterScope Scope { get; }
    public string? Target { get; } // Plugin type name or function name, null for global
    
    public ScopedFilter(IAiFunctionFilter filter, FilterScope scope, string? target = null)
    {
        Filter = filter ?? throw new ArgumentNullException(nameof(filter));
        Scope = scope;
        Target = target;
    }
    
    /// <summary>
    /// Determines if this filter should be applied to the given function
    /// </summary>
    /// <param name="functionName">The name of the function being invoked</param>
    /// <param name="pluginTypeName">The plugin that contains this function (optional)</param>
    /// <param name="skillName">The skill context (if function is called by a skill)</param>
    /// <param name="isSkillContainer">Whether this function is a skill container itself</param>
    public bool AppliesTo(string functionName, string? pluginTypeName = null, string? skillName = null, bool isSkillContainer = false)
    {
        return Scope switch
        {
            FilterScope.Global => true,
            FilterScope.Plugin => !string.IsNullOrEmpty(pluginTypeName) &&
                                 string.Equals(Target, pluginTypeName, StringComparison.Ordinal),
            FilterScope.Skill =>
                // Apply if this function IS the skill container itself
                (isSkillContainer && string.Equals(Target, functionName, StringComparison.Ordinal)) ||
                // OR if this function is called FROM this skill (via mapping)
                (!string.IsNullOrEmpty(skillName) && string.Equals(Target, skillName, StringComparison.Ordinal)),
            FilterScope.Function => string.Equals(Target, functionName, StringComparison.Ordinal),
            _ => false
        };
    }
}

/// <summary>
/// Manages the collection of scoped filters and provides methods to retrieve applicable filters.
/// Internal class for HPD-Agent internals.
/// </summary>
internal class ScopedFilterManager
{
    private readonly List<ScopedFilter> _scopedFilters = new();
    private readonly Dictionary<string, string> _functionToPluginMap = new();
    private readonly Dictionary<string, string> _functionToSkillMap = new();
    
    /// <summary>
    /// Adds a filter with the specified scope
    /// </summary>
    public void AddFilter(IAiFunctionFilter filter, FilterScope scope, string? target = null)
    {
        _scopedFilters.Add(new ScopedFilter(filter, scope, target));
    }
    
    /// <summary>
    /// Registers that a function belongs to a specific plugin
    /// </summary>
    public void RegisterFunctionPlugin(string functionName, string pluginTypeName)
    {
        _functionToPluginMap[functionName] = pluginTypeName;
    }

    /// <summary>
    /// Registers a mapping from a function to the skill that references it.
    /// Used for fallback skill lookup when skill context is not explicitly provided.
    /// </summary>
    /// <param name="functionName">The function name (e.g., "ReadFile")</param>
    /// <param name="skillName">The skill that references this function (e.g., "analyze_codebase")</param>
    public void RegisterFunctionSkill(string functionName, string skillName)
    {
        _functionToSkillMap[functionName] = skillName;
    }
    
    /// <summary>
    /// Gets all filters that apply to the specified function, ordered by scope priority:
    /// 1. Global filters (0)
    /// 2. Plugin-specific filters (1)
    /// 3. Skill-specific filters (2)
    /// 4. Function-specific filters (3)
    /// </summary>
    /// <param name="functionName">The name of the function being invoked</param>
    /// <param name="pluginTypeName">The plugin that contains this function (optional)</param>
    /// <param name="skillName">The skill context (if function is called by a skill)</param>
    /// <param name="isSkillContainer">Whether this function is a skill container itself</param>
    public IEnumerable<IAiFunctionFilter> GetApplicableFilters(
        string functionName,
        string? pluginTypeName = null,
        string? skillName = null,
        bool isSkillContainer = false)
    {
        // Fallback lookup for plugin (existing)
        if (string.IsNullOrEmpty(pluginTypeName))
        {
            _functionToPluginMap.TryGetValue(functionName, out pluginTypeName);
        }

        // Fallback lookup for skill
        if (string.IsNullOrEmpty(skillName))
        {
            _functionToSkillMap.TryGetValue(functionName, out skillName);
        }

        var applicableFilters = _scopedFilters
            .Where(sf => sf.AppliesTo(functionName, pluginTypeName, skillName, isSkillContainer))
            .OrderBy(sf => sf.Scope) // Global(0) → Plugin(1) → Skill(2) → Function(3)
            .Select(sf => sf.Filter);

        return applicableFilters;
    }
    
    /// <summary>
    /// Gets all scoped filters
    /// </summary>
    public IReadOnlyList<ScopedFilter> GetAllScopedFilters() => _scopedFilters.AsReadOnly();
    
    /// <summary>
    /// Gets all global filters that apply to all functions
    /// </summary>
    public List<IAiFunctionFilter> GetGlobalFilters()
    {
        return _scopedFilters
            .Where(sf => sf.Scope == FilterScope.Global)
            .Select(sf => sf.Filter)
            .ToList();
    }
}

/// <summary>
/// Tracks the current context for scoped filter registration in the builder
/// </summary>
internal class BuilderScopeContext
{
    public FilterScope CurrentScope { get; set; } = FilterScope.Global;
    public string? CurrentTarget { get; set; }

    public void SetGlobalScope()
    {
        CurrentScope = FilterScope.Global;
        CurrentTarget = null;
    }

    public void SetPluginScope(string pluginTypeName)
    {
        CurrentScope = FilterScope.Plugin;
        CurrentTarget = pluginTypeName;
    }

    public void SetSkillScope(string skillName)
    {
        CurrentScope = FilterScope.Skill;
        CurrentTarget = skillName;
    }

    public void SetFunctionScope(string functionName)
    {
        CurrentScope = FilterScope.Function;
        CurrentTarget = functionName;
    }
}
