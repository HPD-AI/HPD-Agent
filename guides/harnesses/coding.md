# Coding Harness

The coding harness gives an agent workspace-aware coding tools. Use it for agents that inspect files, edit files, search a repository, or run commands.

## Register The Harness

```csharp
using HPD.Agent;
using HPD.Agent.Sandbox.Local;
using HPD.Agent.ToolHarness.Coding;

var agent = await new AgentBuilder()
    .WithInstructions("Inspect before editing. Stay inside the configured workspace.")
    .WithPermissions()
    .WithLocalSandbox()
    .WithToolHarness<CodingToolHarness>()
    .BuildAsync();
```

`WithPermissions()` gives mutation and command tools an approval path. `WithLocalSandbox()` publishes the atomic process-execution binding used by command execution.

## Provide A Workspace

Pass an `AgentWorkspace` in run context:

```csharp
var workspacePath = Path.GetFullPath("sample-workspace");
Directory.CreateDirectory(workspacePath);

var workspace = new AgentWorkspace(
    "default",
    workspacePath,
    [new AgentWorkspaceRoot("default", workspacePath, "Sample workspace")]);

var runConfig = new AgentRunConfig
{
    Context = new AgentContextRunConfig
    {
        Properties = new Dictionary<string, object>
        {
            [AgentWorkspace.ContextKey] = workspace,
        }
    },
};
```

Then run with that config:

```csharp
var result = await agent.RunAsync(
    "Find where provider keys are documented.",
    runConfig);
```

## Command Execution

Command execution depends on `RuntimeProcessExecutionBinding`. The binding pairs all process authority
that must come from the same selected runtime:

- environment identity and revision;
- `IProcessProvider`;
- optional `IEnvironmentRuntime`;
- the authorized `TargetHandle<ExecutionUnit>`.

`WithLocalSandbox()` publishes a complete local binding. The Coding harness does not look up an
`IProcessProvider` separately and does not manufacture an execution target. If the selected runtime
does not publish the complete binding, command execution fails closed as a setup error.

Sandbox enforcement is platform-dependent. Use permissions and narrow workspace roots even when sandboxing is enabled.

Command approval and process isolation are independent.
`ExecuteCommandPermissionMiddleware` honors `AgentRunConfig.Security.Approval`.
Changing the approval policy does not change `Security.Sandbox`; configure sandbox
isolation and capabilities separately.

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

`executionMode` controls the spawned process. It is separate from the framework-level `invocationMode` control used by functions configured with `AgentInvocationModePolicy.ModelChoice`.

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
