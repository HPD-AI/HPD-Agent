using System;
using System.Collections.Generic;

/// <summary>
/// Tracks usage statistics and telemetry for an Agent instance.
/// Compatible with Microsoft.Extensions.AI observability patterns.
/// </summary>
public class AgentStatistics
{
    /// <summary>
    /// Total number of chat completion requests made
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// Total number of tokens consumed across all requests
    /// </summary>
    public long TotalTokensUsed { get; set; }

    /// <summary>
    /// Total number of tool/function calls executed
    /// </summary>
    public int TotalToolCalls { get; set; }

    /// <summary>
    /// Total processing time across all requests
    /// </summary>
    public TimeSpan TotalProcessingTime { get; set; }

    /// <summary>
    /// Timestamp of the most recent request
    /// </summary>
    public DateTime? LastRequestTime { get; set; }

    /// <summary>
    /// Count of tool calls by function name
    /// </summary>
    public Dictionary<string, int> ToolCallCounts { get; } = new();

    /// <summary>
    /// Average tokens per request (calculated property)
    /// </summary>
    public double AverageTokensPerRequest => TotalRequests > 0 ? (double)TotalTokensUsed / TotalRequests : 0;

    /// <summary>
    /// Average processing time per request (calculated property)
    /// </summary>
    public TimeSpan AverageProcessingTime => TotalRequests > 0 ?
        TimeSpan.FromTicks(TotalProcessingTime.Ticks / TotalRequests) :
        TimeSpan.Zero;

    /// <summary>
    /// Resets all statistics to their initial values
    /// </summary>
    public void Reset()
    {
        TotalRequests = 0;
        TotalTokensUsed = 0;
        TotalToolCalls = 0;
        TotalProcessingTime = TimeSpan.Zero;
        LastRequestTime = null;
        ToolCallCounts.Clear();
    }

    /// <summary>
    /// Records a completed request with optional usage information
    /// </summary>
    /// <param name="processingTime">Time taken to process the request</param>
    /// <param name="tokensUsed">Number of tokens consumed (if available)</param>
    public void RecordRequest(TimeSpan processingTime, int tokensUsed = 0)
    {
        TotalRequests++;
        TotalProcessingTime += processingTime;
        TotalTokensUsed += tokensUsed;
        LastRequestTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a tool call execution
    /// </summary>
    /// <param name="toolName">Name of the tool/function that was called</param>
    public void RecordToolCall(string toolName)
    {
        TotalToolCalls++;
        if (!string.IsNullOrEmpty(toolName))
        {
            ToolCallCounts.TryGetValue(toolName, out var count);
            ToolCallCounts[toolName] = count + 1;
        }
    }

    /// <summary>
    /// Returns a summary of the current statistics
    /// </summary>
    /// <returns>Formatted statistics summary</returns>
    public override string ToString()
    {
        return $"Requests: {TotalRequests}, Tokens: {TotalTokensUsed}, Tools: {TotalToolCalls}, " +
               $"Avg Time: {AverageProcessingTime.TotalMilliseconds:F1}ms";
    }
}