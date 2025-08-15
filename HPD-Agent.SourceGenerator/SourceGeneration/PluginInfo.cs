using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Information about a plugin discovered during source generation.
/// </summary>
internal class PluginInfo
{
    public string Name { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public List<FunctionInfo> Functions { get; set; } = new();
    public bool HasContextAwareMetadata => Description.Contains("{context.");
}

/// <summary>
/// Information about a function discovered during source generation.
/// </summary>
internal class FunctionInfo
{
    public string Name { get; set; } = string.Empty;
    public string? CustomName { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<ParameterInfo> Parameters { get; set; } = new();
    public string ReturnType { get; set; } = string.Empty;
    public bool IsAsync { get; set; }
    public bool RequiresContext { get; set; }
    public List<string> RequiredPermissions { get; set; } = new();
    public bool HasContextAwareMetadata => Description.Contains("{context.");
    
    /// <summary>
    /// V2 property-based conditional expression (null if not conditional)
    /// </summary>
    public string? ConditionalExpressionV2 { get; set; }
    
    /// <summary>
    /// Type name of the context for V2 conditional expressions
    /// </summary>
    public string? ConditionalContextTypeName { get; set; }
    
    /// <summary>
    /// Whether this function has a conditional inclusion requirement
    /// </summary>
    public bool IsConditional => !string.IsNullOrEmpty(ConditionalExpressionV2);

    public string FunctionName => CustomName ?? Name;

    /// <summary>
    /// Gets a dictionary of parameter descriptions for this function.
    /// </summary>
    public Dictionary<string, string> ParameterDescriptions => Parameters?.Count > 0
        ? Parameters.ToDictionary(p => p.Name, p => p.Description ?? string.Empty)
        : new Dictionary<string, string>();
}

/// <summary>
/// Information about a function parameter discovered during source generation.
/// </summary>
internal class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool HasDefaultValue { get; set; }
    public string? DefaultValue { get; set; }
}
