using Microsoft.Extensions.AI;
using System.Linq;

/// <summary>
/// Responsible for executing tools and running the associated IAiFunctionFilter pipeline
/// </summary>
public class ToolScheduler
{
    private readonly FunctionCallProcessor _functionCallProcessor;

    /// <summary>
    /// Initializes a new instance of ToolScheduler
    /// </summary>
    /// <param name="functionCallProcessor">The function call processor to use for tool execution</param>
    public ToolScheduler(FunctionCallProcessor functionCallProcessor)
    {
        _functionCallProcessor = functionCallProcessor ?? throw new ArgumentNullException(nameof(functionCallProcessor));
    }

    /// <summary>
    /// Executes the requested tools in parallel and returns the tool response message
    /// </summary>
    /// <param name="currentHistory">The current conversation history</param>
    /// <param name="toolRequests">The tool call requests to execute</param>
    /// <param name="options">Optional chat options containing tool definitions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A chat message containing the tool execution results</returns>
    public async Task<ChatMessage> ExecuteToolsAsync(
        List<ChatMessage> currentHistory,
        List<FunctionCallContent> toolRequests,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        // For single tool calls, use sequential execution (no parallelization overhead)
        if (toolRequests.Count <= 1)
        {
            return await ExecuteSequentiallyAsync(currentHistory, toolRequests, options, cancellationToken);
        }
        
        // For multiple tool calls, execute in parallel for better performance
        return await ExecuteInParallelAsync(currentHistory, toolRequests, options, cancellationToken);
    }
    
    /// <summary>
    /// Executes tools sequentially (used for single tools or as fallback)
    /// </summary>
    private async Task<ChatMessage> ExecuteSequentiallyAsync(
        List<ChatMessage> currentHistory,
        List<FunctionCallContent> toolRequests,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        // Use the existing FunctionCallProcessor to execute the tools sequentially
        var resultMessages = await _functionCallProcessor.ProcessFunctionCallsAsync(
            currentHistory, options, toolRequests, cancellationToken);
        
        // Combine all tool results into a single message
        var allContents = new List<AIContent>();
        foreach (var message in resultMessages)
        {
            allContents.AddRange(message.Contents);
        }
        
        return new ChatMessage(ChatRole.Tool, allContents);
    }
    
    /// <summary>
    /// Executes tools in parallel for improved performance with multiple independent tools
    /// </summary>
    private async Task<ChatMessage> ExecuteInParallelAsync(
        List<ChatMessage> currentHistory,
        List<FunctionCallContent> toolRequests,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        // Create tasks for each tool execution
        var executionTasks = toolRequests.Select(async toolRequest =>
        {
            try
            {
                // Execute each tool call individually through the processor
                var singleToolList = new List<FunctionCallContent> { toolRequest };
                var resultMessages = await _functionCallProcessor.ProcessFunctionCallsAsync(
                    currentHistory, options, singleToolList, cancellationToken);
                
                return (Success: true, Messages: resultMessages, Error: (Exception?)null);
            }
            catch (Exception ex)
            {
                // Capture any errors for aggregation
                return (Success: false, Messages: new List<ChatMessage>(), Error: ex);
            }
        }).ToArray();
        
        // Wait for all tasks to complete
        var results = await Task.WhenAll(executionTasks);
        
        // Aggregate results and handle errors
        var allContents = new List<AIContent>();
        var errors = new List<Exception>();
        
        foreach (var result in results)
        {
            if (result.Success)
            {
                foreach (var message in result.Messages)
                {
                    allContents.AddRange(message.Contents);
                }
            }
            else if (result.Error != null)
            {
                errors.Add(result.Error);
            }
        }
        
        // If there were any errors, include them in the response
        if (errors.Count > 0)
        {
            var errorMessage = $"Some tool executions failed: {string.Join("; ", errors.Select(e => e.Message))}";
            allContents.Add(new TextContent($"⚠️ Tool Execution Errors: {errorMessage}"));
        }
        
        return new ChatMessage(ChatRole.Tool, allContents);
    }
}