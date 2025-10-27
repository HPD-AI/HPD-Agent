using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Information about a skill discovered during source generation.
/// </summary>
internal class SkillInfo
{
    /// <summary>
    /// Method name (e.g., "FileDebugging")
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// Skill name from SkillFactory.Create() call (ideally matches MethodName)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description shown before activation
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Instructions shown after activation
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Skill options extracted from SkillFactory.Create() call
    /// </summary>
    public SkillOptionsInfo Options { get; set; } = new();

    /// <summary>
    /// References to functions or other skills
    /// </summary>
    public List<ReferenceInfo> References { get; set; } = new();

    /// <summary>
    /// Class containing this skill method
    /// </summary>
    public ClassDeclarationSyntax ContainingClass { get; set; } = null!;

    /// <summary>
    /// Namespace of the containing class
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Full name: "ClassName.MethodName"
    /// </summary>
    public string FullName => $"{ContainingClass?.Identifier.ValueText ?? "Unknown"}.{MethodName}";

    /// <summary>
    /// Resolved function references (populated during resolution phase)
    /// Format: "PluginName.FunctionName"
    /// </summary>
    public List<string> ResolvedFunctionReferences { get; set; } = new();

    /// <summary>
    /// Resolved plugin types (populated during resolution phase)
    /// </summary>
    public List<string> ResolvedPluginTypes { get; set; } = new();
}

/// <summary>
/// Information about skill options extracted from SkillFactory.Create() call
/// </summary>
internal class SkillOptionsInfo
{
    /// <summary>
    /// Scoping mode: InstructionOnly or Scoped
    /// </summary>
    public string ScopingMode { get; set; } = "InstructionOnly";

    /// <summary>
    /// Whether skill should auto-expand at conversation start
    /// </summary>
    public bool AutoExpand { get; set; } = false;

    /// <summary>
    /// Optional instruction document paths
    /// </summary>
    public List<string> InstructionDocuments { get; set; } = new();

    /// <summary>
    /// Base directory for instruction documents
    /// </summary>
    public string InstructionDocumentBaseDirectory { get; set; } = "skills/documents/";
}

/// <summary>
/// Information about a reference in a skill (function or skill reference)
/// </summary>
internal class ReferenceInfo
{
    /// <summary>
    /// Type of reference (function or skill)
    /// </summary>
    public ReferenceType ReferenceType { get; set; }

    /// <summary>
    /// Plugin type name (e.g., "FileSystemPlugin")
    /// </summary>
    public string PluginType { get; set; } = string.Empty;

    /// <summary>
    /// Namespace of the plugin
    /// </summary>
    public string PluginNamespace { get; set; } = string.Empty;

    /// <summary>
    /// Method name (e.g., "ReadFile" or "FileDebugging")
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// Full name: "PluginType.MethodName"
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Location in source code (for diagnostics)
    /// </summary>
    public Location? Location { get; set; }
}

/// <summary>
/// Type of reference
/// </summary>
internal enum ReferenceType
{
    /// <summary>
    /// Reference to a function (method with [AIFunction])
    /// </summary>
    Function,

    /// <summary>
    /// Reference to another skill (method returning Skill)
    /// </summary>
    Skill
}

/// <summary>
/// Resolved skill information after flattening nested references
/// </summary>
internal class ResolvedSkillInfo
{
    /// <summary>
    /// All function references (deduplicated)
    /// Format: "PluginName.FunctionName"
    /// </summary>
    public List<string> FunctionReferences { get; set; } = new();

    /// <summary>
    /// All plugin types referenced (deduplicated)
    /// </summary>
    public List<string> PluginTypes { get; set; } = new();
}
