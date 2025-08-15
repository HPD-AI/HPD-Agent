using System;
using System.Linq.Expressions;

/// <summary>
/// Marks a function as conditionally available based on a type-safe condition.
/// The condition is specified using a property name that will be evaluated against the context.
/// </summary>
/// <typeparam name="TContext">The type of context that provides the properties for evaluation.</typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ConditionalFunctionAttribute<TContext> : Attribute where TContext : IPluginMetadataContext
{
    /// <summary>
    /// The property expression that must evaluate to true for the function to be included.
    /// This uses a property name from the TContext type for compile-time validation.
    /// </summary>
    public string PropertyExpression { get; }
    
    /// <summary>
    /// The type of the context used for condition evaluation.
    /// </summary>
    public Type ContextType => typeof(TContext);
    
    /// <summary>
    /// Initializes a new instance of the ConditionalFunctionAttribute with a property-based condition.
    /// </summary>
    /// <param name="propertyExpression">A property expression like "HasTavilyProvider" or "HasBraveProvider && HasBingProvider"</param>
    public ConditionalFunctionAttribute(string propertyExpression)
    {
        PropertyExpression = propertyExpression ?? throw new ArgumentNullException(nameof(propertyExpression));
    }
}
