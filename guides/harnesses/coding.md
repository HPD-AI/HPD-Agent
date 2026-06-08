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
