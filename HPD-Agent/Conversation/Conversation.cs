using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using Microsoft.Agents.AI;

/// <summary>
/// Stateless conversation orchestrator built on Microsoft.Extensions.AI.
/// Coordinates Agent execution with external ConversationThread state.
/// Multiple threads can use the same Conversation instance safely.
/// Implements AIAgent to be compatible with Microsoft.Agents.AI workflows.
///
/// This is a stateless orchestrator following Microsoft's design pattern where
/// agents are reusable tools and threads (state) are managed externally.
/// For a stateful convenience wrapper that manages its own thread, use ConversationSession.
/// </summary>
/// <remarks>
/// Design Philosophy:
/// - Agent = Stateless execution engine (can be singleton)
/// - Conversation = Stateless orchestrator (can be singleton)
/// - ConversationThread = State container (managed by caller)
///
/// This pattern enables:
/// - Thread-safe singleton agents and conversations
/// - Multi-agent workflows with shared threads
/// - Scalable web server architectures
/// - Explicit state management
/// </remarks>
public class Conversation : AIAgent
{
    private readonly Agent _agent;

    // OpenTelemetry Activity Source for conversation telemetry
    private static readonly ActivitySource ActivitySource = new("HPD.Conversation");

    /// <summary>Gets the agent in this conversation.</summary>
    public Agent Agent => _agent;

    /// <summary>
    /// Extracts and merges chat options from agent run options with conversation context.
    /// Since we're using only the abstractions library, we create new ChatOptions and inject conversation context.
    /// </summary>
    private ChatOptions ExtractAndMergeChatOptions(
        AgentRunOptions? workflowOptions,
        Dictionary<string, object>? conversationContext = null)
    {
        // Create new ChatOptions since we don't have access to ChatClientAgentRunOptions
        var chatOptions = new ChatOptions();

        // Inject conversation context into AdditionalProperties
        if (conversationContext != null)
        {
            chatOptions.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            foreach (var kvp in conversationContext)
            {
                chatOptions.AdditionalProperties[kvp.Key] = kvp.Value;
            }
        }

        return chatOptions;
    }

    #region AIAgent Implementation

    /// <summary>
    /// Gets the name of this conversation (derived from agent).
    /// </summary>
    public override string? Name => _agent?.Config?.Name ?? "Conversation";

    /// <summary>
    /// Gets the description of this conversation (derived from agent).
    /// </summary>
    public override string? Description => _agent?.Config?.SystemInstructions;

    /// <summary>
    /// Creates a new thread compatible with this conversation.
    /// </summary>
    /// <returns>A new ConversationThread instance</returns>
    public override AgentThread GetNewThread()
    {
        return new ConversationThread();
    }

    /// <summary>
    /// Creates a new thread within a project context.
    /// The project reference is stored in the thread's metadata.
    /// </summary>
    /// <param name="project">The project to associate with this thread</param>
    /// <returns>A new ConversationThread with project metadata</returns>
    public ConversationThread GetNewThread(Project project)
    {
        var thread = new ConversationThread();
        thread.AddMetadata("Project", project);
        return thread;
    }

    /// <summary>
    /// Deserializes a thread from its JSON representation.
    /// </summary>
    public override AgentThread DeserializeThread(JsonElement serializedThread, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        // Deserialize the JsonElement to ConversationThreadSnapshot using source-generated context
        var snapshot = serializedThread.Deserialize(ConversationJsonContext.Default.ConversationThreadSnapshot);
        if (snapshot == null)
        {
            throw new InvalidOperationException("Failed to deserialize ConversationThreadSnapshot from JsonElement");
        }

        return ConversationThread.Deserialize(snapshot);
    }

    /// <summary>
    /// Service discovery - chains through agent
    /// </summary>
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        return base.GetService(serviceType, serviceKey)
            ?? ((IChatClient)_agent).GetService(serviceType, serviceKey);
    }

    /// <summary>
    /// Runs the agent with messages (non-streaming, AIAgent interface).
    /// Thread must be provided for state management.
    /// </summary>
    /// <param name="messages">Messages to process</param>
    /// <param name="thread">Thread for conversation state (creates temporary if null)</param>
    /// <param name="options">Optional run options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent run response</returns>
    /// <remarks>
    /// If thread is null, a temporary thread is created for this call only.
    /// To maintain conversation history, provide an explicit thread.
    /// </remarks>
    public override async Task<AgentRunResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Create temporary thread if not provided (stateless behavior)
        thread ??= GetNewThread();

        if (thread is not ConversationThread conversationThread)
        {
            throw new InvalidOperationException(
                "The provided thread is not compatible with Conversation. " +
                "Only ConversationThread instances can be used.");
        }

        var targetThread = conversationThread;

        // Add workflow messages to thread state
        foreach (var msg in messages)
        {
            if (!targetThread.Messages.Contains(msg))
            {
                await targetThread.AddMessageAsync(msg, cancellationToken);
            }
        }

        // Extract workflow options and inject conversation context
        var conversationContextDict = BuildConversationContext(targetThread);
        var chatOptions = ExtractAndMergeChatOptions(options, conversationContextDict);

        // Create ConversationExecutionContext for AsyncLocal context
        var executionContext = new ConversationExecutionContext(targetThread.Id)
        {
            AgentName = _agent.Name
        };

        // Set AsyncLocal context for plugins (e.g., PlanMode) to access
        ConversationContext.Set(executionContext);

        IReadOnlyList<ChatMessage> finalHistory;
        try
        {
            // Create RunAgentInput from current thread state
            var aguiInput = new RunAgentInput
            {
                ThreadId = targetThread.Id,
                RunId = Guid.NewGuid().ToString(),
                State = JsonDocument.Parse("{}").RootElement,
                Messages = ConvertThreadToAGUIMessages(targetThread.Messages),
                Tools = Array.Empty<Tool>(),
                Context = Array.Empty<Context>(),
                ForwardedProps = JsonDocument.Parse("{}").RootElement
            };

            // DIRECT CALL to Agent.ExecuteStreamingTurnAsync with RunAgentInput
            var streamResult = await _agent.ExecuteStreamingTurnAsync(aguiInput, cancellationToken);

            // Consume stream (non-streaming path)
            await foreach (var _ in streamResult.EventStream.WithCancellation(cancellationToken))
            {
                // Just consume events
            }

            // Get final history and apply reduction
            finalHistory = await streamResult.FinalHistory;
            var reductionMetadata = await streamResult.ReductionTask;

            if (reductionMetadata != null)
            {
                if (reductionMetadata.SummaryMessage != null)
                {
                    await targetThread.ApplyReductionAsync(reductionMetadata.SummaryMessage, reductionMetadata.MessagesRemovedCount, cancellationToken);
                }
            }
        }
        finally
        {
            // Clear context to prevent leaks
            ConversationContext.Clear();
        }

        // Build response from final history
        var response = new ChatResponse(finalHistory.ToList());

        // Update thread with response messages
        foreach (var msg in response.Messages)
        {
            if (!targetThread.Messages.Contains(msg))
            {
                await targetThread.AddMessageAsync(msg, cancellationToken);
            }
        }

        // Notify thread of new messages
        await NotifyThreadOfNewMessagesAsync(thread, response.Messages, cancellationToken);

        return new AgentRunResponse(response)
        {
            AgentId = targetThread.Id,
            ResponseId = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            Usage = CreateUsageFromResponse(response)
        };
    }

    /// <summary>
    /// Runs the agent with messages (streaming, AIAgent interface).
    /// Thread must be provided for state management.
    /// </summary>
    /// <param name="messages">Messages to process</param>
    /// <param name="thread">Thread for conversation state (creates temporary if null)</param>
    /// <param name="options">Optional run options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Streaming updates</returns>
    /// <remarks>
    /// If thread is null, a temporary thread is created for this call only.
    /// To maintain conversation history, provide an explicit thread.
    /// </remarks>
    public override async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Create temporary thread if not provided (stateless behavior)
        thread ??= GetNewThread();

        if (thread is not ConversationThread conversationThread)
        {
            throw new InvalidOperationException(
                "The provided thread is not compatible with Conversation. " +
                "Only ConversationThread instances can be used.");
        }

        var targetThread = conversationThread;

        // Add workflow messages to thread state
        foreach (var msg in messages)
        {
            if (!targetThread.Messages.Contains(msg))
            {
                await targetThread.AddMessageAsync(msg, cancellationToken);
            }
        }

        // Extract workflow options and inject conversation context
        var conversationContextDict = BuildConversationContext(targetThread);
        var chatOptions = ExtractAndMergeChatOptions(options, conversationContextDict);

        // Create ConversationExecutionContext for AsyncLocal context
        var executionContext = new ConversationExecutionContext(targetThread.Id)
        {
            AgentName = _agent.Name
        };

        // Set AsyncLocal context for plugins (e.g., PlanMode) to access
        ConversationContext.Set(executionContext);

        IReadOnlyList<ChatMessage> finalHistory;
        try
        {
            // Create RunAgentInput from current thread state
            var aguiInput = new RunAgentInput
            {
                ThreadId = targetThread.Id,
                RunId = Guid.NewGuid().ToString(),
                State = JsonDocument.Parse("{}").RootElement,
                Messages = ConvertThreadToAGUIMessages(targetThread.Messages),
                Tools = Array.Empty<Tool>(),
                Context = Array.Empty<Context>(),
                ForwardedProps = JsonDocument.Parse("{}").RootElement
            };

            // DIRECT CALL to Agent.ExecuteStreamingTurnAsync with RunAgentInput
            var streamResult = await _agent.ExecuteStreamingTurnAsync(aguiInput, cancellationToken);

            // Convert BaseEvent stream to AgentRunResponseUpdate stream
            await foreach (var evt in streamResult.EventStream.WithCancellation(cancellationToken))
            {
                var update = ConvertBaseEventToAgentRunResponseUpdate(evt);
                if (update != null)
                {
                    yield return update;
                }
            }

            // Update thread with final history
            finalHistory = await streamResult.FinalHistory;
            var reductionMetadata = await streamResult.ReductionTask;

            if (reductionMetadata != null)
            {
                if (reductionMetadata.SummaryMessage != null)
                {
                    await targetThread.ApplyReductionAsync(reductionMetadata.SummaryMessage, reductionMetadata.MessagesRemovedCount, cancellationToken);
                }
            }
        }
        finally
        {
            // Clear context to prevent leaks
            ConversationContext.Clear();
        }

        foreach (var msg in finalHistory)
        {
            if (!targetThread.Messages.Contains(msg))
            {
                await targetThread.AddMessageAsync(msg, cancellationToken);
            }
        }

        // Notify thread of new messages
        await NotifyThreadOfNewMessagesAsync(thread, finalHistory, cancellationToken);
    }

    /// <summary>
    /// Converts BaseEvent to AgentRunResponseUpdate with comprehensive AIContent mapping.
    /// Maps AG-UI protocol events to Microsoft.Extensions.AI content types:
    /// - TextMessageContentEvent â†’ TextContent (assistant response)
    /// - ReasoningMessageContentEvent â†’ TextReasoningContent (model thinking)
    /// - ToolCallStartEvent â†’ FunctionCallContent (tool invocation)
    /// - ToolCallResultEvent â†’ FunctionResultContent (tool result)
    /// - RunErrorEvent â†’ ErrorContent (non-fatal errors)
    /// Metadata events (boundaries, steps, etc.) are filtered out as they don't represent message content.
    /// </summary>
    private AgentRunResponseUpdate? ConvertBaseEventToAgentRunResponseUpdate(BaseEvent evt)
    {
        return evt switch
        {
            // âœ… Text Content - Assistant's actual response
            TextMessageContentEvent textEvent => new AgentRunResponseUpdate
            {
                AgentId = this.Id,
                AuthorName = this.Name,
                Role = ChatRole.Assistant,
                Contents = [new TextContent(textEvent.Delta)],
                CreatedAt = DateTimeOffset.UtcNow,
                MessageId = textEvent.MessageId
            },

            // âœ… Reasoning Content - Model's thinking/reasoning process
            ReasoningMessageContentEvent reasoningEvent => new AgentRunResponseUpdate
            {
                AgentId = this.Id,
                AuthorName = this.Name,
                Role = ChatRole.Assistant,
                Contents = [new TextReasoningContent(reasoningEvent.Delta)],
                CreatedAt = DateTimeOffset.UtcNow,
                MessageId = reasoningEvent.MessageId
            },

            // âœ… Function Call - Tool invocation
            ToolCallStartEvent toolStart => new AgentRunResponseUpdate
            {
                AgentId = this.Id,
                AuthorName = this.Name,
                Role = ChatRole.Assistant,
                Contents = [new FunctionCallContent(toolStart.ToolCallId, toolStart.ToolCallName)],
                CreatedAt = DateTimeOffset.UtcNow,
                MessageId = toolStart.ParentMessageId
            },

            // âœ… Function Result - Tool execution result
            ToolCallResultEvent toolResult => new AgentRunResponseUpdate
            {
                AgentId = this.Id,
                AuthorName = this.Name,
                Role = ChatRole.Tool,
                Contents = [new FunctionResultContent(toolResult.ToolCallId, toolResult.Result)],
                CreatedAt = DateTimeOffset.UtcNow,
                MessageId = Guid.NewGuid().ToString("N")
            },

            // âœ… Error Content - Non-fatal errors during execution
            RunErrorEvent errorEvent => new AgentRunResponseUpdate
            {
                AgentId = this.Id,
                AuthorName = this.Name,
                Role = ChatRole.Assistant,
                Contents = [new ErrorContent(errorEvent.Message)],
                CreatedAt = DateTimeOffset.UtcNow,
                MessageId = Guid.NewGuid().ToString("N")
            },

            // âŒ Filtered Events (Metadata/Boundaries - not message content):
            // - ReasoningStartEvent/EndEvent - reasoning boundaries
            // - TextMessageStartEvent/EndEvent - message boundaries
            // - ReasoningMessageStartEvent/EndEvent - reasoning message boundaries
            // - ToolCallEndEvent/ToolCallArgsEvent - tool call boundaries/streaming
            // - StepStartedEvent/StepFinishedEvent - agent iteration metadata
            // - RunStartedEvent/RunFinishedEvent - run boundaries
            // - StateSnapshotEvent/StateDeltaEvent - state management
            // - CustomEvent/RawEvent - custom protocol extensions
            // - TextMessageChunkEvent/ToolCallChunkEvent - alternate chunking (use delta events instead)
            _ => null
        };
    }

    /// <summary>
    /// Builds conversation context for injection into ChatOptions.
    /// Includes ConversationId and Project if available.
    /// </summary>
    private Dictionary<string, object> BuildConversationContext(ConversationThread thread)
    {
        var context = new Dictionary<string, object>
        {
            ["ConversationId"] = thread.Id
        };

        // Inject project if available in metadata
        if (thread.Metadata.TryGetValue("Project", out var projectObj) && projectObj is Project project)
        {
            context["Project"] = project;
        }

        return context;
    }

    /// <summary>
    /// Creates UsageDetails from ChatResponse.Usage for AgentRunResponse.
    /// </summary>
    private static UsageDetails? CreateUsageFromResponse(ChatResponse response)
    {
        if (response.Usage == null)
            return null;

        return new UsageDetails
        {
            InputTokenCount = response.Usage.InputTokenCount,
            OutputTokenCount = response.Usage.OutputTokenCount,
            TotalTokenCount = response.Usage.TotalTokenCount
        };
    }

    #endregion

    /// <summary>
    /// Creates a stateless conversation orchestrator.
    /// Thread must be provided to RunAsync calls for state management.
    /// </summary>
    /// <param name="agent">The agent to use for conversation execution</param>
    /// <remarks>
    /// This constructor creates a stateless conversation instance that can be safely
    /// reused across multiple threads and is thread-safe for concurrent use.
    ///
    /// For stateful convenience (automatic thread management), use ConversationSession instead.
    /// </remarks>
    public Conversation(Agent agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    /// <summary>
    /// Runs the agent with AGUI protocol input (non-streaming).
    /// This method is for direct AGUI protocol integration (e.g., frontend communication).
    /// For standard AIAgent workflows, use RunAsync(messages, thread) instead.
    /// </summary>
    /// <param name="aguiInput">AGUI protocol input containing thread, messages, tools, and context</param>
    /// <param name="thread">Conversation thread for state management</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent run response</returns>
    /// <remarks>
    /// This method is stateless - thread must be provided explicitly.
    /// It converts AGUI protocol to internal format and updates the provided thread.
    /// </remarks>
    public async Task<AgentRunResponse> RunAsync(
        RunAgentInput aguiInput,
        ConversationThread thread,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("conversation.turn");
        var startTime = DateTimeOffset.UtcNow;

        activity?.SetTag("conversation.id", thread.Id);
        activity?.SetTag("conversation.input_format", "agui");
        activity?.SetTag("conversation.thread_id", aguiInput.ThreadId);
        activity?.SetTag("conversation.run_id", aguiInput.RunId);

        try
        {
            // Add the new user message from aguiInput to conversation thread
            var newUserMessage = aguiInput.Messages.LastOrDefault(m => m.Role == "user");
            if (newUserMessage != null)
            {
                await thread.AddMessageAsync(new ChatMessage(ChatRole.User, newUserMessage.Content ?? ""), cancellationToken);
            }

            // Create new RunAgentInput using server-side thread as source of truth
            var serverSideInput = new RunAgentInput
            {
                ThreadId = aguiInput.ThreadId,
                RunId = aguiInput.RunId,
                State = aguiInput.State,
                Messages = ConvertThreadToAGUIMessages(thread.Messages),
                Tools = aguiInput.Tools,
                Context = aguiInput.Context,
                ForwardedProps = aguiInput.ForwardedProps
            };

            // Use agent's AGUI overload with server-side messages
            var streamResult = await _agent.ExecuteStreamingTurnAsync(serverSideInput, cancellationToken);

            // Consume stream (non-streaming path)
            await foreach (var evt in streamResult.EventStream.WithCancellation(cancellationToken))
            {
                // Events consumed but not exposed in non-streaming path
            }

            // Wait for final history and check for reduction
            var finalHistory = await streamResult.FinalHistory;
            var reductionMetadata = await streamResult.ReductionTask;

            // Apply reduction BEFORE adding new messages
            if (reductionMetadata != null)
            {
                if (reductionMetadata.SummaryMessage != null)
                {
                    await thread.ApplyReductionAsync(reductionMetadata.SummaryMessage, reductionMetadata.MessagesRemovedCount, cancellationToken);
                }
            }

            // Build response from final history
            var response = new ChatResponse(finalHistory.ToList());

            // Update conversation thread
            foreach (var msg in response.Messages)
            {
                if (!thread.Messages.Contains(msg))
                {
                    await thread.AddMessageAsync(msg, cancellationToken);
                }
            }

            var duration = DateTimeOffset.UtcNow - startTime;
            activity?.SetTag("conversation.duration_ms", duration.TotalMilliseconds);
            activity?.SetTag("conversation.success", true);

            return new AgentRunResponse(response)
            {
                AgentId = thread.Id,
                ResponseId = aguiInput.RunId,
                CreatedAt = DateTimeOffset.UtcNow,
                Usage = CreateUsageFromResponse(response)
            };
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            activity?.SetTag("conversation.duration_ms", duration.TotalMilliseconds);
            activity?.SetTag("conversation.success", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Runs the agent with AGUI protocol input (streaming).
    /// Returns full BaseEvent stream for AGUI frontend compatibility.
    /// Includes ALL events: content, reasoning, tool calls, steps, boundaries, state.
    /// This method is for direct AGUI protocol integration (e.g., frontend communication).
    /// For standard AIAgent workflows, use RunStreamingAsync(messages, thread) instead.
    /// </summary>
    /// <param name="aguiInput">AGUI protocol input containing thread, messages, tools, and context</param>
    /// <param name="thread">Conversation thread for state management</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Full stream of BaseEvent (AGUI protocol events)</returns>
    /// <remarks>
    /// This method is stateless - thread must be provided explicitly.
    /// It converts AGUI protocol to internal format and updates the provided thread.
    /// </remarks>
    public async IAsyncEnumerable<BaseEvent> RunStreamingAsync(
        RunAgentInput aguiInput,
        ConversationThread thread,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("conversation.turn");
        var startTime = DateTimeOffset.UtcNow;

        activity?.SetTag("conversation.id", thread.Id);
        activity?.SetTag("conversation.input_format", "agui");
        activity?.SetTag("conversation.thread_id", aguiInput.ThreadId);
        activity?.SetTag("conversation.run_id", aguiInput.RunId);

        // Add the new user message from aguiInput to conversation thread
        var newUserMessage = aguiInput.Messages.LastOrDefault(m => m.Role == "user");
        if (newUserMessage != null)
        {
            await thread.AddMessageAsync(new ChatMessage(ChatRole.User, newUserMessage.Content ?? ""), cancellationToken);
        }

        // Create new RunAgentInput using server-side thread as source of truth
        var serverSideInput = new RunAgentInput
        {
            ThreadId = aguiInput.ThreadId,
            RunId = aguiInput.RunId,
            State = aguiInput.State,
            Messages = ConvertThreadToAGUIMessages(thread.Messages),
            Tools = aguiInput.Tools,
            Context = aguiInput.Context,
            ForwardedProps = aguiInput.ForwardedProps
        };

        // Use agent's AGUI overload with server-side messages
        var streamResult = await _agent.ExecuteStreamingTurnAsync(serverSideInput, cancellationToken);

        // Stream ALL BaseEvent events (no filtering for AGUI protocol)
        await foreach (var evt in streamResult.EventStream.WithCancellation(cancellationToken))
        {
            yield return evt;
        }

        // Wait for final history and check for reduction
        var finalHistory = await streamResult.FinalHistory;
        var reductionMetadata = await streamResult.ReductionTask;

        // Apply reduction BEFORE adding new messages
        if (reductionMetadata != null && reductionMetadata.SummaryMessage != null)
        {
            await thread.ApplyReductionAsync(reductionMetadata.SummaryMessage, reductionMetadata.MessagesRemovedCount, cancellationToken);
        }

        // Update conversation thread
        foreach (var msg in finalHistory)
        {
            if (!thread.Messages.Contains(msg))
            {
                await thread.AddMessageAsync(msg, cancellationToken);
            }
        }

        var duration = DateTimeOffset.UtcNow - startTime;
        activity?.SetTag("conversation.duration_ms", duration.TotalMilliseconds);
        activity?.SetTag("conversation.success", true);
    }

    // TODO: Potential convenience methods to add later (if needed):
    // - public async Task<AgentRunResponse> RunAsync(string message, ChatOptions? options = null)
    //   Simple string overload that creates ChatMessage internally
    // - public async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(string message, ChatOptions? options = null)
    //   Streaming string overload
    // - Extension methods to extract common data from AgentRunResponse:
    //   * response.GetText() - Extract text content
    //   * response.GetDuration() - Calculate duration from timestamps
    //   * response.GetAgent() - Resolve agent from AgentId
    //
    // For now, consumers can use:
    //   await conversation.RunAsync([new ChatMessage(ChatRole.User, "message")])
    //   or call via RunAsync(aguiInput) for AGUI protocol

    #region Conversation Helpers

    /// <summary>
    /// Converts Extensions.AI ChatMessage collection to AGUI BaseMessage collection.
    /// This ensures server-side conversation thread is the source of truth for AG-UI protocol.
    /// Filters out tool-related messages since AG-UI handles tools via events, not message history.
    /// </summary>
    private static IReadOnlyList<BaseMessage> ConvertThreadToAGUIMessages(IEnumerable<ChatMessage> messages)
    {
        return messages
            .Where(m => !HasToolContent(m)) // Skip messages with tool calls/results
            .Select(AGUIEventConverter.ConvertChatMessageToBaseMessage)
            .ToList();
    }

    /// <summary>
    /// Checks if a message contains tool-related content (FunctionCallContent or FunctionResultContent).
    /// These messages should be excluded from AG-UI message history as tools are handled via events.
    /// </summary>
    private static bool HasToolContent(ChatMessage message)
    {
        return message.Contents.Any(c => c is FunctionCallContent or FunctionResultContent);
    }

    #endregion
}