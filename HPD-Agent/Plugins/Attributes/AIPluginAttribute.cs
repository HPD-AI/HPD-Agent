using System;

/// <summary>
/// Marks a class as an AI plugin that can be registered with the HPD-Agent system.
/// Supports context-aware metadata through DSL expressions.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class AIPluginAttribute : Attribute
{
    /// <summary>
    /// The name of the plugin as it will appear to the AI model.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Description of the plugin. Supports context expressions like {context.organizationName}.
    /// </summary>
    public string Description { get; }
    
    /// <summary>
    /// Initializes a new instance of the AIPluginAttribute.
    /// </summary>
    /// <param name="name">The plugin name</param>
    /// <param name="description">The plugin description (supports context expressions)</param>
    public AIPluginAttribute(string name, string description = "")
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? "";
    }
}
