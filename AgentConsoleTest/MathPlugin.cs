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

    public T GetProperty<T>(string propertyName, T defaultValue = default)
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

public class MathPlugin
{
    [AIFunction, Description("Adds two numbers and returns the sum.")]
    public long Add(
        [Description("First addend.")] long a,
        [Description("Second addend.")] long b)
        => a + b;

    [AIFunction, Description("Multiplies two numbers and returns the product.")]
    public long Multiply(
        [Description("First factor.")] long a,
        [Description("Second factor.")] long b)
        => a * b;

    [AIFunction, ConditionalFunction<MathPluginMetadataContext>("AllowNegative == false"), Description("Returns the absolute value. Only available if negatives are not allowed.")]
    public long Abs(
        [Description("Input value.")] long value)
        => Math.Abs(value);

    [AIFunction, ConditionalFunction<MathPluginMetadataContext>("MaxValue > 1000"), Description("Squares a number. Only available if maxValue > 1000.")]
    public long Square(
        [Description("Input value.")] long value)
        => value * value;

    [AIFunction, ConditionalFunction<MathPluginMetadataContext>("AllowNegative == true"), Description("Subtracts b from a. Only available if negatives are allowed.")]
    public long Subtract(
        [Description("Minuend.")] long a,
        [Description("Subtrahend.")] long b)
        => a - b;

    [AIFunction, ConditionalFunction<MathPluginMetadataContext>("MaxValue < 500"), Description("Returns the minimum of two numbers. Only available if maxValue < 500.")]
    public long Min(
        [Description("First value.")] long a,
        [Description("Second value.")] long b)
        => Math.Min(a, b);
}
