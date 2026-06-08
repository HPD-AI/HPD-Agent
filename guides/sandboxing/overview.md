# Sandboxing Overview

Sandboxing gives tools a runtime for process execution and environment capabilities. It should be used with permissions and narrow workspaces, especially for coding agents.

## Enable Local Sandbox Capabilities

```csharp
using HPD.Agent;
using HPD.Agent.Sandbox.Local;

var agent = await new AgentBuilder()
    .WithPermissions()
    .WithLocalSandbox()
    .BuildAsync();
```

`WithLocalSandbox()` publishes local environment services such as `IEnvironmentRuntime` and `IProcessProvider` into agent runtime capabilities. Built-in tools such as coding command execution use those services.

Local process execution and OS-level isolation are related but separate. A local process provider can execute trusted commands with isolation disabled. Isolation-capable execution requires the sandbox planner and platform applicator path for the current operating system.

## Security Model

Sandboxing is not a substitute for permissions. Use both:

- permissions decide whether a tool call is allowed
- sandbox/runtime services decide how a process or environment operation is executed
- workspace configuration decides which files a tool should see

## Platform Caveats

Local isolation depends on the operating system and backend.

| Platform | Local isolation posture |
| --- | --- |
| macOS | Source uses `sandbox-exec` / Seatbelt profiles when the dependency is available. Validate the exact blocked filesystem, network, and socket behavior your deployment requires. |
| Linux | Source uses bubblewrap-oriented planning and backend execution when dependencies are available. Validate the exact mount, namespace, network, and socket behavior your deployment requires. |
| Windows | Local OS-level isolation is unsupported and fails closed by default unless degraded execution is explicitly allowed. |

Document your own deployment's isolation guarantees. Do not assume every local process policy is enforced identically across platforms.
