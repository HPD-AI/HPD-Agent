using Microsoft.Extensions.AI;
using System.Threading.Channels;
using System.Runtime.CompilerServices;
using CoreAgent = HPD.Agent.AgentCore;

namespace HPD.Agent.AGUI;

/// <summary>
/// AGUI protocol adapter for HPD-Agent.
/// Wraps the protocol-agnostic core agent and provides AGUI protocol compatibility.
/// </summary>
public sealed class Agent
{
    private readonly CoreAgent _core;
    private readonly AGUIEventConverter _converter;

/// <summary>
/// Initializes a new AGUI protocol agent instance.
/// Internal constructor - use AgentBuilder.BuildAGUI() to create agents.
/// </summary>
internal Agent(CoreAgent core)
{
    _core = core ?? throw new ArgumentNullException(nameof(core));
    _converter = new AGUIEventConverter();
}    /// <summary>
    /// Agent name (delegated to core)
    /// </summary>
    public string Name => _core.Name;

    /// <summary>
    /// Runs the agent with AGUI protocol input and streams events to the provided channel.
    /// This is the primary AGUI protocol entry point.
    /// </summary>
    /// <param name="input">AGUI run agent input containing messages, tools, and context</param>
    /// <param name="events">Channel writer for streaming AGUI BaseEvent events</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task RunAsync(
        RunAgentInput input,
        ChannelWriter<BaseEvent> events,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert AGUI input to Extensions.AI format (ensure non-null)
            var messageList = _converter.ConvertToExtensionsAI(input);
            var messages = messageList != null ? messageList.ToList() : new List<ChatMessage>();

            // Convert AGUI tools to ChatOptions (if needed)
            var chatOptions = _converter.ConvertToExtensionsAIChatOptions(
                input,
                existingOptions: null,
                enableFrontendToolScoping: false);

            // ═══════════════════════════════════════════════════════
            // ✅ CHECKPOINT SUPPORT: Create/load conversation thread
            // ═══════════════════════════════════════════════════════

            ConversationThread? conversationThread = null;
            IAsyncEnumerable<AgentEvent> internalStream;

            var config = _core.Config;
            if (config?.ThreadStore != null)
            {
                // Load thread from thread store (may have checkpoint)
                conversationThread = await config.ThreadStore.LoadThreadAsync(
                    input.ThreadId, cancellationToken);

                // Create new thread if not found
                if (conversationThread == null)
                {
                    conversationThread = new ConversationThread();
                }

                // Validate resume semantics
                var hasMessages = messages?.Any() ?? false;
                var hasCheckpoint = conversationThread.ExecutionState != null;

                if (hasCheckpoint && hasMessages)
                {
                    throw new InvalidOperationException(
                        $"Cannot add new messages when resuming mid-execution. " +
                        $"Thread '{input.ThreadId}' is at iteration {conversationThread.ExecutionState?.Iteration ?? 0}.\n\n" +
                        $"To resume execution, send RunAgentInput with empty Messages array.");
                }

                // Call core agent with thread support
                internalStream = _core.RunAsync(
                    messages ?? new List<ChatMessage>(),
                    chatOptions,
                    conversationThread,
                    cancellationToken);
            }
            else
            {
                // No checkpointer - use original code path
                internalStream = _core.RunAsync(
                    messages ?? new List<ChatMessage>(),
                    chatOptions,
                    cancellationToken);
            }

            // Convert internal events to AGUI protocol using EventStreamAdapter
            var aguiStream = EventStreamAdapter.ToAGUI(
                internalStream,
                input.ThreadId,
                input.RunId,
                cancellationToken);

            // Stream AGUI events to the provided channel
            await foreach (var aguiEvent in aguiStream)
            {
                await events.WriteAsync(aguiEvent, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Emit error event on failure
            var errorEvent = EventSerialization.CreateRunError(ex.Message);
            await events.WriteAsync(errorEvent, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Sends a Middleware response to the core agent (for permission handling, etc.)
    /// </summary>
    /// <param name="MiddlewareId">The Middleware ID to respond to</param>
    /// <param name="response">The response event</param>
    public void SendMiddlewareResponse(string MiddlewareId, AgentEvent response)
    {
        _core.SendMiddlewareResponse(MiddlewareId, response);
    }
}

/// <summary>
/// Adapts protocol-agnostic internal agent events to AGUI protocol format.
/// </summary>
internal static class EventStreamAdapter
{
    /// <summary>
    /// Adapts internal events to AGUI protocol format.
    /// Maps internal events to AGUI lifecycle events (Run, Step, Tool, Content).
    /// </summary>
    public static async IAsyncEnumerable<BaseEvent> ToAGUI(
        IAsyncEnumerable<AgentEvent> internalStream,
        string threadId,
        string runId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var internalEvent in internalStream.WithCancellation(cancellationToken))
        {
            BaseEvent? aguiEvent = internalEvent switch
            {
                // MESSAGE TURN → RUN events
                MessageTurnStartedEvent => EventSerialization.CreateRunStarted(threadId, runId),
                MessageTurnFinishedEvent => EventSerialization.CreateRunFinished(threadId, runId),
                MessageTurnErrorEvent e => EventSerialization.CreateRunError(e.Message),

                // AGENT TURN → STEP events
                AgentTurnStartedEvent e => EventSerialization.CreateStepStarted(
                    stepId: $"step_{e.Iteration}",
                    stepName: $"Iteration {e.Iteration}",
                    description: null),
                AgentTurnFinishedEvent e => EventSerialization.CreateStepFinished(
                    stepId: $"step_{e.Iteration}",
                    stepName: $"Iteration {e.Iteration}",
                    result: null),

                // TEXT CONTENT events
                TextMessageStartEvent e => EventSerialization.CreateTextMessageStart(e.MessageId, e.Role),
                TextDeltaEvent e => EventSerialization.CreateTextMessageContent(e.MessageId, e.Text),
                TextMessageEndEvent e => EventSerialization.CreateTextMessageEnd(e.MessageId),

                // REASONING events (consolidated)
                Reasoning e when e.Phase == ReasoningPhase.SessionStart => EventSerialization.CreateReasoningStart(e.MessageId),
                Reasoning e when e.Phase == ReasoningPhase.MessageStart => EventSerialization.CreateReasoningMessageStart(e.MessageId, e.Role ?? "assistant"),
                Reasoning e when e.Phase == ReasoningPhase.Delta => EventSerialization.CreateReasoningMessageContent(e.MessageId, e.Text ?? ""),
                Reasoning e when e.Phase == ReasoningPhase.MessageEnd => EventSerialization.CreateReasoningMessageEnd(e.MessageId),
                Reasoning e when e.Phase == ReasoningPhase.SessionEnd => EventSerialization.CreateReasoningEnd(e.MessageId),

                // TOOL events
                HPD.Agent.ToolCallStartEvent e => EventSerialization.CreateToolCallStart(e.CallId, e.Name, e.MessageId),
                HPD.Agent.ToolCallArgsEvent e => EventSerialization.CreateToolCallArgs(e.CallId, e.ArgsJson),
                HPD.Agent.ToolCallEndEvent e => EventSerialization.CreateToolCallEnd(e.CallId),
                HPD.Agent.ToolCallResultEvent e => EventSerialization.CreateToolCallResult(e.CallId, e.Result),

                // PERMISSION events (Human-in-the-Loop)
                PermissionRequestEvent e => EventSerialization.CreateFunctionPermissionRequest(
                    e.PermissionId,
                    e.FunctionName,
                    e.Description ?? "",
                    e.Arguments?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object?>()),

                ContinuationRequestEvent e => EventSerialization.CreateContinuationPermissionRequest(
                    e.ContinuationId,
                    e.CurrentIteration,
                    e.MaxIterations,
                    Array.Empty<string>(),  // Would need to track completed functions elsewhere
                    ""),  // Would need to track elapsed time elsewhere

                _ => null // Unknown event type
            };

            if (aguiEvent != null)
            {
                yield return aguiEvent;
            }
        }
    }
}
