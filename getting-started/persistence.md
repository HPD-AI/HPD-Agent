# Save Sessions And State

Persistence lets sessions, threads, agent definitions, and content survive process restarts.

## Default Stores

HPD Agent supplies in-memory storage automatically when your app leaves storage at its defaults.

| Store | Default | Holds |
| --- | --- | --- |
| Session | `InMemorySessionStore` | Sessions, threads, and their event history |
| Content | `InMemoryContentStore` | Uploads and generated artifacts |
| Agent | `InMemoryAgentStore` | Reusable agent definitions |

For in-process agents, the builder uses the agent store when it resolves stored definitions or materializes stored subagents. ASP.NET Core hosting supplies all three defaults per hosting profile. In-memory data lasts for the life of the process. Configure durable implementations for the data you want to keep across restarts.

## Add Program.cs

```csharp
using HPD.Agent;
using HPD.Agent.Providers.OpenAI;

var dataRoot = Path.Combine(Directory.GetCurrentDirectory(), ".hpd-data");

var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithInstructions("You are a concise helpful assistant.")
    .WithFileSessionStore(Path.Combine(dataRoot, "sessions"))
    .WithAgentStore(Path.Combine(dataRoot, "agents"))
    .WithContentStore(new LocalFileContentStore(Path.Combine(dataRoot, "content")))
    .BuildAsync();

var sessionId = await agent.CreateSessionAsync("getting-started-persistence");
const string threadId = "main";

var result = await agent.RunAsync(
    "Remember that my workspace name is Northstar.",
    sessionId,
    threadId);

Console.WriteLine(result.Text);
Console.WriteLine($"Saved session: {sessionId}");
```

Run it:

```bash
dotnet run
```

Run it again and inspect the `.hpd-data` folder. The stores contain the durable state created by the run.

```bash
find .hpd-data -maxdepth 3 -type f
```

## You Succeeded If

You should see files under `.hpd-data/sessions`, and possibly `.hpd-data/agents` or `.hpd-data/content` depending on what the run created.

## What Happens

`WithFileSessionStore(path)` creates a segmented `FileSessionStore` from the application's generated event codec.

The session store saves session metadata and canonical thread journals. Messages, thread metadata, run lifecycle, and thread-scoped middleware state are projected from committed events. Explicitly configuring a session store enables canonical commit before hosted or replayable publication.

`CreateSessionAsync(...)` returns the new session ID and creates the default `main` thread.

`WithAgentStore(path)` creates a `JsonAgentStore`.

The agent store saves reusable agent definitions. This matters for hosted agents, stored subagents, and systems where agent definitions can be edited or selected at runtime.

`WithContentStore(...)` configures framework-managed content storage for uploads, artifacts, and internal content references.

`LocalFileContentStore` stores content bytes on disk. With `WithFileSessionStore(path)`, content defaults to a local file store under `path/content`. The explicit content store in this example uses a separate directory.

## Local Store Or Hosted Store

Use the file session store and JSON agent store for local development, samples, tests, and filesystem-oriented deployments.

For production hosting, place the same store responsibilities behind the storage layer you want to operate:

- session and thread metadata
- canonical thread journals, descriptors, indexes, and semantic projections
- session-scoped and thread-scoped middleware state
- agent definitions, if users can create or select agents at runtime
- content bytes, hosted-file references, and metadata needed to resolve attachments

Treat local file stores as a development default unless your deployment model is designed around that filesystem. Production apps should make explicit choices for retention, backup, tenant boundaries, authorization, encryption, and content lifecycle.

`FileSessionStore` uses segmented journals, sparse indexes, lightweight descriptors, and rebuildable projections. See [Canonical Thread Journal](../guides/sessions-and-streaming/canonical-thread-journal.md) for the store contract and physical layout.

## HPD Base Stores

HPD Agent currently provides HPD Base-backed implementations for these storage contracts:

| Data | Contract | Implementation and package | Registration |
| --- | --- | --- | --- |
| Sessions, threads, and canonical event journals | `ISessionStore` | `HpdBaseSessionStore` in `HPD-Agent.Stores.HpdBase` | `AddHpdBaseSessionStore(eventCodec)` |
| Stored agent definitions | `IAgentStore` | `HpdBaseAgentStore` in `HPD-Agent.Stores.HpdBase` | `AddHpdBaseAgentStore()` |
| Evaluation scores and runs | `IScoreStore` | `HpdBaseScoreStore` in `HPD-Agent.Evaluations.HpdBase` | `AddHpdBaseEvaluationStore()` |
| Evaluation datasets | `IDatasetStore` | `HpdBaseDatasetStore` in `HPD-Agent.Evaluations.HpdBase` | `AddHpdBaseDatasetStore(...)` |

The session and agent stores are configured in the [ASP.NET hosting guide](../guides/hosting/aspnet-core.md). Evaluation store setup, including the required dataset input codecs, is in [Datasets and Reports](../guides/evaluations/datasets-and-reports.md). Use the same HPD Base application/provider configuration for the collections required by each package. The session adapter must receive the same `AgentEventCodec` as the host's event composition.

The hosting example currently selects HPD Base's SQLite provider and stores its database in a local `.db` file. HPD Agent's adapters define how sessions, threads, agent definitions, and evaluation records map to HPD Base collections; the selected HPD Base provider supplies the underlying persistence. The SQLite file must live on persistent storage if data should survive redeployments, and should be included in backups. Other HPD Base providers can use the same adapter layer when available.

These adapters do not currently provide an HPD Base implementation for `IContentStore`; content should use the documented file store or an application-provided implementation. See [Content Upload and Resolution](../guides/content/content-upload-and-resolution.md).

## Next

Next: expose the runtime over HTTP in [ASP.NET Hosting](aspnet-hosting.md).

Optional: add behavior around the turn in [Middleware](middleware.md).

Go deeper: see [Sessions, Threads, And Events](../concepts/sessions-threads-and-events.md) and [Content Upload And Resolution](../guides/content/content-upload-and-resolution.md).
