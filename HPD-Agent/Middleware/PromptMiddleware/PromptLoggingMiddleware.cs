using System.Collections.Immutable;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace HPD.Agent.Internal.MiddleWare;

/// <summary>
/// Logs the final instructions after all Middlewares have executed.
/// MUST run LAST in the Middleware chain to capture the complete final state.
/// Logs only: final instructions and active Middlewares that modified them.
/// </summary>
internal class PromptLoggingMiddleware : IPromptMiddleware
{
    private readonly ILogger? _logger;
    private static int _callCounter = 0;

    public PromptLoggingMiddleware(ILogger? logger = null)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<ChatMessage>> InvokeAsync(
        PromptMiddlewareContext context,
        Func<PromptMiddlewareContext, Task<IEnumerable<ChatMessage>>> next)
    {
        // Capture full stack trace to understand the call chain
        var stackTrace = new System.Diagnostics.StackTrace(true);
        var relevantFrames = new System.Collections.Generic.List<string>();

        // Find frames that are part of our codebase (not async infrastructure)
        for (int i = 0; i < Math.Min(stackTrace.FrameCount, 15); i++)
        {
            var frame = stackTrace.GetFrame(i);
            var method = frame?.GetMethod();
            if (method != null)
            {
                var declaringType = method.DeclaringType?.FullName ?? "Unknown";
                // Skip async infrastructure and Middleware our own types
                if (!declaringType.Contains("AsyncMethodBuilder") &&
                    !declaringType.Contains("TaskAwaiter") &&
                    (declaringType.Contains("HPD.Agent") || declaringType.Contains("MessageProcessor")))
                {
                    relevantFrames.Add($"{method.DeclaringType?.Name}.{method.Name}");
                }
            }
        }

        var callChain = relevantFrames.Count > 0
            ? string.Join(" → ", relevantFrames.Take(3))
            : "Unknown";

        // Call next Middlewares first (this runs last, so next() completes the pipeline)
        var messages = await next(context);

        // Now log the final instructions after all Middlewares have run
        LogInstructions(context, callChain);

        return messages;
    }

    private void LogInstructions(PromptMiddlewareContext context, string callChain)
    {
        var callNumber = System.Threading.Interlocked.Increment(ref _callCounter);

        // Track ChatOptions object identity to see if same instance is reused
        var optionsHashCode = context.Options?.GetHashCode().ToString("X8") ?? "null";

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine($"📋 FINAL INSTRUCTIONS (After All Middlewares) - Call #{callNumber}");
        sb.AppendLine($"🔍 Call chain: {callChain}");
        sb.AppendLine($"📨 Message count: {context.Messages.Count()}");
        sb.AppendLine($"🆔 ChatOptions hash: {optionsHashCode}");
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine();

        // Log instructions
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
        if (_logger != null)
        {
            _logger.LogInformation(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    public Task PostInvokeAsync(PostInvokeContext context, CancellationToken cancellationToken)
    {
        // No post-processing needed
        return Task.CompletedTask;
    }
}
