# FileSystem Harness

The FileSystem harness exposes explicit filesystem tools over a configured root. Prefer an explicit context so the agent cannot accidentally use the process current directory as its workspace.

## Configure A Root

```csharp
using HPD.Agent;
using HPD.Agent.ToolHarness.FileSystem;

var workspacePath = Path.GetFullPath("sample-workspace");
Directory.CreateDirectory(workspacePath);

var fileSystemContext = new FileSystemContext(
    workspaceRoot: workspacePath,
    allowOutsideWorkspace: false,
    enableSearch: true,
    enableShell: false);

var fileSystemTools = new FileSystemTools(fileSystemContext);

var agent = await new AgentBuilder()
    .WithPermissions()
    .WithToolHarness(fileSystemTools, fileSystemContext)
    .BuildAsync();
```

Keep `allowOutsideWorkspace: false` for normal application use. Keep `enableShell: false` unless the agent truly needs command execution.

## Path Behavior

File operations validate paths against the configured workspace. Use absolute paths or paths resolved by your application before passing them into the context.

For coding workflows, prefer the [Coding Harness](coding.md), which layers workspace, permissions, and command execution behavior around common code-editing tasks.
