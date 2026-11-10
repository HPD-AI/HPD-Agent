# Sandboxing Overview

Sandboxing connects Agent tools to an HPD Environment execution boundary. The
Agent runtime owns tool calls and the shared permission interaction. HPD
Environment owns process placement, workspace projection, provider enforcement,
and cleanup.

Sandbox capability authorization answers a different question from function
permission:

```text
Function permission:
  May this function call execute?

Sandbox capability permission:
  Given that the function executes, may it cross this path, network,
  local-binding, terminal, or unsandboxed-execution boundary?
```

The two decisions use the same request/response event contract, but they have
separate authorities, policies, grants, and decision keys. Autoapproving a
function does not approve a sandbox escape. Approving a sandbox capability does
not authorize a different function call.

## Current Implementation Status

The shared event normalization is implemented:

- the duplicate capability request/response event pair has been removed;
- Coding filesystem and command escalations use
  `PermissionRequestEvent`, `PermissionResponseEvent`, and
  `PermissionResolvedEvent`;
- the generic TUI handles sandbox requests through the normal permission
  interaction;
- turn approvals remain bounded process-local state in the generic TUI handler.

The dedicated core `SandboxCapabilityGate`, transient sandbox grant middleware,
post-approval ceiling revalidation, and full provider-wide capability routing
remain future work. Current behavior is enforced by the Coding harness and the
selected Environment provider.

## Authorization Flow

```text
tool call
  -> function permission
  -> tool body
  -> sandbox baseline or boundary check
       inside baseline: continue
       outside baseline: PermissionRequestEvent
  -> host selects an exact PermissionChoice
  -> PermissionResponseEvent
  -> PermissionResolvedEvent
       allow: continue or retry with a narrow exception
       deny: return the sandbox denial
```

Sandbox requests use:

```text
PermissionDomain = PermissionDomains.SandboxCapability
Authority        = sandbox/{capability}
PermissionResource = the requested path, host, port, or bounded resource
Reason           = why the boundary was crossed
Risk             = capability risk
FunctionName     = the function that caused the request
Arguments        = the safe public arguments needed for the decision
Choices          = the framework-supplied sandbox choices
```

The current stable authorities are:

| Capability | Authority |
| --- | --- |
| Filesystem read | `sandbox/filesystem-read` |
| Filesystem write | `sandbox/filesystem-write` |
| Network egress | `sandbox/network-egress` |
| Local binding | `sandbox/local-binding` |
| Interactive terminal | `sandbox/interactive-terminal` |
| Unsandboxed execution | `sandbox/unsandboxed-execution` |

Current Coding producers exercise filesystem read/write and command violations
for filesystem, network, and local binding. The remaining capability authorities
are part of the shared vocabulary and policy-amendment surface.

## Choices

Sandbox requests normally offer:

| Choice ID | Behavior |
| --- | --- |
| `allow_once` | Allow this boundary crossing only. |
| `allow_for_turn` | Answer matching sandbox requests in the same thread execution. |
| `deny_once` | Deny this crossing. |

`allow_for_turn` is omitted when the request has no thread-execution identity.
The generic TUI stores it by:

```text
agent + session + thread + thread execution + permission domain + capability family
```

Filesystem-read and filesystem-write requests share one `filesystem` family for
turn approval. The cache is bounded to 64 grants with a 24-hour safety expiry.
It is cleared when the TUI process ends and does not survive a restart.

The turn approval is UI state in the current implementation. The host still
answers with the exact `allow_for_turn` choice ID, and every response produces a
terminal `PermissionResolvedEvent`.

## Shared Permission Contract

A host renders `PermissionRequestEvent` and returns:

```csharp
using HPD.Agent.Permissions;

new PermissionResponseEvent(
    request.PermissionId,
    request.SourceName,
    ChoiceId: selectedChoice.Id);
```

The response is validated against the request's permission ID, source, and
declared choice set. A canceled dialog resolves to the request's deny choice.
Request cancellation and timeout produce deny resolutions with
`PermissionGrantSource.Cancelled` or `PermissionGrantSource.TimedOut`.

The terminal event contains:

```text
PermissionId
CallId
AgentId
Domain
Authority
Action
PolicyDescriptorId
PermissionResource
DecisionKind
ChoiceId
DecisionSource
DenialReason
```

The sandbox adapter currently records the stable policy descriptor
`hpd.sandbox.capability.v1`.

## Filesystem Escalation

The Coding harness canonicalizes the requested path, checks explicitly read-only
workspace roots, and compares the path with the run's baseline sandbox grants.

```text
inside baseline:
  continue without a sandbox prompt

outside baseline with Ask:
  emit a shared sandbox permission request

outside baseline with Deny:
  reject before prompting

capability ceiling present:
  reject before prompting
```

After approval, the file operation proceeds for that call. Current in-process
file escalation does not mutate a durable provider policy.

## Command Escalation

Commands are initially executed with the existing process-isolation policy. If
the provider reports a recognizable violation, the Coding harness classifies the
capability, emits the shared permission request, adds a narrow exception to that
invocation's process policy, and retries once.

Examples:

- a denied local port adds `AllowLocalBinding`;
- denied filesystem read/write adds one resource rule;
- denied network access switches the invocation to the approved network mode.

The retry receives a new command ID and cannot recursively request another
sandbox retry. Denial leaves the original failed sandboxed result in place.

## Environment Backends

### Host Process

Host Process runs commands on the current macOS or Linux host under the selected
isolation policy.

```csharp
using HPD.Agent;
using HPD.Agent.Sandbox.HostProcess;
using HPD.Agent.ToolHarness.Coding;

var agent = await new AgentBuilder()
    .WithPermissions()
    .WithHostProcessSandbox()
    .WithToolHarness<CodingToolHarness>()
    .BuildAsync();
```

### Host Containers

Host Containers runs coding tools and commands inside one retained sandbox
container.

```csharp
using HPD.Agent.Sandbox.HostContainers;

var agent = await new AgentBuilder()
    .WithPermissions()
    .WithHostContainersSandbox(
        new HostContainersSandboxOptions
        {
            Image = "hpd/coding-toolchain@sha256:...",
            EngineSocketPath = "/var/run/docker.sock",
        })
    .WithToolHarness<CodingToolHarness>()
    .BuildAsync();
```

### Apple Virtualization

Apple Virtualization runs commands and filesystem operations through a guest
process and signed virtualization helper.

```csharp
using HPD.Agent.Sandbox.VirtualMachine;

var agent = await new AgentBuilder()
    .WithPermissions()
    .WithAppleVirtualizationSandbox(appleOptions)
    .WithToolHarness<CodingToolHarness>()
    .BuildAsync();
```

## What The Backend Owns

The selected Environment backend publishes:

- `RuntimeProcessExecutionBinding` for commands, language servers, search, and
  debug adapters;
- `RuntimeWorkspaceBinding` for file tools and logical workspace paths;
- process and workspace capability reports;
- session cleanup and finalization.

Tools consume those bindings. They do not select a container, VM, host path, or
process provider themselves.

## Search And Ripgrep

`Grep` uses Environment workspace search. Environment provisions a verified
ripgrep bundle when the selected target supports it and executes that absolute
binary through the process lane. Supported targets are macOS arm64/x64, Linux
x64, and Linux arm64 with the Alpine musl runtime.

A missing bundle does not disable `Grep`; Environment falls back to managed
workspace search without ripgrep acceleration. See
[Bundled Workspace Search](ripgrep.md).

## Security Profiles

Hosts commonly combine three independent controls:

| Profile | Function approval | Sandbox | Sandbox escape |
| --- | --- | --- | --- |
| Approval required | Review protected actions | Enforced | Ask |
| Sandboxed autonomy | Autoapprove function calls | Enforced | Ask |
| Full access | Autoapprove function calls | Disabled | Deny or not applicable |

These names are host UX labels. Core uses the independent
`Security.Approval`, `Security.Sandbox`, capability ceiling, and escape policy
settings.

## Platform Caveats

| Platform | Host Process posture |
| --- | --- |
| macOS | Uses Seatbelt through `sandbox-exec` when dependency checks pass. |
| Linux | Uses bubblewrap, namespaces, and seccomp, with provider-mediated networking when dependencies pass. |
| Windows | No accepted OS-confined Host Process backend; explicit degraded admission is required. |

Host Containers and virtual machines publish separate capability reports.
Validate the exact limits of the deployment rather than assuming every backend
provides identical isolation.

See [Host Process Isolation](host-process-isolation.md) for provider details,
and [Permissions Middleware](../middleware/permissions.md) for the function
permission gate.
