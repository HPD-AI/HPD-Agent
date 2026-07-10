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

`WithPermissions()` gives mutation and command tools an approval path. `WithLocalSandbox()` publishes the process/runtime capabilities used by command execution.

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
    ContextOverrides = new Dictionary<string, object>
    {
        [AgentWorkspace.ContextKey] = workspace,
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

Command execution depends on an `IProcessProvider`. `WithLocalSandbox()` provides one for local runs. Without a process provider, command execution should be treated as a setup error.

Sandbox enforcement is platform-dependent. Use permissions and narrow workspace roots even when sandboxing is enabled.

Command approval and process isolation are independent. `ExecuteCommandPermissionMiddleware` honors `AgentRunConfig.PermissionMode`: `Ask` applies its command-specific approval analysis, while `FullAccess` skips those approval requests for that run. Changing permission mode does not change `ExecuteCommandSandboxPolicy`; configure sandbox isolation separately through run context.

### Background Commands

Use `invocationMode: "background"` for long-running commands such as dev servers, file watchers, or test watchers. The tool returns after the process launches instead of waiting for the command to finish.

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
  "action": "ReadOutput",
  "backgroundHandleId": "cmd_...",
  "delayMilliseconds": 1000
}
```

```json
{
  "action": "Stop",
  "backgroundHandleId": "cmd_..."
}
```

`ListBackground` returns active or recently completed background commands for the current session. Background command launch only means the process started; verify server readiness with `ReadOutput` or another explicit check before reporting that a service is healthy.

Internally, background commands register both a background task and a background handle:

- The background task observes final completion and can trigger notifications.
- The background handle is the live process resource used for list, read, stop, and artifact inspection.
