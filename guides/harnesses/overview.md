# Built-In Harnesses

Built-in harnesses are ready-made tool groups for common agent capabilities.

| Harness | Use it for | Status |
| --- | --- | --- |
| Coding | Workspace-aware read, search, edit, and command workflows | Use with permissions and sandboxing |
| FileSystem | Explicit filesystem operations over a configured root | Use with an explicit `FileSystemContext` |
| Coding TUI | TUI renderers and controls for coding harness events | Compose into the TUI registry |
| WebSearch | Public web search through Tavily | Use with an explicit Tavily key or `Tavily:ApiKey` configuration |

Use built-in harnesses the same way you use your own harnesses: register them on `AgentBuilder`, then configure the runtime context they require.

## Safety Defaults

Filesystem and coding tools can mutate files or run processes. Start with:

- an explicit workspace root
- `WithPermissions()`
- `WithLocalSandbox()` when command execution is available
- shell disabled unless the workflow truly needs it

```csharp
var agent = await new AgentBuilder()
    .WithInstructions("Stay inside the configured workspace.")
    .WithPermissions()
    .WithLocalSandbox()
    .WithToolHarness<CodingToolHarness>()
    .BuildAsync();
```

## Recommended Reading

- [Coding Harness](coding.md)
- [FileSystem Harness](filesystem.md)
- [Coding TUI](coding-tui.md)
- [Web Search Harness](web-search.md)
- [Sandboxing Overview](../sandboxing/overview.md)
- [Permissions](../middleware/permissions.md)
