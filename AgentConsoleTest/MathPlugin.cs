using System;
using System.ComponentModel;

/// <summary>
/// Simple math plugin for testing plugin registration and invocation.
/// </summary>
public class MathPluginMetadataContext : IPluginMetadataContext
{
    private readonly Dictionary<string, object> _properties = new();

    public MathPluginMetadataContext(long maxValue = 1000, bool allowNegative = true)
    {
        _properties["maxValue"] = maxValue;
        _properties["allowNegative"] = allowNegative;
        MaxValue = maxValue;
        AllowNegative = allowNegative;
    }

    // ✅ V2: Strongly-typed properties for compile-time validation
    public long MaxValue { get; }
    public bool AllowNegative { get; }

    public T? GetProperty<T>(string propertyName, T? defaultValue = default)
    {
        if (_properties.TryGetValue(propertyName, out var value))
        {
            if (value is T typedValue)
                return typedValue;
            if (typeof(T) == typeof(string))
                return (T)(object)value.ToString()!;
        }
        return defaultValue;
    }

    public bool HasProperty(string propertyName) => _properties.ContainsKey(propertyName);
    public IEnumerable<string> GetPropertyNames() => _properties.Keys;
}

[PluginScope(
    description: "Mathematical operations including addition, subtraction, multiplication, and more. Invoke first to get access to functions",
    postExpansionInstructions: @"
Best practices for using MathPlugin:
- For complex calculations, break them into atomic operations (Add, Subtract, Multiply)
- Use Square(x) for x², it's optimized and clearer than Multiply(x, x)
- Some functions are conditional and only visible based on context:
  * Abs() - Only available when AllowNegative=false
  * Square() - Only available when MaxValue>1000
  * Subtract() - Only available when AllowNegative=true
  * Min() - Only available when MaxValue<500

Performance tips:
- Chain operations by calling functions sequentially
- Add() requires permission - approve once to use multiple times
- All operations return immediately (no async overhead)

Example workflow:
1. Use Multiply for basic calculations
2. If you need x², prefer Square over Multiply(x,x)
3. For negative results, ensure Subtract is available in current context
    ")]
public class ExpandMathPlugin
{
    [AIFunction<MathPluginMetadataContext>]
    [AIDescription("Adds two numbers and returns the sum.")]
    [RequiresPermission]
    public decimal Add(
        [AIDescription("First addend.")] decimal a,
        [AIDescription("Second addend.")] decimal b)
        => a + b;

    [AIFunction<MathPluginMetadataContext>]
    [AIDescription("Multiplies two numbers and returns the product.")]
    public long Multiply(
        [AIDescription("First factor.")] long a,
        [AIDescription("Second factor.")] long b)
        => a * b;

    [AIFunction<MathPluginMetadataContext>]
    [ConditionalFunction("AllowNegative == false")]
    [AIDescription("Returns the absolute value. Only available if negatives are not allowed.")]
    public long Abs(
        [AIDescription("Input value.")] long value)
        => Math.Abs(value);

    [AIFunction<MathPluginMetadataContext>]
    [ConditionalFunction("MaxValue > 1000")]
    [AIDescription("Squares a number. Only available if maxValue > 1000.")]
    public long Square(
        [AIDescription("Input value.")] long value)
        => value * value;

    [AIFunction<MathPluginMetadataContext>]
    [ConditionalFunction("AllowNegative == true")]
    [AIDescription("Subtracts b from a. Only available if negatives are allowed.")]
    public long Subtract(
        [AIDescription("Minuend.")] long a,
        [AIDescription("Subtrahend.")] long b)
        => a - b;

    [AIFunction<MathPluginMetadataContext>]
    [ConditionalFunction("MaxValue < 500")]
    [AIDescription("Returns the minimum of two numbers. Only available if maxValue < 500.")]
    public long Min(
        [AIDescription("First value.")] long a,
        [AIDescription("Second value.")] long b)
        => Math.Min(a, b);
}
