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

var agent = await new AgentBuilder()
    .WithPermissions()
    .WithToolHarness<FileSystemTools>(
        options => options.UseDependency(fileSystemContext),
        fileSystemContext)
    .BuildAsync();
```

Keep `allowOutsideWorkspace: false` for normal application use. Keep `enableShell: false` unless the agent truly needs command execution.

The generated constructor receives `FileSystemContext` through the common dependency resolver. The second argument supplies the same object as typed harness metadata because FileSystem functions use `[AIFunction<FileSystemContext>]` for descriptions and conditions. `UseDependency(...)` keeps it caller-owned and applies it only to this harness registration.

## Explicit Instance

```csharp
var fileSystemTools = new FileSystemTools(fileSystemContext);

builder.WithToolHarness(fileSystemTools);
```

An explicit instance retains its captured context and cannot receive registration-time configuration or dependency overrides.

## Path Behavior

File operations validate paths against the configured workspace. Use absolute paths or paths resolved by your application before passing them into the context.

For coding workflows, prefer the [Coding Harness](coding.md), which layers workspace, permissions, and command execution behavior around common code-editing tasks.
