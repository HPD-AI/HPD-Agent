namespace HPD_Agent.Skills;

/// <summary>
/// Configuration options for skills
/// </summary>
public class SkillOptions
{
    /// <summary>
    /// Scoping mode for skill function visibility
    /// - InstructionOnly: Functions always visible, skill provides instructions (default)
    /// - Scoped: Functions hidden until skill activated
    /// </summary>
    public SkillScopingMode ScopingMode { get; set; } = SkillScopingMode.InstructionOnly;

    /// <summary>
    /// If true, skill auto-expands at conversation start
    /// </summary>
    public bool AutoExpand { get; set; } = false;

    /// <summary>
    /// Optional paths to instruction document files
    /// Loaded at build time and merged with inline instructions
    /// </summary>
    public string[]? InstructionDocuments { get; set; }

    /// <summary>
    /// Base directory for instruction documents
    /// Default: "skills/documents/"
    /// </summary>
    public string InstructionDocumentBaseDirectory { get; set; } = "skills/documents/";
}
