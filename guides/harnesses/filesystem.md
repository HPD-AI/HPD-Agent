# FileSystem Harness

The `HPD-Agent.ToolHarness.FileSystem` package is deprecated in `0.9.5`.
`FileSystemTools` and `FileSystemContext` compile with `CS0618`, do not receive new
features, and are not a supported workspace-isolation or command-execution security
boundary.

Use the [Coding Harness](coding.md) for trusted coding workspaces and an
Environment-backed sandbox for untrusted files, commands, or generated code. This page
documents the legacy API only for migration.

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

Keep `allowOutsideWorkspace: false` and `enableShell: false` during migration, but do not
treat either setting as a security boundary.

The generated constructor receives `FileSystemContext` through the common dependency resolver. The second argument supplies the same object as typed harness metadata because FileSystem functions use `[AIFunction<FileSystemContext>]` for descriptions and conditions. `UseDependency(...)` keeps it caller-owned and applies it only to this harness registration.

## Explicit Instance

```csharp
var fileSystemTools = new FileSystemTools(fileSystemContext);

builder.WithToolHarness(fileSystemTools);
```

An explicit instance retains its captured context and cannot receive registration-time configuration or dependency overrides.

## Path Behavior

File operations validate paths against the configured workspace. Use absolute paths or paths resolved by your application before passing them into the context.

Migrate coding workflows to the [Coding Harness](coding.md), which uses Environment
workspace and process bindings instead of the legacy path-prefix checks.
