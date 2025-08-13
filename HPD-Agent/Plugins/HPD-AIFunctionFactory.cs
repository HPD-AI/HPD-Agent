using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using System.Reflection;

/// <summary>
/// Extended AIFunctionFactory that supports parameter descriptions, invocation filters, and enhanced JSON schema generation.
/// </summary>
public class HPDAIFunctionFactory
{
    private static readonly HPDAIFunctionFactoryOptions _defaultOptions = new();
    // AOT-compatible JSON context for basic serialization
    private static readonly AOTJsonContext _aotJsonContext = AOTJsonContext.Default;

    /// <summary>
    /// Creates an AIFunction with rich parameter descriptions and invocation filters.
    /// </summary>
    public static AIFunction Create(Delegate method, HPDAIFunctionFactoryOptions? options = null)
    {
        return new HPDAIFunction(method.Method, method.Target, options ?? _defaultOptions);
    }

    /// <summary>
    /// Creates an AIFunction with rich parameter descriptions and invocation filters.
    /// </summary>
    public static AIFunction Create(MethodInfo method, object? target, HPDAIFunctionFactoryOptions? options = null)
    {
        return new HPDAIFunction(method, target, options ?? _defaultOptions);
    }

    /// <summary>
    /// AIFunction implementation that includes parameter descriptions in its JSON schema and supports invocation filters.
    /// </summary>
    private class HPDAIFunction : AIFunction
    {
        private readonly MethodInfo _method;
        private readonly object? _target;
        private readonly HPDAIFunctionFactoryOptions _options;
        private readonly Lazy<JsonElement> _jsonSchema;
        private readonly Lazy<JsonElement?> _returnJsonSchema;

        public HPDAIFunction(MethodInfo method, object? target, HPDAIFunctionFactoryOptions options)
        {
            _method = method ?? throw new ArgumentNullException(nameof(method));
            _target = target;
            _options = options;

            _jsonSchema = new Lazy<JsonElement>(() => CreateJsonSchema());
            _returnJsonSchema = new Lazy<JsonElement?>(() => CreateReturnJsonSchema());

            Name = options.Name ?? method.Name;
            Description = options.Description ??
                method.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description ?? "";
        }

        public override string Name { get; }
        public override string Description { get; }
        public override JsonElement JsonSchema => _jsonSchema.Value;
        public override JsonElement? ReturnJsonSchema => _returnJsonSchema.Value;
        public override MethodInfo? UnderlyingMethod => _method;

        private JsonElement CreateJsonSchema()
        {
            var parameters = _method.GetParameters();
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            foreach (var param in parameters)
            {
                // Skip special parameters like CancellationToken
                if (param.ParameterType == typeof(CancellationToken) ||
                    param.ParameterType == typeof(AIFunctionArguments) ||
                    param.ParameterType == typeof(IServiceProvider))
                {
                    continue;
                }

                // Get description from options first, then fall back to attribute
                var description = _options.ParameterDescriptions?.ContainsKey(param.Name!) == true
                    ? _options.ParameterDescriptions[param.Name!]
                    : param.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;

                var paramSchema = new Dictionary<string, object>
                {
                    { "type", GetJsonType(param.ParameterType) },
                };

                // Add description if available
                if (!string.IsNullOrEmpty(description))
                {
                    paramSchema["description"] = description;
                }

                properties[param.Name!] = paramSchema;

                // Add to required list if parameter has no default value
                if (!param.HasDefaultValue)
                {
                    required.Add(param.Name!);
                }
            }

            var schema = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", properties }
            };

            if (required.Count > 0)
            {
                schema["required"] = required;
            }

            // FIX: Use AOT-compatible serialization
            var jsonString = JsonSerializer.Serialize(schema, _aotJsonContext.DictionaryStringObject);
            return JsonSerializer.Deserialize(jsonString, _aotJsonContext.JsonElement);
        }

        private JsonElement? CreateReturnJsonSchema()
        {
            if (_method.ReturnType == typeof(void) ||
                _method.ReturnType == typeof(Task) ||
                _method.ReturnType == typeof(ValueTask))
            {
                return null;
            }

            var returnType = _method.ReturnType;
            if (returnType.IsGenericType)
            {
                var genericTypeDef = returnType.GetGenericTypeDefinition();
                if (genericTypeDef == typeof(Task<>) ||
                    genericTypeDef == typeof(ValueTask<>))
                {
                    returnType = returnType.GetGenericArguments()[0];
                }
            }

            var schema = new Dictionary<string, object>
            {
                { "type", GetJsonType(returnType) }
            };

            var description = _method.ReturnParameter.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;
            if (!string.IsNullOrEmpty(description))
            {
                schema["description"] = description;
            }

            // FIX: Use AOT-compatible serialization
            var jsonString = JsonSerializer.Serialize(schema, _aotJsonContext.DictionaryStringObject);
            return JsonSerializer.Deserialize(jsonString, _aotJsonContext.JsonElement);
        }

        private string GetJsonType(Type type)
        {
            if (type == typeof(string))
                return "string";
            if (type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double))
                return "number";
            if (type == typeof(bool))
                return "boolean";
            if (type.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
                return "array";
            return "object";
        }

        [UnconditionalSuppressMessage("AOT", "IL2067", Justification = "Type conversion needed for function parameters")]
        private object? ConvertArgument(object? value, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type targetType)
        {
            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            if (targetType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }

            if (value is JsonElement jsonElement)
            {
                // FIX: Use custom AOT-safe conversion instead of reflection-based deserialization
                return ConvertJsonElementToType(jsonElement, targetType);
            }

            // FIX: Use custom conversion for complex types
            return ConvertObjectToType(value, targetType);
        }

        [UnconditionalSuppressMessage("AOT", "IL2067", Justification = "Type conversion needed for function parameters")]
        private object? ConvertArgumentUnsafe(object? value, Type targetType)
        {
            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            if (targetType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }

            if (value is JsonElement jsonElement)
            {
                // FIX: Use custom AOT-safe conversion instead of reflection-based deserialization
                return ConvertJsonElementToType(jsonElement, targetType);
            }

            // FIX: Use custom conversion for complex types
            return ConvertObjectToType(value, targetType);
        }

        private object? ConvertJsonElementToType(JsonElement jsonElement, Type targetType)
        {
            // AOT-safe conversion for common types
            if (targetType == typeof(string))
                return jsonElement.GetString();
            if (targetType == typeof(int))
                return jsonElement.GetInt32();
            if (targetType == typeof(long))
                return jsonElement.GetInt64();
            if (targetType == typeof(double))
                return jsonElement.GetDouble();
            if (targetType == typeof(float))
                return jsonElement.GetSingle();
            if (targetType == typeof(bool))
                return jsonElement.GetBoolean();
            if (targetType == typeof(decimal))
                return jsonElement.GetDecimal();

            // For complex types, convert to string and back
            var jsonString = jsonElement.GetRawText();
            return ConvertFromJsonString(jsonString, targetType);
        }

        private object? ConvertObjectToType(object value, Type targetType)
        {
            // Simple type conversions
            if (targetType == typeof(string))
                return value.ToString();
            
            // For complex conversions, serialize and deserialize
            var jsonString = JsonSerializer.Serialize(value, _aotJsonContext.Object);
            return ConvertFromJsonString(jsonString, targetType);
        }

        private object? ConvertFromJsonString(string jsonString, Type targetType)
        {
            // AOT-safe conversions for known types
            if (targetType == typeof(string))
                return JsonSerializer.Deserialize(jsonString, _aotJsonContext.String);
            if (targetType == typeof(int))
                return JsonSerializer.Deserialize(jsonString, _aotJsonContext.Int32);
            if (targetType == typeof(long))
                return JsonSerializer.Deserialize(jsonString, _aotJsonContext.Int64);
            if (targetType == typeof(double))
                return JsonSerializer.Deserialize(jsonString, _aotJsonContext.Double);
            if (targetType == typeof(float))
                return JsonSerializer.Deserialize(jsonString, _aotJsonContext.Single);
            if (targetType == typeof(bool))
                return JsonSerializer.Deserialize(jsonString, _aotJsonContext.Boolean);

            // For other types, return the JSON string and let the method handle it
            return jsonString;
        }

        // --- VVV THIS IS THE CORE CHANGE VVV ---
        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            var parameters = _method.GetParameters();
            var args = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                if (param.ParameterType == typeof(CancellationToken))
                {
                    args[i] = cancellationToken;
                }
                else if (param.ParameterType == typeof(AIFunctionArguments))
                {
                    args[i] = arguments;
                }
                else if (param.ParameterType == typeof(IServiceProvider))
                {
                    args[i] = arguments.Services;
                }
                else
                {
                    if (!arguments.TryGetValue(param.Name!, out var value))
                    {
                        if (param.HasDefaultValue)
                        {
                            args[i] = param.DefaultValue;
                            continue;
                        }
                        throw new ArgumentException($"Required parameter '{param.Name}' was not provided.");
                    }
                    args[i] = ConvertArgumentUnsafe(value, param.ParameterType);
                }
            }

            var result = _method.Invoke(_target, args);
            if (result is Task task)
            {
                await task.ConfigureAwait(true);
                result = ExtractTaskResult(task);
            }
            else if (result != null && result.GetType().Name.StartsWith("ValueTask"))
            {
                if (result is ValueTask nonGenericValueTask)
                {
                    await nonGenericValueTask.ConfigureAwait(true);
                    result = null;
                }
                else
                {
                    result = null;
                }
            }
            return result;
        }

        private static object? ExtractTaskResult(Task task)
        {
            // Use pattern matching instead of reflection for AOT compatibility
            return task switch
            {
                Task<object> objTask => objTask.Result,
                Task<string> stringTask => stringTask.Result,
                Task<int> intTask => intTask.Result,
                Task<long> longTask => longTask.Result,
                Task<double> doubleTask => doubleTask.Result,
                Task<float> floatTask => floatTask.Result,
                Task<bool> boolTask => boolTask.Result,
                Task<decimal> decimalTask => decimalTask.Result,
                _ => null // For non-generic tasks or unsupported types
            };
        }

        private static object? ExtractValueTaskResult(ValueTask valueTask)
        {
            // For ValueTask, we need to check the type using different approach
            // Since ValueTask is a struct, we can't use pattern matching directly
            // Return null for non-generic ValueTasks since they don't have results
            return null;
        }
    }
}

/// <summary>
/// Options for HPDAIFunctionFactory with enhanced metadata support.
/// </summary>
public class HPDAIFunctionFactoryOptions
{
    /// <summary>
    /// Optional name override for the function.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Optional description override for the function.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional JsonSerializerOptions for parameter marshalling.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }

    /// <summary>
    /// Parameter descriptions mapped by parameter name.
    /// </summary>
    public Dictionary<string, string>? ParameterDescriptions { get; set; }
}

/// <summary>
/// AOT-compatible JSON context for basic type serialization
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<object>))]
[JsonSerializable(typeof(List<Dictionary<string, object>>))]
[JsonSerializable(typeof(List<Dictionary<string, object?>>))]
internal partial class AOTJsonContext : JsonSerializerContext
{
}