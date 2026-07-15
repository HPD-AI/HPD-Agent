# Bidirectional Events

Bidirectional events pause part of a run until a host, UI, policy engine, or user answers a request.

Use them when the agent runtime needs a decision during execution, not just after the run finishes.

## Request And Response

A bidirectional flow uses the standardized request/response event contracts:

- request events inherit from `AgentEvent` and implement `IAgentRequestEvent` / `IRequestEvent`
- response events inherit from `AgentEvent` and implement `IAgentResponseEvent` / `IResponseEvent`
- both sides share the same `RequestId` through `IRequestCorrelatedEvent`

The runtime flow has three parts:

```text
middleware or tool
  -> emits request event and waits
host or UI
  -> observes request event
  -> sends matching response event
middleware or tool
  -> continues with the response
```

The request and response are matched by `RequestId`. Built-in permission events map `PermissionId` to `RequestId`; continuation events map `ContinuationId` to `RequestId`; clarification and client-tool events use `RequestId` directly.

The waiter is registered before the request event is emitted. Duplicate request ids, timeouts, and mismatched response types are treated as errors by the coordinator.

Response routing can also use the standard `IResponseEvent` metadata: `ResponderId`, `ResponderGroup`, and `Capabilities`. Request events may expose response policy, target, and visibility hints for transports and UIs that need to route requests to a specific responder.

The request-specific events carry domain payload. Generic request lifecycle events carry coordinator state:

- `AgentRequestStartedEvent` means the coordinator opened a request session
- `AgentRequestResolvedEvent` means a matching response was accepted
- `AgentRequestExpiredEvent` means the request timed out
- `AgentRequestCancelledEvent` means the request was cancelled
- `AgentResponseRejectedEvent` means a response was late, mismatched, unauthorized, or otherwise not accepted

Do not create request-family terminal events such as `FooApprovedEvent`, `FooDeniedEvent`, `FooResolvedEvent`, `FooExpiredEvent`, or `FooCancelledEvent`. Put the domain decision on the response payload, then let the generic lifecycle events describe the request session outcome.

## Direct Subscribe And Respond

In direct in-process code, subscribe before the run starts. When a request arrives, answer it with `agent.AnswerRequestAsync(...)`:

```csharp
using var permissions = agent.Subscribe<PermissionRequestEvent>(async request =>
{
    var approved = await ui.ConfirmAsync(
        $"Allow {request.FunctionName}?");

    await agent.AnswerRequestAsync(new PermissionResponseEvent(
        PermissionId: request.PermissionId,
        SourceName: request.SourceName,
        Approved: approved,
        Reason: approved ? null : "User denied"));
});

await agent.RunAsync("Clean up temporary files.");
```

Use `TryAnswerRequestAsync(...)` when a response may arrive late or the waiter may already be gone:

```csharp
var delivered = await agent.TryAnswerRequestAsync(response);

if (!delivered)
    logger.LogDebug("Response arrived after the request was no longer waiting.");
```

ASP.NET Core hosted clients do not call `agent.Subscribe(...)` or `agent.AnswerRequestAsync(...)` directly. Their response command is the hosted `/responses` route. However, the current committed SSE endpoint cannot deliver built-in request records that still use the default non-persistent event policy. Treat hosted bidirectional interaction as incomplete until request delivery is committed and replayable; direct in-process subscriptions continue to work.

## Ask From Middleware

Middleware can emit a request and wait for a typed response:

```csharp
var response = await context.RequestAsync<PermissionRequestEvent, PermissionResponseEvent>(
    new PermissionRequestEvent(
        PermissionId: Guid.NewGuid().ToString("N"),
        SourceName: "PermissionMiddleware",
        FunctionName: functionName,
        Description: description,
        CallId: callId,
        Arguments: arguments),
    timeout: TimeSpan.FromSeconds(30));

if (!response.Approved)
    throw new InvalidOperationException(response.Reason);
```

`RequestAsync(...)` is available from hook contexts, agent context, and `FunctionExecutionContext`.

The default timeout is five minutes when no timeout is supplied. `FunctionExecutionContext.RequestAsync(...)` exposes timeout, but not a separate cancellation token parameter.

## Ask From A Tool

Tools can ask the host for more information by accepting `FunctionExecutionContext`:

```csharp
public async Task<string> BookMeeting(
    string topic,
    FunctionExecutionContext context,
    CancellationToken cancellationToken)
{
    var requestId = Guid.NewGuid().ToString("N");

    var response = await context.RequestAsync<ClarificationRequestEvent, ClarificationResponseEvent>(
        new ClarificationRequestEvent(
            RequestId: requestId,
            SourceName: context.FunctionName,
            Question: "Which day should I book?"),
        timeout: TimeSpan.FromMinutes(2));

    return $"Booking {topic} for {response.Answer}.";
}
```

In direct in-process code, the app handles the request in the same way: subscribe to `ClarificationRequestEvent`, ask the user, and send a `ClarificationResponseEvent` with the same request id. In ASP.NET Core hosted clients, observe the request from the hosted event stream and return the response through the hosted response path for the same `agentId + sessionId + threadId`.

Responses sent through `agent.AnswerRequestAsync(...)`, `agent.TryAnswerRequestAsync(...)`, or the hosted `/responses` route must be events too. In practice, use response records that inherit from `AgentEvent` and implement `IAgentResponseEvent` / `IResponseEvent`, as the built-in response events do.

## Built-In Families

| Family | Request | Response |
| --- | --- | --- |
| Permission | `PermissionRequestEvent` | `PermissionResponseEvent` |
| Continuation | `ContinuationRequestEvent` | `ContinuationResponseEvent` |
| Clarification | `ClarificationRequestEvent` | `ClarificationResponseEvent` |
| Client tools | `ClientToolInvokeRequestEvent` | `ClientToolInvokeOutcomeEvent` |

Client tool outcomes are the immediate domain response to the request. `Completed`, `Failed`, and `Rejected` finish the tool call during the request session. `AcceptedBackground` resolves the request session because the client has accepted ownership of later work; the later work is then tracked by the background task lifecycle.

Built-in permission events are one permission protocol, not the whole permission architecture. `PermissionMiddleware` uses them for function-level approvals keyed by function name. Apps that need command, path, network, tenant, or workspace-scoped permission grants can implement custom `IAgentPermissionMiddleware` and use custom bidirectional events with their own state model.

## Author Custom Request Families

Custom bidirectional flows should keep the same two-layer shape:

```text
domain request event  -> what is being asked
domain response event -> what answer was given
generic lifecycle     -> whether the request session started, resolved, expired, cancelled, or rejected a response
```

Define a request event that inherits from `AgentEvent` and implements `IAgentRequestEvent`. Define a response event that inherits from `AgentEvent` and implements `IAgentResponseEvent`. Both events must expose the same request id through `IRequestCorrelatedEvent.RequestId`.

```csharp
public sealed record WorkspaceTrustRequestEvent(
    string RequestId,
    string SourceName,
    string WorkspacePath,
    string Reason) : AgentEvent, IAgentRequestEvent
{
    public override EventChannel Channel { get; init; } = EventChannel.Interactive;
    string IRequestCorrelatedEvent.RequestId => RequestId;
}

public sealed record WorkspaceTrustResponseEvent(
    string RequestId,
    string SourceName,
    bool Trusted,
    string? Reason = null) : AgentEvent, IAgentResponseEvent
{
    public override EventChannel Channel { get; init; } = EventChannel.Interactive;
    public override EventDirection Direction { get; init; } = EventDirection.Upstream;
    string IRequestCorrelatedEvent.RequestId => RequestId;
}
```

Then wait for the typed response:

```csharp
var requestId = Guid.NewGuid().ToString("N");

var response = await context.RequestAsync<WorkspaceTrustRequestEvent, WorkspaceTrustResponseEvent>(
    new WorkspaceTrustRequestEvent(
        RequestId: requestId,
        SourceName: "WorkspaceTrustMiddleware",
        WorkspacePath: workspacePath,
        Reason: "The agent may read, edit, and execute files in this folder."));

if (!response.Trusted)
    throw new InvalidOperationException(response.Reason ?? "Workspace was not trusted.");
```

Use response payload fields such as `Trusted`, `Approved`, `ChoiceId`, `Reason`, or `Result` for the domain decision. Use `AgentRequestResolvedEvent`, `AgentRequestExpiredEvent`, `AgentRequestCancelledEvent`, and `AgentResponseRejectedEvent` for lifecycle. This keeps every UI, hosted transport, bot adapter, and TUI on one request-session model.

## Timeouts

Set an explicit timeout when waiting on user or host input. If a timeout expires, handle it like any other runtime failure: deny by policy, return a fallback, or surface a clear error.

Do not block inside a direct event handler while holding UI state that the response path also needs. The handler should gather the decision and call `agent.AnswerRequestAsync(...)`. Hosted clients should post or send the response promptly while the thread runtime is still active.

## Hosted Response Route

The route below is the response half of the hosted protocol. The current committed-stream limitation described above means its existence does not by itself make built-in hosted interactive prompts end-to-end usable.

Hosted runtimes expose one response route for all standardized response events:

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/responses
Content-Type: application/json

{
  "version": "1.0",
  "type": "PERMISSION_RESPONSE",
  "permissionId": "permission-id-from-request",
  "sourceName": "PermissionMiddleware",
  "approved": true
}
```

The body must be a serialized `AgentEvent` envelope whose event implements `IResponseEvent`. The route returns `404` when the session/thread scope does not exist, `409` when no active runtime or pending waiter accepts the response, and `200` with a `RespondResult` when the response is accepted.

## Related Pages

- [Permissions Middleware](../middleware/permissions.md)
- [Custom Events](custom-events.md)
- [Tool And Function Events](tool-and-function-events.md)
- [Hosted Streaming API](../hosting/hosted-streaming-api.md)
