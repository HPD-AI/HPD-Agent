namespace HPD_Agent.Skills;

/// <summary>
/// Represents a skill - a semantic grouping of functions with instructions.
/// Skills are created via SkillFactory.Create() and processed by source generator.
/// </summary>
public class Skill
{
    /// <summary>
    /// Skill name (used as AIFunction name)
    /// </summary>
    public string Name { get; internal set; } = string.Empty;

    /// <summary>
    /// Description shown in tool list before activation
    /// </summary>
    public string Description { get; internal set; } = string.Empty;

    /// <summary>
    /// Instructions shown after skill activation
    /// </summary>
    public string? Instructions { get; internal set; }

    /// <summary>
    /// References to functions or skills (delegates)
    /// Can be: Func/Action delegates (functions) OR Func&lt;SkillOptions?, Skill&gt; (skills)
    /// </summary>
    public Delegate[] References { get; internal set; } = Array.Empty<Delegate>();

    /// <summary>
    /// Skill configuration options
    /// </summary>
    public SkillOptions Options { get; internal set; } = new();

    // Internal - resolved by source generator during code generation

    /// <summary>
    /// Resolved function references in "PluginName.FunctionName" format
    /// Set by source generator after flattening skill references
    /// </summary>
    internal string[] ResolvedFunctionReferences { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Resolved plugin types that need to be registered
    /// Set by source generator after analyzing all references
    /// </summary>
    internal string[] ResolvedPluginTypes { get; set; } = Array.Empty<string>();
}
