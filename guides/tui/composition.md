# TUI Composition

`HpdAgentTuiBuilder` composes the terminal shell. Use it to install defaults, render events, add commands and pages, configure shortcuts, and connect app policy to TUI interactions.

## Start With Defaults

```csharp
await using var app = HpdAgentTuiApp.Create(
    runtime,
    scope,
    tui => tui.AddAgentTuiDefaults());
```

`AddAgentTuiDefaults()` installs the default header, prompt status, footer, prompt, shell layout, slash-command autocomplete, help page, `/help`, and `/clear`.

Defaults are shell mechanics. Add your own event handlers for the transcript, tools, cards, product events, or custom state your users need to see.

The sample names below such as `TextMessageStreamHandler`, `ContextWidget`, `ModelStatusItem`, `MyAutocompleteProvider`, and `MyPermissionInteractionHandler` are application-owned examples, not framework-provided types.

## Add, TryAdd, Replace

Most composition APIs come in three forms:

```csharp
tui.AddSlashCommand(command);
tui.TryAddSlashCommand(command);
tui.ReplaceSlashCommand(command);
```

`Add*` is strict and throws on a duplicate key.

`TryAdd*` keeps the existing contribution when the key is already present.

`Replace*` overwrites an existing contribution and throws when that contribution is missing.

This lets libraries safely install defaults with `TryAdd*`, while applications can intentionally replace them.

## Event Handlers

```csharp
tui.AddEventHandler("myapp.text", new TextMessageStreamHandler());
```

Event handlers decide how `AgentEvent` instances affect the terminal shell. A handler can update transcript rows, add status output, track event state, or ignore events that are not relevant to the current surface.

Use stable keys for handlers so your app can replace or suppress behavior deliberately.

## Pages And Commands

```csharp
tui
    .AddPage("myapp.status", context => new Text("Agent status"))
    .AddSlashCommand(new HpdAgentTuiCommandDescriptor(
        "status",
        context => context.Navigation.GoToPage("myapp.status")));
```

Pages render alternate terminal surfaces. Slash commands let users invoke shell behavior without sending text to the agent.

## Prompt Status, Widgets, And Footer Items

The shell separates agent runtime state from application-owned status:

- `PromptStatusText` and the prompt-status component describe the current agent interaction: submitting, running, steering, awaiting permission, cancelling, failure, and context usage.
- Footer components and footer items describe the application or domain: background commands, changed files, diagnostics, external connections, or custom harness state.
- Widgets attach interactive or contextual UI to the explicit `AboveEditor` and `BelowEditor` slots.

The default shell order is:

```text
header
transcript
activity
above-editor widgets
prompt status
prompt
below-editor widgets
footer
```

Customize the prompt-status renderer without taking ownership of the footer:

```csharp
tui.ReplacePromptStatus(context => new Text(context.Shell.PromptStatusText));
```

Register application-owned footer output independently:

```csharp
tui.AddWidget(TuiSlot.BelowEditor, "myapp.context", new ContextWidget());
tui.AddFooterItem("myapp.connection", new ConnectionFooterItem());
```

`IAgentTuiFooterItem.Create(AgentTuiFooterContext)` receives the current scope, shell model, and shared TUI state. `AddFooterItem`, `TryAddFooterItem`, and `ReplaceFooterItem` use stable keys, just like the other keyed composition surfaces.

Keep widgets and footer items app-owned. The TUI framework provides placement and lifecycle; your component decides what the information means. Do not put agent execution state in `FooterText`; write it to `PromptStatusText` or replace the prompt-status component.

## Shortcuts And Autocomplete

```csharp
tui.AddShortcut(new HpdAgentTuiShortcutDescriptor(
    "myapp.backToTranscript",
    new KeyGesture(KeyCode.Escape),
    context => context.Navigation.GoToTranscript()));

tui.AddAutocompleteProvider("myapp.commands", new MyAutocompleteProvider());
```

Shortcuts should map to clear shell actions. Autocomplete providers can suggest slash commands, agent ids, filenames, or app-specific tokens, depending on what your app supports.

## Run Configuration

The prompt submission path can attach an `AgentRunConfig` to the input event:

```csharp
tui.SetRunConfigComposer(context => new AgentRunConfig
{
    Clients = new AgentClientsConfig
    {
        Chat = new ChatClientConfig
        {
            ProviderKey = "openai",
            ModelName = "gpt-5-mini"
        }
    }
});
```

Model selection helpers are composition conveniences. They do not change the underlying rule: each submitted prompt can carry run configuration into the agent runtime.

The composer can also attach a run-level permission profile:

```csharp
tui.SetRunConfigComposer(context => new AgentRunConfig
{
    Security = new AgentSecurityRunConfig
    {
        Approval = AgentApprovalPolicy.ReviewProtectedActions
    }
});
```

The TUI only transports this setting. Permission middleware decides how it is enforced, and sandbox policy remains a separate host concern.

## Transcript Checkpoint Boundaries

Use `TranscriptModel.ReplaceHistoryWith(...)` when an authoritative checkpoint supersedes everything currently visible before it:

```csharp
context.Shell.Transcript.ReplaceHistoryWith(checkpointEntry);
```

The replacement is atomic: it removes finalized and live entries regardless of their cell or event type, inserts the supplied entry as finalized, and advances the transcript history epoch. Use it for real history boundaries such as a compaction checkpoint, not as a general-purpose clear-and-add convenience.

## Interaction Handlers

```csharp
tui.AddInteractionHandler(
    "myapp.permissions",
    new MyPermissionInteractionHandler());
```

Interaction handlers handle request/response workflows such as permissions, continuations, clarifications, and client tools. Keep product policy in your app layer. For example, a development TUI might allow an approval prompt, while a production TUI might deny or restrict the same request.

`AddAgentTuiDefaults()` installs the generic
`PermissionRequestInteractionHandler` under the `hpd.permission` key. It presents
the server-authored choice labels, returns the selected choice ID, maps dialog
cancellation to the request's deny choice, and handles the transient
`allow_for_turn` behavior for sandbox capability requests.

To replace the prompt presentation, use
`ReplaceInteractionHandler("hpd.permission", ...)` rather than registering a
second matching handler under another key. A replacement must preserve exact
`PermissionId`, `SourceName`, and `ChoiceId` values. Policy and reusable approval
state belong to the permission middleware, not the TUI.

## Composition Boundary

Provider pickers, model catalogs, coding harness UI, and product-specific diagnostics can all be layered through composition, but they are not core TUI guarantees. Treat them as app features built on the shell primitives.
