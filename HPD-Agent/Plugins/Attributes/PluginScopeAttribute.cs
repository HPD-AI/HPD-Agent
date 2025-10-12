using System;

/// <summary>
/// Marks a plugin class for scoping. When scoped, the plugin's functions are hidden
/// until the plugin container is explicitly expanded by the agent.
/// This reduces token consumption and cognitive load by organizing functions hierarchically.
/// </summary>
/// <example>
/// <code>
/// [PluginScope("Search operations across web, code, and documentation")]
/// public class SearchPlugin
/// {
///     [AIFunction]
///     [AIDescription("Search the web for information")]
///     public async Task&lt;string&gt; WebSearch(string query) { ... }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PluginScopeAttribute : Attribute
{
    /// <summary>
    /// Description of the plugin shown in the container function.
    /// This helps the agent understand when to expand this plugin.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new instance of the PluginScopeAttribute with the specified description.
    /// </summary>
    /// <param name="description">Brief description of plugin capabilities (e.g., "Search operations", "Database utilities")</param>
    /// <exception cref="ArgumentNullException">Thrown when description is null</exception>
    public PluginScopeAttribute(string description)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }
}
