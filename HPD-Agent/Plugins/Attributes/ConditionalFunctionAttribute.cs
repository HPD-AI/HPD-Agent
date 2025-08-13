using System;

/// <summary>
/// Marks a function as conditionally available based on execution context.
/// Functions with false conditions are completely excluded from registration.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ConditionalFunctionAttribute : Attribute
{
    /// <summary>
    /// Context expression that must evaluate to true for the function to be included.
    /// Uses the same DSL syntax as description templates.
    /// </summary>
    public string Condition { get; }
    
    /// <summary>
    /// Initializes a new instance of the ConditionalFunctionAttribute.
    /// </summary>
    /// <param name="condition">The conditional expression</param>
    public ConditionalFunctionAttribute(string condition)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }
}
