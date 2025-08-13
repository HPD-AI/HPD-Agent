using System;
using System.ComponentModel;

/// <summary>
/// Marks a method as an AI function that can be called by the AI model.
/// Functions must be public and can be async.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AIFunctionAttribute : Attribute
{
    /// <summary>
    /// Custom name for the function. If not specified, uses the method name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description of the function, suitable for model context.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Specifies that a function requires specific permissions to be executed.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresPermissionAttribute : Attribute
{
    /// <summary>
    /// The required permission string.
    /// </summary>
    public string Permission { get; }
    
    /// <summary>
    /// Initializes a new instance of the RequiresPermissionAttribute.
    /// </summary>
    /// <param name="permission">The required permission</param>
    public RequiresPermissionAttribute(string permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }
}
