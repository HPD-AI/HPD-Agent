# Permissions Middleware

Permissions are HPD's call-level authorization gate. A function can be admitted
only after HPD resolves its effective permission binding, evaluates the exact
validated invocation, records the decision, and issues an invocation-bound
grant.

Permissions answer one question:

```text
May this exact function or action execute?
```

They do not sandbox the function body or authorize a filesystem path, network
destination, local port, external tenant, or other resource. Sandbox capability
authorization uses the same request lifecycle but has a separate decision
domain. See [Sandboxing Overview](../sandboxing/overview.md).

## One Gate, One Function Representation

`PermissionMiddleware` is the framework permission middleware. HPD normalizes a
registered function into one `HPDAIFunction` and attaches zero or one permission
binding:

```text
FunctionCallContent
  -> canonical argument validation
  -> action resolution
  -> effective permission binding
  -> policy Allow, Deny, or Ask
  -> invocation-bound grant or terminal result
```

An ordinary `AIFunction` imported into HPD must declare its permission status.
Use `HPDAIFunctionFactory.Import(...)` with either an HPD permission binding or
the explicit ungoverned designation. HPD rejects an imported function whose
permission status is ambiguous. Approval wrappers such as
`ApprovalRequiredAIFunction` and delegating wrapper chains are not admitted.

## Register The Gate

Use `WithPermissions()` for normal applications:

```csharp
using HPD.Agent;
using HPD.Agent.Permissions;
using HPD.Agent.Providers.OpenAI;

var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithPermissions()
    .RequirePermissionFor("run_command")
    .WithTool<ShellTools>("run_command")
    .BuildAsync();
```

`WithPermissions()` constructs `PermissionMiddleware` with the builder's stable
agent identity, permission overrides, and generated policy descriptors.

There are two construction-time authorization modes:

| Mode | Behavior |
| --- | --- |
| `AuthorizationMode.Enforce` | Resolves and enforces the function's permission binding. This is the default. |
| `AuthorizationMode.Bypass` | Issues a host-bypass grant without evaluating the policy, while still journaling one `PermissionResolvedEvent`. |

Bypass is a trusted host construction decision. Do not expose it as a portable
run configuration.

## Declare A Permission

Mark a generated tool function with `[RequiresPermission]`:

```csharp
using HPD.Agent;

public sealed class ShellTools
{
    [AIFunction(Name = "run_command")]
    [AIDescription("Runs a shell command.")]
    [RequiresPermission(PermissionAuthority = "shell/run")]
    public string RunCommand(string command)
        => $"Would run: {command}";
}
```

The declaration contains:

- whether the function requires permission;
- a stable authority such as `shell/run`;
- an optional stable policy descriptor ID.

The source generator emits a portable descriptor package for the selected
policy. HPD resolves declarations by stable ID during AOT-compatible
composition; it does not load policy implementations by CLR type name from
portable configuration.

`RequirePermissionFor(...)`, `DisablePermissionFor(...)`, and
`ClearPermissionOverride(...)` can add a builder override. A run can use
`AgentSecurityRunConfig.PermissionOverrides` for an exact function, action, or
authority selector.

## Default Policy

When no custom policy is declared, HPD uses
`hpd.permission.default.v1`. Its choices are:

| Choice ID | Kind | Persistence |
| --- | --- | --- |
| `allow_once` | Allow | This invocation only |
| `allow_session` | Allow | Session approval, when session persistence is available |
| `deny_once` | Deny | This invocation only |
| `feedback` | Feedback | Return the user's instruction to the model |

Persistent denial is not supported. A denial never revokes a previously stored
approval.

`allow_session` is removed before prompting when HPD has no stable agent
identity or no writable session store. If filtering leaves no Allow choice, the
middleware records a framework denial with
`PermissionPersistenceUnavailable`.

## Custom Policies

Declare a policy implementation with a stable descriptor ID and attach it to a
function:

```csharp
using HPD.Agent.Permissions;

[PermissionPolicyDefinition("acme.reports.write.v1")]
public sealed class ReportWritePolicy : IPermissionPolicy
{
    public ValueTask<PermissionPolicyResult> EvaluateAsync(
        ValidatedPermissionInput input,
        PermissionEvaluationContext context,
        CancellationToken cancellationToken)
    {
        var destination = input.GetRequiredString("path");
        var choices = new[]
        {
            new PermissionChoice(
                "allow_once", "Allow this write", PermissionDecisionKind.Allow),
            new PermissionChoice(
                "allow_session", "Allow report writes this session",
                PermissionDecisionKind.Allow),
            new PermissionChoice(
                "deny_once", "Deny", PermissionDecisionKind.Deny),
        };

        return ValueTask.FromResult(new PermissionPolicyResult
        {
            Outcome = new PermissionAsk(
                $"Write {destination}?",
                PermissionRisk.Medium,
                new PermissionResource(destination, Path.GetFileName(destination)),
                choices),
            PersistenceByChoice = new Dictionary<string, PermissionPersistenceProposal>
            {
                ["allow_session"] = new()
                {
                    Kind = PermissionPersistenceKind.Session,
                },
            },
        });
    }
}
```

```csharp
[RequiresPermission(
    PermissionAuthority = "reports/write",
    PermissionPolicy = typeof(ReportWritePolicy))]
public Task WriteReport(string path, string body)
{
    // Tool body.
}
```

An Ask result is validated before it is shown:

- it contains 2–16 unique choices;
- at least one choice is Allow and one is Deny;
- every choice has a canonical ID and bounded label;
- persistence mappings target Allow choices only;
- evidence and validated rules remain within framework bounds.

`PermissionDeny` can return `InterruptTurn` or `ReturnToModel`. A deny or
feedback choice can use the same behaviors. Only approval records are persisted.

## Function Permission Flow

`PermissionMiddleware` uses this lifecycle:

1. Resolve the binding for the registered function and selected action.
2. Validate the canonical arguments and freeze the invocation authority.
3. Evaluate the policy unless construction bypass is active.
4. Publish `PermissionEvaluatedEvent` for policy outcomes.
5. For Allow, issue an invocation-bound grant.
6. For Deny, return the configured interruption or model-facing denial.
7. For Ask, reuse a matching stored approval or publish one durable
   `PermissionRequestEvent`.
8. Validate the returned `PermissionResponseEvent` against the exact permission
   ID, source, and declared choice set.
9. Publish `PermissionResolvedEvent`, checkpoint any new reusable approval, and
   execute the function only when the final disposition is Allow.

The final PermissionResolvedEvent records a decision source such as
`PolicyDecision`, `StoredDecision`, `HostAutoApprove`, `HostBypass`,
`UserDecision`, `Cancelled`, or `TimedOut`.

## Request And Response

`PermissionRequestEvent` contains the domain-neutral decision contract:

```text
PermissionId
CallId
SourceName
PermissionDomain
Authority
Action
PermissionResource
Reason
Risk
Choices
Evidence
PersistenceUnavailableReason
FunctionName
Arguments
```

The host must return one exact declared choice:

```csharp
new PermissionResponseEvent(
    PermissionId: request.PermissionId,
    SourceName: request.SourceName,
    ChoiceId: selectedChoice.Id,
    Feedback: selectedChoice.Kind == PermissionDecisionKind.Feedback
        ? userInstruction
        : null);
```

For ordinary approve/deny hosts, use the smaller projection:

```csharp
var prompt = request.GetPrompt();
var response = request.Answer(new PermissionAnswer(
    prompt.PermissionId,
    Approved: true));
```

The simple projection requires `allow_once` and `deny_once` in the request.
Custom choice sets must return the full `PermissionResponseEvent`.

```csharp
using var permissionRequests = agent.Subscribe<PermissionRequestEvent>(request =>
{
    var allow = request.Choices.Single(choice => choice.Id == "allow_once");
    return agent.AnswerRequestAsync(new PermissionResponseEvent(
        request.PermissionId,
        request.SourceName,
        allow.Id));
});

var result = await agent.RunAsync("Run the command pwd.");
```

Preserve `PermissionId` and `SourceName`. A response with an unknown choice,
different source, or different permission ID is rejected.

## Reusable Approvals

Reusable approvals are stored in the persistent, session-scoped
`PermissionDecisionStateData`, using the existing middleware-state and session
store APIs.

Two persistence kinds are available:

| Kind | Reuse rule |
| --- | --- |
| `Session` | One agent + authority + action + policy descriptor approval applies to later calls in the session. |
| `ValidatedRule` | The policy revalidates the stored canonical rule against each new input. |

A new session approval for the same agent, authority, and action supersedes the
previous policy-descriptor version. Distinct validated rules can coexist.
Framework limits bound each individual rule and evidence document; the
framework does not impose a fixed count or aggregate byte ceiling on the
decision collection.

After an approval candidate is created, HPD marks the permission state pending.
Before executing the tool body, the runtime saves that state through the
existing session save path. A failed checkpoint prevents body execution and
requires the session to be reloaded before another permission decision can use
the failed state.

## Per-Run Approval Policy

`AgentRunConfig.Security.Approval` controls what happens when an Ask policy is
evaluated:

| Mode | Behavior |
| --- | --- |
| `AgentApprovalPolicy.ReviewProtectedActions` | Uses stored approvals or requests a host decision. |
| `AgentApprovalPolicy.AutoApprove` | Resolves the Ask as `HostAutoApprove` without prompting. |

```csharp
await agent.RunAsync(
    "Run the trusted maintenance task.",
    runConfig: new AgentRunConfig
    {
        Security = new AgentSecurityRunConfig
        {
            Approval = AgentApprovalPolicy.AutoApprove,
        },
    });
```

Autoapproval remains a durable permission event path: the runtime still requires
the thread journal. It does not disable function bindings or bypass
construction-time enforcement.

Approval and sandbox policy are independent. Autoapproving the function call
does not expand filesystem, network, or process capabilities.

## Hosted And In-Process Routing

Hosted runtimes use one response route for all standardized response events:

```http
POST /agents/{agentId}/sessions/{sessionId}/threads/{threadId}/responses
```

Read `PermissionRequestEvent` from the canonical event stream. Post the matching
`PermissionResponseEvent` envelope through the runtime route that surfaced the
request.

For a direct in-process agent, subscribe to the request and answer with
`AnswerRequestAsync(...)`. A late responder can use
`TryAnswerRequestAsync(...)` and inspect the returned status.

The TUI installs `PermissionRequestInteractionHandler` through
`AddAgentTuiDefaults()`. It presents the server-authored choices and returns
their exact IDs. Bot adapters follow the same rule and preserve the request
identity; platform-specific approval UX belongs in that adapter.

## Boundaries

`RequiresPermission` is metadata consumed by the HPD policy resolver. It is not
a firewall, tenant check, credential broker, or sandbox rule.

Resource authorization performed inside a trusted tool remains part of that
tool or host. For HPD's Coding sandbox escalations, the tool uses the shared
permission event lifecycle documented in
[Sandboxing Overview](../sandboxing/overview.md).

The permission declaration, custom policy, request, response, and builder
examples on this page are compile-checked against the published `0.9.5`
packages.
