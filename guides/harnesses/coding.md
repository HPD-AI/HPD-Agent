# Coding Harness


The coding harness gives an agent workspace-aware coding tools. Use it for agents that inspect files, edit files, search a repository, or run commands.

## Register The Harness

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OpenRouter;
using HPD.Agent.Sandbox;
using HPD.Environment.HostProcess;
using HPD.Agent.ToolHarness.Coding;

var agent = await new AgentBuilder()
    .WithOpenRouter(model, apiKey)
    .WithInstructions("Inspect before editing. Stay inside the configured workspace.")
    .WithPermissions()
    .WithSandbox(new HostProcessProviderModule(), HostProcessSandbox.ForDirectory(Path.GetFullPath("sample-workspace")))
    .WithToolHarness<CodingToolHarness>()
    .BuildAsync();
```

The full coding harness requires `WithPermissions()`. Connect your app's
approval handler before running protected tools, or use the default approval
dialog in the [Coding TUI](coding-tui.md).
`WithSandbox(...)` publishes the process and workspace bindings used by
the Environment-backed coding tools.

## Configure The Harness

`CodingToolHarness` uses the common `IHarnessConfig<CodingHarnessConfig>` contract. Its portable settings cover command limits, glob traversal, environment selection, and language-server policy.

```yaml
toolHarnesses:
  - name: CodingToolHarness
    config:
      commands:
        maxActiveBackgroundCommands: 4
        defaultTimeout: "00:02:00"
      glob:
        maxTraversalEntries: 20000
      languageServer:
        enabled: true
```

Command and glob settings are run-overridable. Environment and language-server settings are construction-only unless their contract marks a nested field otherwise.

For one run:

```yaml
harnesses:
  - name: CodingToolHarness
    config:
      commands:
        maxActiveBackgroundCommands: 2
```

See [Configure A Tool Harness](../tools/configure-a-tool-harness.md) and [Configure A Harness Per Run](../tools/configure-a-harness-per-run.md).

## Provide A Workspace

A fixed workspace is configured on the builder. For a workspace picker, pass
a host-owned resolver to `WithSandbox` before building. Select a registered
workspace with `AgentRunConfig.Workspace` when you run the agent.

```csharp
builder.WithSandbox(environment, resolveWorkspace: resolveWorkspace);

var runConfig = new AgentRunConfig
{
    Workspace = new AgentWorkspaceReference("repo-a"),
};

var result = await agent.RunAsync(
    "Find where provider keys are documented.",
    sessionId,
    runConfig: runConfig);
```

The resolver authorizes the selection and returns a `SandboxSessionRequest`.
It can use the same provider factory as a fixed workspace. The coding tools
use the resulting workspace and process bindings.

The serialized run config contains the host's workspace ID.

```json
{
  "workspace": { "id": "repo-a" }
}
```

The host owns the directory paths and access rules. A saved run config can
use the workspace as long as its host can still resolve and authorize the ID.

## Command Execution

Command execution depends on `RuntimeProcessExecutionBinding`. The binding pairs all process authority
that must come from the same selected runtime:

- environment identity and revision;
- `IProcessProvider`;
- optional `IEnvironmentRuntime`;
- the authorized `TargetHandle<ExecutionUnit>`.

`WithSandbox(...)` publishes the bindings for the selected provider.
Command approvals and execution use the same resolved workspace, working
directory, shell, and isolation policy. File and shell tools require their
respective runtime bindings.

Sandbox enforcement is platform-dependent. Use permissions and narrow workspace roots even when sandboxing is enabled.

`ExecuteCommand` uses the common `PermissionMiddleware` gate for the function
call. When a command starts inside the sandbox and the provider reports a
filesystem, network, or local-binding violation, the Coding harness projects
that boundary crossing onto the shared sandbox permission lifecycle. Approval
adds a narrow exception to the retry's process policy.

Command permission, automatic function approval, and process isolation are
independent. Changing `AgentRunConfig.Security.Approval` does not change
`Security.Sandbox`; configure sandbox isolation and capabilities separately.
See [Sandboxing Overview](../sandboxing/overview.md).

## Debugging

Registering `CodingToolHarness` also enables `Debug`. Install an adapter
for the language you want to debug in the selected execution environment,
such as `netcoredbg` for .NET or `debugpy` in an installed Python
environment. Make the adapter command discoverable on that environment's
`PATH`. The harness matches the target to its built-in adapter catalog
and probes for an installed adapter when needed.

The app handles debugger approvals through the same permission flow used
for editing and commands. Use a session store and run with a session ID
so permission decisions have a thread journal. Debugger sessions can continue across turns in
the same session and thread. The operations available for a live session
depend on the adapter's capabilities.

## Search And Ripgrep

`Grep` searches through `RuntimeWorkspaceBinding.Files.SearchTextAsync`. The
Environment backend owns the search process:

- Host Process executes the target-specific bundled `rg` through its process
  lane.
- Host Containers materializes the Linux bundle inside the retained container.
- Apple Virtualization materializes the musl bundle inside the guest.

If the execution target is unsupported or bundle provisioning fails,
Environment uses its managed search fallback. Users do not need to install
`rg`, and the coding harness does not discover or launch `rg` directly.

### Background Commands

`ExecuteCommand` uses one closed request union with four actions:

- `run` starts a command.
- `listBackground` lists commands owned by the current session.
- `readOutput` reads recent output from one background command.
- `stop` stops one background command.

Use `executionMode: "Background"` on the `run` branch for long-running commands such as dev servers, file watchers, or test watchers:

```json
{
  "request": {
    "action": "run",
    "command": "dotnet watch",
    "workingDirectory": "src/App",
    "executionMode": "Background"
  }
}
```

`executionMode` controls the spawned process. It is separate from the framework-level `invocation.mode` control used by functions configured with `AgentInvocationModePolicy.ModelChoice`.

Background command results include:

```xml
<execute_command
  background="true"
  background_handle_id="cmd_..."
  startup_status="launched_not_verified" />
```

The handle id identifies the live command process. Use it for follow-up control operations:

```json
{
  "request": {
    "action": "readOutput",
    "backgroundHandleId": "cmd_...",
    "delayMilliseconds": 1000
  }
}
```

```json
{
  "request": {
    "action": "stop",
    "backgroundHandleId": "cmd_..."
  }
}
```

To recover handles, call:

```json
{
  "request": {
    "action": "listBackground"
  }
}
```

`listBackground` returns active or recently completed background commands for the current session. Background command launch only means the process started; verify server readiness with `readOutput` or another explicit check before reporting that a service is healthy.

The generated binder rejects unknown fields and fields from the wrong action branch before command execution. See [AI Function Parameter Contracts](../tools/ai-function-parameter-contracts.md) for closed-union authoring and validation rules.

Internally, background commands register both a background task and a background handle:

- The background task observes final completion and can trigger notifications.
- The background handle is the live process resource used for list, read, stop, and artifact inspection.
