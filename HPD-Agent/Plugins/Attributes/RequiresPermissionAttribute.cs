using System;

/// <summary>
/// Marks a function as requiring user permission before execution.
/// Functions with this attribute will trigger permission requests to the configured handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresPermissionAttribute : Attribute
{
    // This attribute acts as a simple boolean flag and requires no parameters.
}
