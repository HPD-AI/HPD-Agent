# Bidirectional Events

Bidirectional events pause part of a run until a host, UI, policy engine, or user answers a request.

Use them when the agent runtime needs a decision during execution, not just after the run finishes.

## Request And Response

A bidirectional flow has three parts:

```text
middleware or tool
  -> emits request event and waits
host or UI
  -> observes request event
  -> sends matching response event
middleware or tool
  -> continues with the response
```

The request and response are matched by `RequestId`. Built-in permission events use `PermissionId`; continuation events use `ContinuationId`; clarification events use `RequestId`.

The waiter is registered before the request event is emitted. Duplicate request ids, timeouts, and mismatched response types are treated as errors by the coordinator.

## Direct Subscribe And Respond

In direct in-process code, subscribe before the run starts. When a request arrives, respond with `agent.RespondAsync(...)`:

```csharp
using var permissions = agent.Subscribe<PermissionRequestEvent>(async request =>
{
    var approved = await ui.ConfirmAsync(
        $"Allow {request.FunctionName}?");

    await agent.RespondAsync(new PermissionResponseEvent(
        PermissionId: request.PermissionId,
        SourceName: request.SourceName,
        Approved: approved,
        Reason: approved ? null : "User denied"));
});

await agent.RunAsync("Clean up temporary files.");
```

Use `TryRespondAsync(...)` when a response may arrive late or the waiter may already be gone:

```csharp
var delivered = await agent.TryRespondAsync(response);

if (!delivered)
    logger.LogDebug("Response arrived after the request was no longer waiting.");
```

ASP.NET Core hosted clients do not call `agent.Subscribe(...)` or `agent.RespondAsync(...)` directly. They observe request events over SSE or WebSocket and send matching response event envelopes through WebSocket or the hosted `/responses` route.

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

In direct in-process code, the app handles the request in the same way: subscribe to `ClarificationRequestEvent`, ask the user, and send a `ClarificationResponseEvent` with the same request id. In ASP.NET Core hosted clients, observe the request from the hosted event stream and return the response through the hosted response path for the same `agentId + sessionId + branchId`.

Responses sent through `agent.RespondAsync(...)` or `agent.TryRespondAsync(...)` must be events too. In practice, use response records that inherit from `AgentEvent` and implement `IBidirectionalEvent`, as the built-in response events do.

## Built-In Families

| Family | Request | Response |
| --- | --- | --- |
| Permission | `PermissionRequestEvent` | `PermissionResponseEvent` |
| Continuation | `ContinuationRequestEvent` | `ContinuationResponseEvent` |
| Clarification | `ClarificationRequestEvent` | `ClarificationResponseEvent` |
| Client tools | client-tool request events | matching client-tool response events |

Permission approved/denied events are observability events emitted after a decision. They are not the response the waiter consumes.

Built-in permission events are one permission protocol, not the whole permission architecture. `PermissionMiddleware` uses them for function-level approvals keyed by function name. Apps that need command, path, network, tenant, or workspace-scoped permission grants can implement custom `IAgentPermissionMiddleware` and use custom bidirectional events with their own state model.

## Timeouts

Set an explicit timeout when waiting on user or host input. If a timeout expires, handle it like any other runtime failure: deny by policy, return a fallback, or surface a clear error.

Do not block inside a direct event handler while holding UI state that the response path also needs. The handler should gather the decision and call `RespondAsync(...)`. Hosted clients should post or send the response promptly while the branch runtime is still active.

## Related Pages

- [Permissions Middleware](../middleware/permissions.md)
- [Custom Events](custom-events.md)
- [Tool And Function Events](tool-and-function-events.md)
- [Hosted Streaming API](../hosting/hosted-streaming-api.md)
