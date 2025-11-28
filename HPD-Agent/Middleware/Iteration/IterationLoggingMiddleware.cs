using Microsoft.Extensions.Logging;
using System.Text;

namespace HPD.Agent.Internal.MiddleWare;

/// <summary>
/// Logs detailed information about each iteration for observability and debugging.
/// Provides pre and post LLM call logging with timing information.
/// Outputs full instructions to console to see skill injection in action.
/// </summary>
/// <remarks>
/// This filter is automatically registered when a logger is provided to the agent builder.
/// Similar to PromptLoggingFilter but runs before/after EACH LLM call in the agentic loop.
/// </remarks>
internal class IterationLoggingFilter : IIterationMiddleWare
{
    private readonly ILogger? _logger;
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private static int _iterationCounter = 0;

    public IterationLoggingFilter(ILogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called BEFORE the LLM call begins.
    /// Logs iteration start and full instructions to console.
    /// </summary>
    public Task BeforeIterationAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken)
    {
        _stopwatch.Restart();

        var iterationNumber = System.Threading.Interlocked.Increment(ref _iterationCounter);

        // Log full instructions to console (like PromptLoggingFilter)
        LogInstructionsToConsole(context, iterationNumber);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called AFTER LLM returns tool calls but BEFORE tools execute.
    /// Logs pending tool calls for visibility.
    /// </summary>
    public Task BeforeToolExecutionAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken)
    {
        if (context.ToolCalls.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🔨 Executing {context.ToolCalls.Count} tool(s):");
            foreach (var toolCall in context.ToolCalls)
            {
                sb.AppendLine($"   • {toolCall.Name}");
            }
            LogMessage(sb.ToString());
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called AFTER the LLM call completes.
    /// Logs iteration completion with timing and results.
    /// </summary>
    public Task AfterIterationAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken)
    {
        _stopwatch.Stop();

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════");

        if (context.IsSuccess)
        {
            sb.AppendLine($"✅ Iteration {context.Iteration} completed in {_stopwatch.ElapsedMilliseconds}ms");
            sb.AppendLine($"🔧 Tool calls: {context.ToolCalls.Count}");

            if (context.IsFinalIteration)
            {
                sb.AppendLine("🏁 FINAL ITERATION - Agent will respond to user");
            }
        }
        else
        {
            sb.AppendLine($"⚠️ Iteration {context.Iteration} completed with errors in {_stopwatch.ElapsedMilliseconds}ms");
        }

        sb.AppendLine("═══════════════════════════════════════════════════════");

        LogMessage(sb.ToString());

        return Task.CompletedTask;
    }

    private void LogInstructionsToConsole(IterationMiddleWareContext context, int iterationNumber)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine($"📋 ITERATION #{context.Iteration} INSTRUCTIONS (Before LLM Call #{iterationNumber})");
        sb.AppendLine($"🤖 Agent: {context.AgentName}");
        sb.AppendLine($"📨 Message count: {context.Messages.Count}");
        sb.AppendLine($"🔧 Tools available: {context.Options?.Tools?.Count ?? 0}");
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine();

        // Log full instructions (this is where we'll see skill injection!)
        if (!string.IsNullOrEmpty(context.Options?.Instructions))
        {
            sb.AppendLine(context.Options.Instructions);
        }
        else
        {
            sb.AppendLine("(no instructions)");
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════");

        LogMessage(sb.ToString());
    }

    private void LogMessage(string message)
    {
        // ALWAYS write directly to Console.Error to ensure it appears immediately
        // Console.Error is not buffered like Console.Out
        Console.Error.WriteLine(message);
        Console.Error.Flush();
    }
}
