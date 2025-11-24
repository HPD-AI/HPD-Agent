using Microsoft.Extensions.AI;
using HPD.Agent.Internal.MiddleWare;

namespace HPD.Agent.Internal.MiddleWare;

/// <summary>
/// Represents the scope of a Middleware - what functions it applies to.
/// Internal enum for HPD-Agent internals.
/// </summary>
internal enum MiddlewareScope
{
    /// <summary>Middleware applies to all functions globally</summary>
    Global,
    /// <summary>Middleware applies to all functions from a specific plugin</summary>
    Plugin,
    /// <summary>Middleware applies to skill container and functions called by a specific skill</summary>
    Skill,
    /// <summary>Middleware applies to a specific function only</summary>
    Function
}

/// <summary>
/// Associates a Middleware with its scope and target.
/// Internal class for HPD-Agent internals.
/// </summary>
internal class ScopedMiddleware
{
    public IAIFunctionMiddleware Middleware { get; }
    public MiddlewareScope Scope { get; }
    public string? Target { get; } // Plugin type name or function name, null for global
    
    public ScopedMiddleware(IAIFunctionMiddleware Middleware, MiddlewareScope scope, string? target = null)
    {
        this.Middleware = Middleware ?? throw new ArgumentNullException(nameof(Middleware));
        Scope = scope;
        Target = target;
    }
    
    /// <summary>
    /// Determines if this Middleware should be applied to the given function
    /// </summary>
    /// <param name="functionName">The name of the function being invoked</param>
    /// <param name="pluginTypeName">The plugin that contains this function (optional)</param>
    /// <param name="skillName">The skill context (if function is called by a skill)</param>
    /// <param name="isSkillContainer">Whether this function is a skill container itself</param>
    public bool AppliesTo(string functionName, string? pluginTypeName = null, string? skillName = null, bool isSkillContainer = false)
    {
        return Scope switch
        {
            MiddlewareScope.Global => true,
            MiddlewareScope.Plugin => !string.IsNullOrEmpty(pluginTypeName) &&
                                 string.Equals(Target, pluginTypeName, StringComparison.Ordinal),
            MiddlewareScope.Skill =>
                // Apply if this function IS the skill container itself
                (isSkillContainer && string.Equals(Target, functionName, StringComparison.Ordinal)) ||
                // OR if this function is called FROM this skill (via mapping)
                (!string.IsNullOrEmpty(skillName) && string.Equals(Target, skillName, StringComparison.Ordinal)),
            MiddlewareScope.Function => string.Equals(Target, functionName, StringComparison.Ordinal),
            _ => false
        };
    }
}

/// <summary>
/// Manages the collection of scoped Middlewares and provides methods to retrieve applicable Middlewares.
/// Internal class for HPD-Agent internals.
/// </summary>
internal class ScopedFunctionMiddlewareManager
{
    private readonly List<ScopedMiddleware> _scopedMiddlewares = new();
    private readonly Dictionary<string, string> _functionToPluginMap = new();
    private readonly Dictionary<string, string> _functionToSkillMap = new();
    
    /// <summary>
    /// Adds a Middleware with the specified scope
    /// </summary>
    public void AddMiddleware(IAIFunctionMiddleware Middleware, MiddlewareScope scope, string? target = null)
    {
        if (Middleware == null)
        {
            return;
        }
        _scopedMiddlewares.Add(new ScopedMiddleware(Middleware, scope, target));
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
    /// Gets all Middlewares that apply to the specified function, ordered by scope priority:
    /// 1. Global Middlewares (0)
    /// 2. Plugin-specific Middlewares (1)
    /// 3. Skill-specific Middlewares (2)
    /// 4. Function-specific Middlewares (3)
    /// </summary>
    /// <param name="functionName">The name of the function being invoked</param>
    /// <param name="pluginTypeName">The plugin that contains this function (optional)</param>
    /// <param name="skillName">The skill context (if function is called by a skill)</param>
    /// <param name="isSkillContainer">Whether this function is a skill container itself</param>
    public IEnumerable<IAIFunctionMiddleware> GetApplicableMiddlewares(
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

        var applicableMiddlewares = _scopedMiddlewares
            .Where(sf => sf.AppliesTo(functionName, pluginTypeName, skillName, isSkillContainer))
            .OrderBy(sf => sf.Scope) // Global(0) → Plugin(1) → Skill(2) → Function(3)
            .Select(sf => sf.Middleware);

        return applicableMiddlewares;
    }
    
    /// <summary>
    /// Gets all scoped Middlewares
    /// </summary>
    public IReadOnlyList<ScopedMiddleware> GetAllScopedMiddlewares() => _scopedMiddlewares.AsReadOnly();
    
    /// <summary>
    /// Gets all global Middlewares that apply to all functions
    /// </summary>
    public List<IAIFunctionMiddleware> GetGlobalMiddlewares()
    {
        var globalMiddlewares = _scopedMiddlewares
            .Where(sf => sf.Scope == MiddlewareScope.Global)
            .Select(sf => sf.Middleware)
            .ToList();

        return globalMiddlewares;
    }
}

/// <summary>
/// Tracks the current context for scoped Middleware registration in the builder
/// </summary>
internal class BuilderScopeContext
{
    public MiddlewareScope CurrentScope { get; set; } = MiddlewareScope.Global;
    public string? CurrentTarget { get; set; }

    public void SetGlobalScope()
    {
        CurrentScope = MiddlewareScope.Global;
        CurrentTarget = null;
    }

    public void SetPluginScope(string pluginTypeName)
    {
        CurrentScope = MiddlewareScope.Plugin;
        CurrentTarget = pluginTypeName;
    }

    public void SetSkillScope(string skillName)
    {
        CurrentScope = MiddlewareScope.Skill;
        CurrentTarget = skillName;
    }

    public void SetFunctionScope(string functionName)
    {
        CurrentScope = MiddlewareScope.Function;
        CurrentTarget = functionName;
    }
}
