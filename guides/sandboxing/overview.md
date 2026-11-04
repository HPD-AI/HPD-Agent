# Sandboxing Overview

Sandboxing connects Agent tools to an HPD Environment execution session.
The Agent owns tool behavior and permission policy. HPD Environment owns
placement, process execution, workspace projection, and cleanup.

## Choose An Environment Backend

Select one Environment backend when building the agent.

### Host Process

Host Process runs commands directly on the current macOS or Linux host under
the selected isolation policy.

```csharp
using HPD.Agent;
using HPD.Agent.Sandbox.HostProcess;
using HPD.Agent.ToolHarness.Coding;

var agent = await new AgentBuilder()
    .WithPermissions()
    .WithHostProcessSandbox()
    .WithToolHarness<CodingToolHarness>()
    .BuildAsync();
```

### Host Containers

Host Containers runs coding tools and commands inside one retained sandbox
container. The image and engine socket are application deployment choices.

```csharp
using HPD.Agent.Sandbox.HostContainers;

var agent = await new AgentBuilder()
    .WithHostContainersSandbox(
        new HostContainersSandboxOptions
        {
            Image = "hpd/coding-toolchain@sha256:...",
            EngineSocketPath = "/var/run/docker.sock",
        })
    .WithToolHarness<CodingToolHarness>()
    .BuildAsync();
```

### Apple Virtualization

Apple Virtualization runs commands and filesystem operations inside a guest
through the signed HPD virtualization helper.

```csharp
using HPD.Agent.Sandbox.VirtualMachine;

var agent = await new AgentBuilder()
    .WithAppleVirtualizationSandbox(appleOptions)
    .WithToolHarness<CodingToolHarness>()
    .BuildAsync();
```

## What The Backend Owns

The selected backend creates the Environment session and publishes:

- `RuntimeProcessExecutionBinding` for commands, language servers, search, and
  debug adapters
- `RuntimeWorkspaceBinding` for file tools and logical workspace paths
- process and workspace capability reports
- session cleanup and finalization

Tools consume these bindings. They do not select a container, VM, host path, or
process provider themselves.

## Search And Ripgrep

`Grep` uses Environment workspace search. When the target has a supported
platform and architecture, Environment provisions a verified ripgrep bundle
and executes that absolute binary through the selected process lane.

The bundle is used for:

- macOS arm64 and x64
- Linux x64
- Linux arm64 with the Alpine musl runtime

A missing or unusable bundle does not disable `Grep`. Environment falls back
to its managed search implementation. The fallback preserves search results
but does not provide ripgrep acceleration.

The Agent coding package no longer carries an `rg` wrapper or discovers
workspace search tools on its own.

See [Bundled Workspace Search](ripgrep.md) for target support, fallback
behavior, and package ownership.

## Security Model

Sandboxing is not a substitute for permissions:

- permissions decide whether a tool call is allowed
- Environment isolation decides how a process or filesystem operation is
  constrained
- workspace selection decides which content is visible

Approval and process isolation are independent. Auto-approving a tool does not
expand its filesystem, network, or process capabilities.

## Platform Caveats

Enforcement depends on the selected provider and host platform.

| Platform | Host Process posture |
| --- | --- |
| macOS | Uses Seatbelt through `sandbox-exec` when dependency checks pass. |
| Linux | Uses bubblewrap, namespaces, seccomp, and mediated networking when dependencies pass. |
| Windows | Host Process has no accepted OS-confinement backend and requires explicit degraded admission. |

Host Containers and virtual machines have separate provider capability
reports. Validate the exact limits of the deployment rather than assuming all
backends provide identical isolation.
