# Bidirectional Events

Bidirectional events pause part of a run until a host, UI, policy engine, or user answers a request.

Use them when the agent runtime needs a decision during execution, not just after the run finishes.

## Request And Response

A bidirectional flow uses the standardized request/response event contracts:

- request events inherit from `AgentEvent` and implement `IAgentRequestEvent<TResponse>`
- response events inherit from `AgentEvent` and implement `IAgentResponseEvent`
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

Response routing can also use responder identity, group, and capability metadata inherited through the Agent contracts. Request events may expose response policy, target, and visibility hints for transports and UIs that need to route requests to a specific responder.

The request-specific events carry domain payload. Accepted responses are durable
domain facts. An unanswered durable request is closed by
`AgentRequestTerminatedEvent`, whose terminal kind is `Expired`, `Cancelled`, or
`Abandoned`. Lower-level coordinator lifecycle notifications remain process-local
implementation details.

Permission requests additionally publish one shared
`PermissionResolvedEvent`. It records the authorization domain, selected
choice, decision kind, source, and denial reason. Do not create a parallel
family such as `FooPermissionApprovedEvent` or `FooPermissionResolvedEvent`.

For other request families, do not create `FooApprovedEvent`,
`FooDeniedEvent`, `FooExpiredEvent`, or `FooCancelledEvent`. Put the domain
decision on the response payload and let `AgentRequestTerminatedEvent` describe
why an unanswered request stopped being actionable.

## Direct Subscribe And Respond

In direct in-process code, subscribe before the run starts. When a request arrives, answer it with `agent.AnswerRequestAsync(...)`:

```csharp
using HPD.Agent.Permissions;

using var permissions = agent.Subscribe<PermissionRequestEvent>(
    async Task (request) =>
    {
        var allow = request.Choices.Single(choice => choice.Id == "allow_once");
        var deny = request.Choices.First(choice => choice.Kind == PermissionDecisionKind.Deny);
        var approved = await ui.ConfirmAsync($"Allow {request.FunctionName}?");

        await agent.AnswerRequestAsync(new PermissionResponseEvent(
            PermissionId: request.PermissionId,
            SourceName: request.SourceName,
            ChoiceId: approved ? allow.Id : deny.Id,
            Feedback: approved ? null : "User denied"));
    });

await agent.RunAsync("Clean up temporary files.");
```

Use `TryAnswerRequestAsync(...)` when a response may arrive late or the waiter may already be gone:

```csharp
var result = await agent.TryAnswerRequestAsync(response);

if (!result.Accepted)
    logger.LogDebug("Response was not accepted: {Status}", result.Status);
```

ASP.NET Core hosted clients do not call `agent.Subscribe(...)` or `agent.AnswerRequestAsync(...)` directly. Canonical request events arrive through the hosted `/events` stream and currently answerable requests are also included in `/state`. Their response command is the hosted `/responses` route.

## Ask From Middleware

Middleware can author a full permission request and wait for its typed response:

```csharp
using HPD.Agent.Permissions;

var choices = new[]
{
    new PermissionChoice("allow_once", "Allow once", PermissionDecisionKind.Allow),
    new PermissionChoice("deny_once", "Deny", PermissionDecisionKind.Deny),
};

var response = await context.RequestAsync<PermissionRequestEvent, PermissionResponseEvent>(
    new PermissionRequestEvent(
        PermissionId: Guid.NewGuid().ToString("N"),
        CallId: callId,
        SourceName: "PermissionMiddleware",
        PermissionDomain: PermissionDomains.ToolExecution,
        Authority: authority,
        Action: null,
        PermissionResource: null,
        Reason: description,
        Risk: PermissionRisk.Medium,
        Choices: choices,
        FunctionName: functionName,
        Arguments: arguments),
    timeout: TimeSpan.FromSeconds(30));

if (response.ChoiceId != "allow_once")
    throw new InvalidOperationException(response.Feedback ?? "Permission denied.");
```

`RequestAsync(...)` is available from hook contexts, agent context, and `FunctionExecutionContext`.
`FunctionExecutionContext` requires its invocation cancellation token; hook contexts supply their
runtime lifetime internally.

When no timeout is supplied, the request waits indefinitely while its owning
execution remains valid. Supply an explicit timeout only when expiration is part
of the interaction's actual policy. Execution cancellation still cancels the
request.

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
        cancellationToken,
        timeout: TimeSpan.FromMinutes(2));

    return $"Booking {topic} for {response.Answer}.";
}
```

In direct in-process code, the app handles the request in the same way: subscribe to `ClarificationRequestEvent`, ask the user, and send a `ClarificationResponseEvent` with the same request id. In ASP.NET Core hosted clients, observe or restore the request and return the response through the request's agent/session/thread route.

Responses sent through `agent.AnswerRequestAsync(...)`, `agent.TryAnswerRequestAsync(...)`, or the hosted `/responses` route must be events too. Use response records that inherit from `AgentEvent` and implement `IAgentResponseEvent`, as the built-in response events do.

## Built-In Families

| Family | Request | Response |
| --- | --- | --- |
| Permission | `PermissionRequestEvent` | `PermissionResponseEvent` |
| Continuation | `ContinuationRequestEvent` | `ContinuationResponseEvent` |
| Clarification | `ClarificationRequestEvent` | `ClarificationResponseEvent` |
| Client tools | `ClientToolInvokeRequestEvent` | `ClientToolInvokeOutcomeEvent` |

Client tool outcomes are the immediate domain response to the request. `Completed`, `Failed`, and `Rejected` finish the tool call during the request session. `AcceptedBackground` resolves the request session because the client has accepted ownership of later work; the later work is then tracked by the agent-operation lifecycle.

Built-in permission events are the shared authorization protocol. HPD's
`PermissionMiddleware` uses the `ToolExecution` domain for function calls, and
the Coding sandbox adapter uses `SandboxCapability` for path, network,
local-binding, terminal, and unsandboxed-execution decisions.

Application-owned domains may use the same `PermissionRequestEvent`,
`PermissionResponseEvent`, and `PermissionResolvedEvent` records with another
domain string and an application-owned policy. Keep one request/response shape
across domains so hosts, hosted APIs, bots, and TUIs do not need a second
interaction protocol.

## Author Custom Request Families

Custom bidirectional flows should keep the same two-layer shape:

```text
domain request event  -> what is being asked
domain response event -> what answer was given
terminal fact         -> why an unanswered durable request stopped being actionable
```

Define a request event that inherits from `AgentEvent` and implements
`IAgentRequestEvent<TResponse>`. Define its response event by inheriting from
`AgentEvent` and implementing `IAgentResponseEvent`. The positional `RequestId`
and `SourceName` properties satisfy the inherited protocol directly.

```csharp
public sealed record WorkspaceTrustRequestEvent(
    string RequestId,
    string SourceName,
    string WorkspacePath,
    string Reason) : AgentEvent, IAgentRequestEvent<WorkspaceTrustResponseEvent>
{
    public override EventChannel Channel { get; init; } = EventChannel.Interactive;
}

public sealed record WorkspaceTrustResponseEvent(
    string RequestId,
    string SourceName,
    bool Trusted,
    string? Reason = null) : AgentEvent, IAgentResponseEvent
{
    public override EventChannel Channel { get; init; } = EventChannel.Interactive;
    public override EventDirection Direction { get; init; } = EventDirection.Upstream;
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
        Reason: "The agent may read, edit, and execute files in this folder."),
    cancellationToken);

if (!response.Trusted)
    throw new InvalidOperationException(response.Reason ?? "Workspace was not trusted.");
```

Use response payload fields such as `Trusted`, `Approved`, `ChoiceId`, `Reason`,
or `Result` for the domain decision. `AgentRequestTerminatedEvent` closes an
unanswered request when it expires, is cancelled, or is abandoned. This keeps
every UI, hosted transport, bot adapter, and TUI on one request-session model.

## Timeouts

The default is no elapsed-time deadline. Set an explicit timeout only when the
product policy requires expiration. If it expires, deny by policy, return a
fallback, or surface a clear error.

Do not block inside a direct event handler while holding UI state that the response path also needs. The handler should gather the decision and call `agent.AnswerRequestAsync(...)`. Hosted clients should post or send the response promptly while the thread runtime is still active.

## Hosted Response Route

Hosted runtimes expose one response route for all standardized response events:

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/responses
Content-Type: application/json

{
  "version": "1.0",
  "type": "PERMISSION_RESPONSE",
  "permissionId": "permission-id-from-request",
  "sourceName": "PermissionMiddleware",
  "choiceId": "allow_once",
  "feedback": null
}
```

The body must be a serialized `AgentEvent` envelope whose event implements
`IAgentResponseEvent`. A well-formed attempt returns `200` with an
`AgentRespondResult`, including nonaccepted outcomes such as `AlreadyResolved`,
`TimedOut`, `Cancelled`, `ExecutionEnded`, or `RuntimeUnavailable`. Invalid event
envelopes return `400`, and a missing route scope returns `404`.

The `choiceId` must come from the request's server-authored `choices`. A host
may use `request.GetPrompt()` and `request.Answer(...)` when it needs only
`allow_once`/`deny_once`; hosts that expose custom or turn-scoped choices should
return the exact rich response.

## Related Pages

- [Permissions Middleware](../middleware/permissions.md)
- [Custom Events](custom-events.md)
- [Tool And Function Events](tool-and-function-events.md)
- [Hosted Streaming API](../hosting/hosted-streaming-api.md)
