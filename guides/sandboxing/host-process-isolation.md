# Host Process Isolation

The Host Process Environment backend executes commands on the current machine
and publishes the process and workspace bindings consumed by Agent tools.

```csharp
using HPD.Agent;
using HPD.Agent.Sandbox.HostProcess;
using HPD.Agent.ToolHarness.Coding;
using HPD.Environment.Contracts;

var agent = await new AgentBuilder()
    .WithPermissions()
    .WithHostProcessSandbox(new HostProcessSandboxOptions
    {
        Workspaces =
        [
            new HostProcessWorkspaceOptions
            {
                RootId = "workspace",
                Path = workspacePath,
                Access = AccessMode.ReadWrite,
            },
        ],
    })
    .WithToolHarness<CodingToolHarness>()
    .BuildAsync();
```

The Host Process adapter does not contain the operating-system sandbox
implementation. It composes `HPD-Environment.HostProcess`, creates a real host
and execution unit, and publishes one `RuntimeProcessExecutionBinding`.

Low-level Environment hosts may use `IProcessProvider` or
`IEnvironmentRuntime` directly when they already own an authorized execution
target. Agent-facing tools use the published binding instead.

## Isolation

Host Process supports two related but separate concerns:

1. Process placement through the Host Process Environment provider.
2. Operating-system process isolation described by `ProcessIsolationPolicy`.

Placement can succeed while a requested isolation capability is unavailable.
That case follows the provider's capability and degradation policy. Do not
infer enforcement merely because a process ran through Host Process.

## Platform Boundaries

| Platform | Effective implementation |
| --- | --- |
| macOS | Seatbelt profiles through `sandbox-exec` when dependency checks pass. |
| Linux | Bubblewrap, namespaces, seccomp, and provider-mediated networking when dependencies pass. |
| Windows | No accepted OS-confined Host Process backend; explicit degraded admission is required. |

Use permissions and narrowly selected workspace roots in addition to the
Environment isolation policy.
