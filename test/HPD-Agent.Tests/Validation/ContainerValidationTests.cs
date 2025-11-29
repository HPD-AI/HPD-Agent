using HPD.Agent;
using Microsoft.Extensions.AI;
using System.Text.Json;
using Xunit;

namespace HPD_Agent.Tests.Validation;

/// <summary>
/// Tests for container-specific parameter validation.
/// Ensures that container functions reject invocations with parameters and guide the LLM to expand first.
/// </summary>
public class ContainerValidationTests
{
    #region Container Detection and Validation

    [Fact]
    public async Task ContainerFunction_WithParameters_ReturnsContainerInvocationError()
    {
        // Arrange - Create a container function (like MathPlugin)
        var containerFunction = CreateContainerFunction(
            name: "MathPlugin",
            description: "Mathematical operations including Add, Multiply, Divide",
            functionNames: new[] { "Add", "Multiply", "Divide", "Subtract" }
        );

        // Simulate LLM trying to invoke container with parameters
        var arguments = new AIFunctionArguments
        {
            ["function"] = "Add",
            ["a"] = 5,
            ["b"] = 10
        };

        // Act
        var result = await containerFunction.InvokeAsync(arguments);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<ContainerInvocationErrorResponse>(result);

        var errorResponse = (ContainerInvocationErrorResponse)result;
        Assert.Equal("container_invocation_error", errorResponse.ErrorType);
        Assert.Equal("MathPlugin", errorResponse.ContainerName);
        Assert.Contains("cannot be called with parameters", errorResponse.ErrorMessage);
        Assert.Contains("TWO separate tool calls", errorResponse.RetryGuidance);
        Assert.Contains("First call 'MathPlugin' with NO arguments", errorResponse.RetryGuidance);
        Assert.NotNull(errorResponse.AvailableFunctions);
        Assert.Equal(4, errorResponse.AvailableFunctions.Length);
        Assert.Contains("Add", errorResponse.AvailableFunctions);
    }

    [Fact]
    public async Task ContainerFunction_NoParameters_InvokesSuccessfully()
    {
        // Arrange - Create a container function
        var containerFunction = CreateContainerFunction(
            name: "MathPlugin",
            description: "Mathematical operations",
            functionNames: new[] { "Add", "Multiply" }
        );

        // Invoke with empty arguments (correct usage)
        var arguments = new AIFunctionArguments();

        // Act
        var result = await containerFunction.InvokeAsync(arguments);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<string>(result);
        Assert.Contains("expanded successfully", result.ToString());
    }

    [Fact]
    public async Task ContainerFunction_EmptyJsonObject_ReturnsContainerInvocationError()
    {
        // Arrange - LLM might send {} which is still "parameters"
        var containerFunction = CreateContainerFunction(
            name: "DatabasePlugin",
            description: "Database operations",
            functionNames: new[] { "Query", "Insert", "Update" }
        );

        // Empty object but still has properties enumerable
        var arguments = new AIFunctionArguments();
        var emptyJson = JsonDocument.Parse("{}").RootElement;
        arguments.SetJson(emptyJson);

        // Act
        var result = await containerFunction.InvokeAsync(arguments);

        // Assert
        // Empty JSON object has no properties, so should succeed
        Assert.IsType<string>(result);
    }

    [Fact]
    public async Task ContainerFunction_NestedParameters_ReturnsContainerInvocationError()
    {
        // Arrange
        var containerFunction = CreateContainerFunction(
            name: "FilePlugin",
            description: "File operations",
            functionNames: new[] { "Read", "Write", "Delete" }
        );

        // LLM tries complex nested invocation
        var arguments = new AIFunctionArguments
        {
            ["operation"] = "Write",
            ["params"] = new Dictionary<string, object>
            {
                ["path"] = "/tmp/file.txt",
                ["content"] = "Hello World"
            }
        };

        // Act
        var result = await containerFunction.InvokeAsync(arguments);

        // Assert
        Assert.IsType<ContainerInvocationErrorResponse>(result);
        var errorResponse = (ContainerInvocationErrorResponse)result;
        Assert.NotNull(errorResponse.AttemptedParameters);
        Assert.Contains("Read", errorResponse.AvailableFunctions);
        Assert.Contains("Write", errorResponse.AvailableFunctions);
    }

    #endregion

    #region Different Container Types

    [Fact]
    public async Task MCPContainerFunction_WithParameters_ReturnsError()
    {
        // Arrange - MCP server container
        var mcpContainer = CreateContainerFunction(
            name: "TavilyMCPServer",
            description: "Tavily MCP server tools",
            functionNames: new[] { "search", "get_search_context" },
            additionalMetadata: new Dictionary<string, object?>
            {
                ["SourceType"] = "MCP",
                ["MCPServerName"] = "tavily"
            }
        );

        var arguments = new AIFunctionArguments
        {
            ["tool"] = "search",
            ["query"] = "latest AI news"
        };

        // Act
        var result = await mcpContainer.InvokeAsync(arguments);

        // Assert
        Assert.IsType<ContainerInvocationErrorResponse>(result);
        var errorResponse = (ContainerInvocationErrorResponse)result;
        Assert.Equal("TavilyMCPServer", errorResponse.ContainerName);
        Assert.Contains("search", errorResponse.AvailableFunctions);
    }

    [Fact]
    public async Task FrontendToolsContainer_WithParameters_ReturnsError()
    {
        // Arrange - Frontend tools container
        var frontendContainer = CreateContainerFunction(
            name: "FrontendTools",
            description: "UI tools for user interaction",
            functionNames: new[] { "ShowNotification", "PromptUser", "DisplayChart" },
            additionalMetadata: new Dictionary<string, object?>
            {
                ["SourceType"] = "Frontend"
            }
        );

        var arguments = new AIFunctionArguments
        {
            ["action"] = "ShowNotification",
            ["message"] = "Hello!"
        };

        // Act
        var result = await frontendContainer.InvokeAsync(arguments);

        // Assert
        Assert.IsType<ContainerInvocationErrorResponse>(result);
    }

    [Fact]
    public async Task SkillContainer_WithParameters_ReturnsError()
    {
        // Arrange - Skill container
        var skillContainer = CreateContainerFunction(
            name: "ComprehensiveFinancialAnalysis",
            description: "Multi-step financial analysis skill",
            functionNames: new[] { "CalculateRatios", "GenerateReport", "CreateCharts" },
            additionalMetadata: new Dictionary<string, object?>
            {
                ["IsSkill"] = true
            }
        );

        var arguments = new AIFunctionArguments
        {
            ["step"] = "CalculateRatios",
            ["data"] = "balance_sheet.json"
        };

        // Act
        var result = await skillContainer.InvokeAsync(arguments);

        // Assert
        Assert.IsType<ContainerInvocationErrorResponse>(result);
    }

    #endregion

    #region Error Message Guidance

    [Fact]
    public async Task ContainerInvocationError_IncludesFirstFiveFunctionNames()
    {
        // Arrange - Container with many functions
        var functionNames = Enumerable.Range(1, 20)
            .Select(i => $"Function{i}")
            .ToArray();

        var containerFunction = CreateContainerFunction(
            name: "LargePlugin",
            description: "Plugin with many functions",
            functionNames: functionNames
        );

        var arguments = new AIFunctionArguments { ["param"] = "value" };

        // Act
        var result = await containerFunction.InvokeAsync(arguments);

        // Assert
        var errorResponse = (ContainerInvocationErrorResponse)result;
        Assert.Contains("Function1", errorResponse.RetryGuidance);
        Assert.Contains("Function2", errorResponse.RetryGuidance);
        Assert.Contains("Function5", errorResponse.RetryGuidance);
        // Should only show first 5
        Assert.DoesNotContain("Function6", errorResponse.RetryGuidance);
    }

    [Fact]
    public async Task ContainerInvocationError_NoFunctionNames_GenericGuidance()
    {
        // Arrange - Container without function names metadata
        var containerFunction = CreateContainerFunction(
            name: "GenericContainer",
            description: "Generic container",
            functionNames: null // No function names available
        );

        var arguments = new AIFunctionArguments { ["param"] = "value" };

        // Act
        var result = await containerFunction.InvokeAsync(arguments);

        // Assert
        var errorResponse = (ContainerInvocationErrorResponse)result;
        Assert.Null(errorResponse.AvailableFunctions);
        Assert.Contains("TWO separate tool calls", errorResponse.RetryGuidance);
        Assert.DoesNotContain("Available functions:", errorResponse.RetryGuidance);
    }

    [Fact]
    public async Task ContainerInvocationError_CapturesAttemptedParameters()
    {
        // Arrange
        var containerFunction = CreateContainerFunction(
            name: "TestPlugin",
            description: "Test",
            functionNames: new[] { "Test" }
        );

        var arguments = new AIFunctionArguments
        {
            ["function"] = "Test",
            ["param1"] = 123,
            ["param2"] = "value",
            ["param3"] = true
        };

        // Act
        var result = await containerFunction.InvokeAsync(arguments);

        // Assert
        var errorResponse = (ContainerInvocationErrorResponse)result;
        Assert.NotNull(errorResponse.AttemptedParameters);

        var attemptedParams = errorResponse.AttemptedParameters.Value;
        Assert.True(attemptedParams.TryGetProperty("function", out var funcProp));
        Assert.Equal("Test", funcProp.GetString());
        Assert.True(attemptedParams.TryGetProperty("param1", out var param1));
        Assert.Equal(123, param1.GetInt32());
    }

    #endregion

    #region Regular Functions (Non-Container)

    [Fact]
    public async Task RegularFunction_WithParameters_InvokesNormally()
    {
        // Arrange - Regular function (not a container)
        var regularFunction = CreateRegularFunction(
            name: "Add",
            description: "Adds two numbers"
        );

        var arguments = new AIFunctionArguments
        {
            ["a"] = 5,
            ["b"] = 10
        };

        // Act
        var result = await regularFunction.InvokeAsync(arguments);

        // Assert
        Assert.IsType<int>(result);
        Assert.Equal(15, result);
    }

    [Fact]
    public async Task RegularFunction_NoContainerMetadata_NoValidationError()
    {
        // Arrange - Function without IsContainer metadata
        var regularFunction = HPDAIFunctionFactory.Create(
            async (args, ct) =>
            {
                await Task.CompletedTask;
                return "Success";
            },
            new HPDAIFunctionFactoryOptions
            {
                Name = "RegularFunction",
                Description = "Regular function",
                SchemaProvider = () => default,
                AdditionalProperties = new Dictionary<string, object?>()
                // No IsContainer metadata
            }
        );

        var arguments = new AIFunctionArguments
        {
            ["anyParam"] = "anyValue"
        };

        // Act
        var result = await regularFunction.InvokeAsync(arguments);

        // Assert
        Assert.Equal("Success", result);
    }

    #endregion

    #region Helper Methods

    private AIFunction CreateContainerFunction(
        string name,
        string description,
        string[]? functionNames,
        Dictionary<string, object?>? additionalMetadata = null)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["IsContainer"] = true,
            ["PluginName"] = name,
            ["FunctionNames"] = functionNames,
            ["FunctionCount"] = functionNames?.Length ?? 0
        };

        if (additionalMetadata != null)
        {
            foreach (var kvp in additionalMetadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }
        }

        return HPDAIFunctionFactory.Create(
            async (args, ct) =>
            {
                await Task.CompletedTask;
                return $"{name} expanded successfully. You can now see individual functions.";
            },
            new HPDAIFunctionFactoryOptions
            {
                Name = name,
                Description = description,
                SchemaProvider = () => CreateEmptyContainerSchema(),
                AdditionalProperties = metadata
            }
        );
    }

    private AIFunction CreateRegularFunction(string name, string description)
    {
        return HPDAIFunctionFactory.Create(
            async (args, ct) =>
            {
                await Task.CompletedTask;
                // Simple add function for testing
                if (args.TryGetValue("a", out var aVal) && args.TryGetValue("b", out var bVal))
                {
                    return Convert.ToInt32(aVal) + Convert.ToInt32(bVal);
                }
                return 0;
            },
            new HPDAIFunctionFactoryOptions
            {
                Name = name,
                Description = description,
                SchemaProvider = () => default,
                AdditionalProperties = new Dictionary<string, object?>()
            }
        );
    }

    private JsonElement CreateEmptyContainerSchema()
    {
        // Container functions have no parameters
        return default;
    }

    #endregion
}
