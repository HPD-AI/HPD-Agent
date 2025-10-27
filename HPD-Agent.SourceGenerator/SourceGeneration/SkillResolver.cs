using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Resolves skill references recursively, handling nested skills and circular dependencies
/// </summary>
internal class SkillResolver
{
    private readonly Dictionary<string, SkillInfo> _allSkills = new();
    private readonly HashSet<string> _visitedSkills = new();
    private readonly Stack<string> _resolutionStack = new();
    private readonly Dictionary<string, ResolvedSkillInfo> _resolvedSkills = new();
    private const int MaxDepth = 50; // Prevent infinite loops

    /// <summary>
    /// Initializes the resolver with all discovered skills
    /// </summary>
    public SkillResolver(IEnumerable<SkillInfo> skills)
    {
        foreach (var skill in skills)
        {
            _allSkills[skill.FullName] = skill;
        }
    }

    /// <summary>
    /// Resolves all skills, flattening nested references
    /// </summary>
    public void ResolveAllSkills()
    {
        foreach (var skill in _allSkills.Values)
        {
            ResolveSkill(skill);
        }
    }

    /// <summary>
    /// Resolves a skill recursively, following all skill references
    /// </summary>
    public ResolvedSkillInfo ResolveSkill(SkillInfo skill)
    {
        // Check if already resolved
        if (_visitedSkills.Contains(skill.FullName))
        {
            return _resolvedSkills[skill.FullName];
        }

        // Check for circular reference
        if (_resolutionStack.Contains(skill.FullName))
        {
            // Circular reference detected - this is OK!
            // Return empty to prevent infinite loop
            // The circular reference will be resolved on the other branch
            return new ResolvedSkillInfo
            {
                FunctionReferences = new List<string>(),
                PluginTypes = new List<string>()
            };
        }

        // Check depth limit
        if (_resolutionStack.Count >= MaxDepth)
        {
            // Exceeded max depth - likely a complex circular reference
            return new ResolvedSkillInfo
            {
                FunctionReferences = new List<string>(),
                PluginTypes = new List<string>()
            };
        }

        _resolutionStack.Push(skill.FullName);
        _visitedSkills.Add(skill.FullName);

        var functionRefs = new List<string>();
        var pluginTypes = new HashSet<string>();

        foreach (var reference in skill.References)
        {
            if (reference.ReferenceType == ReferenceType.Skill)
            {
                // Recursive: resolve nested skill
                if (_allSkills.TryGetValue(reference.FullName, out var nestedSkill))
                {
                    var resolved = ResolveSkill(nestedSkill);
                    functionRefs.AddRange(resolved.FunctionReferences);
                    foreach (var pluginType in resolved.PluginTypes)
                    {
                        pluginTypes.Add(pluginType);
                    }
                }
                else
                {
                    // Skill reference not found (could be in another assembly)
                    // Treat as a warning, not an error
                    // The skill might be defined in a referenced project
                }
            }
            else
            {
                // Direct function reference
                functionRefs.Add(reference.FullName);
                pluginTypes.Add(reference.PluginType);
            }
        }

        _resolutionStack.Pop();

        // Deduplicate function references
        var result = new ResolvedSkillInfo
        {
            FunctionReferences = functionRefs.Distinct().ToList(),
            PluginTypes = pluginTypes.ToList()
        };

        _resolvedSkills[skill.FullName] = result;

        // Update the skill with resolved references
        skill.ResolvedFunctionReferences = result.FunctionReferences;
        skill.ResolvedPluginTypes = result.PluginTypes;

        return result;
    }

    /// <summary>
    /// Gets all resolved skills
    /// </summary>
    public Dictionary<string, ResolvedSkillInfo> GetResolvedSkills()
    {
        return _resolvedSkills;
    }

    /// <summary>
    /// Detects circular skill references
    /// Returns list of circular reference chains
    /// </summary>
    public List<List<string>> DetectCircularReferences()
    {
        var circularChains = new List<List<string>>();
        var visited = new HashSet<string>();
        var stack = new Stack<string>();

        foreach (var skill in _allSkills.Values)
        {
            if (!visited.Contains(skill.FullName))
            {
                DetectCircularReferencesRecursive(skill, visited, stack, circularChains);
            }
        }

        return circularChains;
    }

    private void DetectCircularReferencesRecursive(
        SkillInfo skill,
        HashSet<string> visited,
        Stack<string> stack,
        List<List<string>> circularChains)
    {
        visited.Add(skill.FullName);
        stack.Push(skill.FullName);

        foreach (var reference in skill.References)
        {
            if (reference.ReferenceType == ReferenceType.Skill)
            {
                if (stack.Contains(reference.FullName))
                {
                    // Found circular reference
                    var chain = new List<string>();
                    var stackArray = stack.ToArray();
                    bool foundStart = false;

                    for (int i = stackArray.Length - 1; i >= 0; i--)
                    {
                        if (stackArray[i] == reference.FullName)
                            foundStart = true;

                        if (foundStart)
                            chain.Add(stackArray[i]);
                    }

                    chain.Add(reference.FullName); // Close the circle
                    circularChains.Add(chain);
                }
                else if (!visited.Contains(reference.FullName))
                {
                    if (_allSkills.TryGetValue(reference.FullName, out var nestedSkill))
                    {
                        DetectCircularReferencesRecursive(nestedSkill, visited, stack, circularChains);
                    }
                }
            }
        }

        stack.Pop();
    }

    /// <summary>
    /// Gets all plugins referenced by all skills
    /// </summary>
    public HashSet<string> GetAllReferencedPlugins()
    {
        var plugins = new HashSet<string>();

        foreach (var skill in _allSkills.Values)
        {
            foreach (var pluginType in skill.ResolvedPluginTypes)
            {
                plugins.Add(pluginType);
            }
        }

        return plugins;
    }
}
