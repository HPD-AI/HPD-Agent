# Coding TUI

The coding harness includes TUI integration for coding-specific events and render states.

## Register Coding TUI Defaults

```csharp
using HPD.Agent.TUI;
using HPD.Agent.ToolHarness.Coding.TUI;

var registry = new HpdAgentTuiBuilder()
    .AddAgentTuiDefaults()
    .AddCodingHarnessTui()
    .Build();
```

`AddCodingHarnessTui()` composes the coding exploration, command, and file-mutation TUI handlers. It also installs coding-domain footer items for exploration, commands, background terminals, command output, changed files, and diagnostics.

You can also register narrower sets:

```csharp
var registry = new HpdAgentTuiBuilder()
    .AddAgentTuiDefaults()
    .AddCodingExplorationTui()
    .AddCodingCommandTui()
    .AddCodingFileMutationTui()
    .Build();
```

Use the full coding harness registration for normal coding-agent terminals. Use the narrower methods when you are building a custom terminal surface and want to own part of the rendering model yourself.

## Shell Placement

Coding Harness state is application-domain output, so it renders in the footer below the prompt. It does not share the prompt-status line used by the agent runtime.

```text
state: running | ctx: 14.7k/200k (7%)       model: provider/model
› Ask HPD...

coding exploration, commands, background work, files, and diagnostics
```

The narrower registration methods contribute only their own keyed footer items:

- `AddCodingExplorationTui()` adds exploration state.
- `AddCodingCommandTui()` adds command, background-terminal, and output state.
- `AddCodingFileMutationTui()` adds file-mutation and diagnostics state.

Applications can replace a particular item through `ReplaceFooterItem(...)` using its stable registration key, or omit the corresponding registration method entirely.
