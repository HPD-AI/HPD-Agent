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

`AddCodingHarnessTui()` composes the coding exploration, command, and file-mutation TUI handlers.

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
