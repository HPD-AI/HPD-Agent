# Local Process Isolation

Local process execution flows through environment runtime services.

```csharp
using HPD.Environment.Contracts;
using HPD.Environment.Runtime;

var registry = new EnvironmentProviderRegistry()
    .RegisterLocalProcessProvider();

var runtime = new InMemoryEnvironmentRuntime(registry);
```

Tools call the runtime through an `IProcessProvider` or `IEnvironmentRuntime`. `WithLocalSandbox()` wires those services into an agent run.

Registering a local process provider is enough for local process execution. It does not, by itself, prove OS-level isolation. Isolation-capable execution depends on the sandbox planner/applicator path for the host platform.

## Disabled Isolation Sample

For basic runtime wiring, a process can run with disabled isolation:

```csharp
var result = await runtime.RunProcessAsync(new ProcessInvocationSpec
{
    Target = unit,
    Command = new ProcessCommandSpec
    {
        FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
        Arguments = OperatingSystem.IsWindows()
            ? ["/c", "echo hello"]
            : ["-c", "printf '%s\\n' hello"],
    },
    Isolation = ProcessIsolationPolicy.Default with
    {
        Mode = ProcessIsolationMode.Disabled,
    },
});
```

Use disabled isolation only for wiring tests or trusted local automation. This sample proves process-runtime wiring, not isolation enforcement. For agent-facing command execution, combine local sandboxing with permissions and workspace limits.

## Enforcement Boundaries

Filesystem, network, and socket enforcement are platform/backend concerns. Validate the exact backend used by your deployment before promising hard isolation guarantees to users.

| Platform | Boundary |
| --- | --- |
| macOS | Uses Seatbelt profiles through `sandbox-exec` when available. Test the exact filesystem, network, socket, and Mach lookup behavior you rely on. |
| Linux | Uses bubblewrap-oriented planning and execution when available. Test the exact bind mounts, namespace behavior, network policy, and socket behavior you rely on. |
| Windows | Local OS-level isolation is unsupported and fails closed by default unless degraded execution is explicitly allowed. |
