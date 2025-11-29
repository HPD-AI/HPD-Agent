using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using System.Reflection;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

/// A modern, unified AIFunctionFactory that prioritizes delegate-based invocation 
/// for performance and AOT-compatibility.
/// </summary>
public class HPDAIFunctionFactory
{
    private static readonly HPDAIFunctionFactoryOptions _defaultOptions = new();

    /// <summary>
    /// Creates an AIFunction using a pre-compiled invocation delegate.
    /// This is the preferred method for source-generated plugins and adapters.
    /// </summary>
    public static AIFunction Create(
        Func<AIFunctionArguments, CancellationToken, Task<object?>> invocation, 
        HPDAIFunctionFactoryOptions? options = null)
    {
        return new HPDAIFunction(invocation, options ?? _defaultOptions);
    }


    /// <summary>
    /// Modern AIFunction implementation using delegate-based invocation with validation.
    /// </summary>
    public class HPDAIFunction : AIFunction
    {
        private readonly Func<AIFunctionArguments, CancellationToken, Task<object?>> _invocationHandler;
        private readonly MethodInfo? _method;

        // Constructor for the modern, delegate-based approach
        public HPDAIFunction(Func<AIFunctionArguments, CancellationToken, Task<object?>> invocationHandler, HPDAIFunctionFactoryOptions options)
        {
            _invocationHandler = invocationHandler ?? throw new ArgumentNullException(nameof(invocationHandler));
            _method = invocationHandler.Method; // For metadata
            HPDOptions = options;

            JsonSchema = options.SchemaProvider?.Invoke() ?? default;
            Name = options.Name ?? _method?.Name ?? "Unknown";
            Description = options.Description ?? "";
        }

        public HPDAIFunctionFactoryOptions HPDOptions { get; }
        public override string Name { get; }
        public override string Description { get; }
        public override JsonElement JsonSchema { get; }
        public override MethodInfo? UnderlyingMethod => _method;
        public override IReadOnlyDictionary<string, object?> AdditionalProperties
        {
            get
            {
                if (HPDOptions.AdditionalProperties == null)
                    return base.AdditionalProperties;

                // Return the dictionary as IReadOnlyDictionary
                return HPDOptions.AdditionalProperties;
            }
        }

        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            // 1. Robustly get the JSON arguments for validation.
            JsonElement jsonArgs;
            var existingJson = arguments.GetJson();
            if (existingJson.ValueKind != JsonValueKind.Undefined)
            {
                jsonArgs = existingJson;
            }
            else
            {
                // If no raw JSON is available, serialize the arguments dictionary.
                var argumentsDict = arguments.Where(kvp => kvp.Key != "__raw_json__").ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                var jsonString = JsonSerializer.Serialize(argumentsDict, HPDJsonContext.Default.DictionaryStringObject);
                jsonArgs = JsonDocument.Parse(jsonString).RootElement;
            }

            // 2. Container-specific validation: Check if this is a container being invoked with parameters
            var isContainer = HPDOptions.AdditionalProperties?.TryGetValue("IsContainer", out var containerVal) == true
                && containerVal is bool isCont && isCont;

            if (isContainer)
            {
                // Check if ANY parameters were provided
                bool hasParameters = jsonArgs.ValueKind == JsonValueKind.Object && jsonArgs.EnumerateObject().Any();

                if (hasParameters)
                {
                    // Extract function names if available
                    string[]? functionNames = null;
                    if (HPDOptions.AdditionalProperties?.TryGetValue("FunctionNames", out var funcNamesVal) == true
                        && funcNamesVal is string[] names)
                    {
                        functionNames = names;
                    }

                    // Extract plugin/container name
                    var containerName = HPDOptions.AdditionalProperties?.TryGetValue("PluginName", out var pluginNameVal) == true
                        ? pluginNameVal?.ToString()
                        : Name;

                    return new ContainerInvocationErrorResponse
                    {
                        ContainerName = containerName ?? Name,
                        AttemptedParameters = jsonArgs,
                        AvailableFunctions = functionNames,
                        ErrorMessage = $"'{containerName ?? Name}' is a container function that groups related functions. It cannot be called with parameters.",
                        RetryGuidance = GenerateRetryGuidanceWithMermaid(containerName ?? Name, functionNames)
                    };
                }
            }

            // 3. Use the validator for regular parameter validation.
            var validationErrors = HPDOptions.Validator?.Invoke(jsonArgs);

            if (validationErrors != null && validationErrors.Count > 0)
            {
                // 4. Return structured error on failure.
                var errorResponse = new ValidationErrorResponse();
                foreach (var error in validationErrors)
                {
                    if (jsonArgs.TryGetProperty(error.Property, out var propertyNode))
                    {
                        error.AttemptedValue = propertyNode.Clone();
                    }
                    errorResponse.Errors.Add(error);
                }
                return errorResponse;
            }

            // 5. Invoke the function using the delegate approach only.
            // NOTE: Must NOT use ConfigureAwait(false) here to preserve ExecutionContext flow for AsyncLocal (e.g., ConversationContext)
            arguments.SetJson(jsonArgs);
            return await _invocationHandler(arguments, cancellationToken);
        }
    }

    /// <summary>
    /// Generates retry guidance with embedded Mermaid diagram showing correct invocation flow.
    /// </summary>
    private static string GenerateRetryGuidanceWithMermaid(string containerName, string[]? functionNames)
    {
        if (functionNames != null && functionNames.Length > 0)
        {
            // Generate Mermaid flowchart showing the two-step process
            var funcList = string.Join(", ", functionNames.Take(5));
            if (functionNames.Length > 5) funcList += ", ...";

            var mermaidFlow = $"A[Call {containerName} with NO arguments] --> B{{{{Container Expands}}}} --> C[Now call individual functions: {funcList}]";

            return $"INCORRECT: You cannot call containers with parameters. " +
                   $"CORRECT FLOW: {mermaidFlow}. " +
                   $"This requires TWO separate tool calls: " +
                   $"(1) First call '{containerName}' with NO arguments to expand it. " +
                   $"(2) After expansion succeeds, call the individual function you need directly (e.g., '{functionNames[0]}', not '{containerName}.{functionNames[0]}').";
        }
        else
        {
            var mermaidFlow = $"A[Call {containerName} with NO arguments] --> B{{{{Container Expands}}}} --> C[Individual functions become available]";

            return $"INCORRECT: You cannot call containers with parameters. " +
                   $"CORRECT FLOW: {mermaidFlow}. " +
                   $"This requires TWO separate tool calls: " +
                   $"(1) First call '{containerName}' with NO arguments to expand it. " +
                   $"(2) After expansion succeeds, call the individual function you need.";
        }
    }
}

/// <summary>
/// Extensions to AIFunctionArguments for JSON handling.
/// </summary>
public static class AIFunctionArgumentsExtensions
{
    private static readonly string JsonKey = "__raw_json__";
    
    /// <summary>
    /// Gets the raw JSON element from the arguments.
    /// </summary>
    public static JsonElement GetJson(this AIFunctionArguments arguments)
    {
        if (arguments.TryGetValue(JsonKey, out var value) && value is JsonElement element)
        {
            return element;
        }
        return default;
    }
    
    /// <summary>
    /// Sets the raw JSON element in the arguments.
    /// </summary>
    public static void SetJson(this AIFunctionArguments arguments, JsonElement json)
    {
        arguments[JsonKey] = json;
    }
}

public class HPDAIFunctionFactoryOptions
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string>? ParameterDescriptions { get; set; }
    public bool RequiresPermission { get; set; }

    // The validator now returns a list of detailed, structured errors.
    public Func<JsonElement, List<ValidationError>>? Validator { get; set; }

    public Func<JsonElement>? SchemaProvider { get; set; }

    // Additional metadata properties for plugin scoping and other features
    public Dictionary<string, object?>? AdditionalProperties { get; set; }
}

/// <summary>
/// A structured response sent to the AI when function argument validation fails.
/// </summary>
public class ValidationErrorResponse
{
    [JsonPropertyName("error_type")]
    public string ErrorType { get; set; } = "validation_error";

    [JsonPropertyName("errors")]
    public List<ValidationError> Errors { get; set; } = new();

    [JsonPropertyName("retry_guidance")]
    public string RetryGuidance { get; set; } = "The provided arguments are invalid. Please review the errors, correct the arguments based on the function schema, and try again.";
}

/// <summary>
/// Describes a single validation error for a specific property, matching pydantic-ai's structure.
/// </summary>
public class ValidationError
{
    [JsonPropertyName("property")]
    public string Property { get; set; } = "";

    [JsonPropertyName("attempted_value")]
    public object? AttemptedValue { get; set; }

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; } = "";

    [JsonPropertyName("error_code")]
    public string ErrorCode { get; set; } = "";
}

/// <summary>
/// Response sent when the LLM tries to invoke a container function with parameters.
/// Containers must be called with no arguments to expand and reveal individual functions.
/// </summary>
public class ContainerInvocationErrorResponse
{
    [JsonPropertyName("error_type")]
    public string ErrorType { get; set; } = "container_invocation_error";

    [JsonPropertyName("container_name")]
    public string ContainerName { get; set; } = "";

    [JsonPropertyName("attempted_parameters")]
    public JsonElement? AttemptedParameters { get; set; }

    [JsonPropertyName("available_functions")]
    public string[]? AvailableFunctions { get; set; }

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; } = "";

    [JsonPropertyName("retry_guidance")]
    public string RetryGuidance { get; set; } = "This requires TWO separate tool calls: (1) First call the container with NO arguments to expand it. (2) After expansion succeeds, call the individual function you need.";
}
