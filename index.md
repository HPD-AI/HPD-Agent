---
layout: home

hero:
  name: HPD Agent
  text: Agent runtime infrastructure for .NET.
  tagline: Tools, sessions, branches, events, middleware, providers, audio, bots, and hosted runtimes as explicit system surfaces.
  image:
    src: /logo.svg
    alt: HPD Agent
  actions:
    - theme: brand
      text: Start Building
      link: /getting-started/
    - theme: alt
      text: Read The Concepts
      link: /concepts/agent-runtime-and-capabilities

features:
  - icon:
      src: /icons/zap.svg
    title: Start Small
    details: Begin with one builder and one run, then add streaming, tools, sessions, persistence, and hosting without changing the architecture.
    link: /getting-started/
    linkText: Follow the path

  - icon:
      src: /icons/git-branch.svg
    title: Durable State
    details: Keep session history, fork alternate branches, compact context, and preserve the runtime state a real agent app depends on.
    link: /concepts/sessions-branches-and-events
    linkText: Understand state

  - icon:
      src: /icons/radio.svg
    title: Event Native
    details: Stream text, tool calls, permissions, workflow traces, audio, custom events, and bidirectional host decisions through one event model.
    link: /guides/events/overview
    linkText: Explore events

  - icon:
      src: /icons/wrench.svg
    title: Composable Surfaces
    details: Register tools, harnesses, middleware, providers, subagents, bot adapters, and hosted clients with source-generation-friendly APIs.
    link: /guides/tools/author-a-tool-harness
    linkText: Add capabilities
---

<section class="hpd-home-lanes">
  <a class="hpd-lane hpd-lane-primary" href="/getting-started/">
    <span class="hpd-lane-kicker">First 30 minutes</span>
    <strong>Build one agent that streams, calls a tool, remembers context, and can be hosted.</strong>
    <span>Follow the shortest working path.</span>
  </a>
  <a class="hpd-lane" href="/guides/events/overview">
    <span class="hpd-lane-kicker">Runtime visibility</span>
    <strong>Render live turns, tool calls, permissions, audio, and workflows from the event stream.</strong>
    <span>Make the agent observable from day one.</span>
  </a>
  <a class="hpd-lane" href="/guides/middleware/overview">
    <span class="hpd-lane-kicker">Control plane</span>
    <strong>Add retrieval, policy, state, usage tracking, and custom behavior around each turn.</strong>
    <span>Shape the runtime without hiding it.</span>
  </a>
</section>

<section class="hpd-runtime-panel">
  <div>
    <p class="hpd-eyebrow">The core loop stays readable</p>
    <h2>One builder surface. Real production escape hatches.</h2>
    <p>
      HPD Agent keeps the beginner path small, then opens the runtime surfaces that usually get bolted on later:
      sessions, branches, event streams, middleware, tool harnesses, hosted APIs, and provider-specific clients.
    </p>
  </div>

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI(model: "gpt-5-mini")
    .WithInstructions("You are a concise product assistant.")
    .WithTool(new WeatherTools())
    .BuildAsync();

using var stream = agent.Subscribe<TextDeltaEvent>(e =>
    Console.Write(e.Delta));

var (sessionId, branchId) = await agent.CreateSessionAsync("demo");
await agent.RunAsync("What should I pack for Seattle?", sessionId, branchId);
```
</section>

# HPD Agent Framework

HPD Agent Framework is a .NET framework for building agents that run through a narrow core loop:

1. Configure an `AgentBuilder`.
2. Add a chat provider.
3. Add instructions, tools, or middleware as needed.
4. Call `BuildAsync()`.
5. Run the agent with `RunAsync(...)`.

Start with [Getting Started](getting-started/index.md), then follow the primary path:

- [What Is An Agent?](getting-started/what-is-an-agent.md): understand the loop before writing code.
- [Hello Agent](getting-started/hello-agent.md): build one agent and print one response.
- [Streaming Events](getting-started/streaming-events.md): print assistant text as it arrives.
- [Add A Tool](getting-started/add-a-tool.md): expose one C# method as a model-callable function.
- [Multi-Turn Sessions](getting-started/multi-turn-sessions.md): keep conversation history across turns.
- [Tiny Console Chat Loop](getting-started/chat-loop.md): turn the pieces into a usable local assistant.
- [Save Sessions And State](getting-started/persistence.md): save sessions, branches, content, and agent definitions.
- [ASP.NET Hosting](getting-started/aspnet-hosting.md): expose an agent runtime over HTTP.

Optional early detours:

- [Tool Harnesses](getting-started/tool-harness.md): register a small group of related tool functions.
- [Branching](getting-started/branching.md): fork one session into alternate paths.
- [Middleware](getting-started/middleware.md): add behavior around agent turns.
- [Build A Multi-Agent Workflow](getting-started/agent-workflow.md): connect agents into an explicit graph after you understand one agent.

## Core Concepts

The first-reader model is intentionally small:

- [Agent Runtime And Capabilities](concepts/agent-runtime-and-capabilities.md): the runtime loop, capability map, extension points, state scopes, and trust boundaries.
- [Agent builder and agent](concepts/agent-builder-and-agent.md): `AgentBuilder` collects configuration; `Agent` runs turns.
- Providers and clients: provider packages create chat clients and resolve credentials. See [Providers, Clients, And Secrets](concepts/providers-clients-and-secrets.md).
- Tools: C# methods marked with function metadata can be registered for the model to call. See [Tools, Functions, And Harnesses](concepts/tools-functions-and-harnesses.md).
- Sessions, branches, and events: runs can emit events and can be attached to session/branch state when you need history. See [Sessions, Branches, And Events](concepts/sessions-branches-and-events.md).
- Event streams and hierarchies: events arrive linearly, and clients project them into transcripts, timelines, trees, prompts, and traces. See [Event Streams And Hierarchies](concepts/event-streams-and-hierarchies.md).
- Middleware: middleware can wrap lifecycle steps, add behavior, and persist middleware state. See [Middleware Lifecycle](concepts/middleware-lifecycle.md).

## What To Read First

Read the getting-started pages in order. They move from one local agent to the first hosted runtime without requiring Azure-specific choices, MCP, OpenAPI, built-in harnesses, or advanced configuration. Those topics belong after the first agent path is clear.

## Guides

You can ignore most guides at first. Build the local agent path above, then use these sections when you need a specific capability.

Common setup:

- [Provider Setup Overview](guides/providers/overview.md)
- [Tools, Functions, And Harnesses](concepts/tools-functions-and-harnesses.md)
- [Events Overview](guides/events/overview.md)
- [Middleware Overview](guides/middleware/overview.md)
- [ASP.NET Core Hosting](guides/hosting/aspnet-core.md)
- [Logging And Telemetry](guides/observability/logging-and-telemetry.md)

Provider details:

- [ONNX Runtime](guides/providers/onnx-runtime.md)
- [ONNX Structured Tool Calling](guides/providers/onnx-structured-tool-calling.md)
- [OpenAI Audio](guides/providers/openai-audio.md)
- [ElevenLabs Audio](guides/providers/elevenlabs-audio.md)
- [Provider Families](reference/provider-families.md)
- [Provider Keys And Environment Variables](reference/provider-keys-and-env-vars.md)

Tools and harnesses:

- [Author A Tool Harness](guides/tools/author-a-tool-harness.md)
- [Source Generation, AOT, And Trimming](guides/tools/source-generation-aot-and-trimming.md)
- [Collapsing And Containers](guides/tools/collapsing-and-containers.md)
- [Externally Executed Client Tools](guides/tools/externally-executed-client-tools.md)
- [MCP Tools](guides/tools/mcp-tools.md)
- [OpenAPI Tools](guides/tools/openapi-tools.md)
- [Multi-Agent Capabilities](guides/tools/multi-agent-capabilities.md)
- [Built-In Harnesses](guides/harnesses/overview.md)
- [Coding Harness](guides/harnesses/coding.md)
- [FileSystem Harness](guides/harnesses/filesystem.md)
- [Web Search Harness](guides/harnesses/web-search.md)

Advanced orchestration:

- [Subagents](guides/agents/subagents.md)
- [Multi-Agent Overview](guides/multi-agent/overview.md)
- [Choose A Composition Pattern](guides/multi-agent/choose-a-pattern.md)
- [Build A Multi-Agent Workflow](guides/multi-agent/build-a-workflow.md)
- [Execution Model](guides/multi-agent/execution-model.md)
- [Workflow Patterns](guides/multi-agent/workflow-patterns.md)
- [Conversation Policies](guides/multi-agent/conversation-policies.md)
- [Data Flow Between Nodes](guides/multi-agent/data-flow-between-nodes.md)
- [Routing And Handoffs](guides/multi-agent/routing-and-handoffs.md)
- [Checkpointing](guides/multi-agent/checkpointing.md)
- [Workflow Events](guides/multi-agent/workflow-events.md)
- [Config And Export](guides/multi-agent/config-and-export.md)

Events and streaming:

- [Events Overview](guides/events/overview.md)
- [Custom Events](guides/events/custom-events.md)
- [Tool And Function Events](guides/events/tool-and-function-events.md)
- [Bidirectional Events](guides/events/bidirectional-events.md)
- [TypeScript Client Events](guides/events/typescript-client.md)
- [Serialization And Registration](guides/events/serialization-and-registration.md)
- [Live Vs Durable Events](guides/events/live-vs-durable-events.md)
- [Lifecycle, Retry, And Error Events](guides/events/lifecycle-retry-and-error-events.md)
- [Testing Event-Driven Code](guides/events/testing-event-driven-code.md)
- [Render An Event Stream](guides/sessions-and-streaming/render-an-event-stream.md)
- [Branch History And Forking](guides/sessions-and-streaming/branch-history-and-forking.md)
- [Compaction](guides/sessions-and-streaming/compaction.md): reduce model-visible context and optionally compact durable branch history.
- [Events Reference](reference/events.md)

Middleware:

- [Middleware Overview](guides/middleware/overview.md)
- [Custom Middleware](guides/middleware/custom-middleware.md)
- [Middleware State Persistence](guides/middleware/state-persistence.md)
- [Permissions Middleware](guides/middleware/permissions.md)
- [Error Handling Middleware](guides/middleware/error-handling.md)

Hosting:

- [ASP.NET Core Hosting](guides/hosting/aspnet-core.md)
- [Hosted Streaming API](guides/hosting/hosted-streaming-api.md)
- [Stored Agent Definitions](guides/hosting/stored-agent-definitions.md)

Bots:

- [Bots Overview](guides/bots/overview.md)
- [Bot Platform Setup](guides/bots/platform-setup.md)
- [Slack Bots](guides/bots/slack.md)
- [Discord Bots](guides/bots/discord.md)
- [Telegram Bots](guides/bots/telegram.md)
- [WhatsApp Bots](guides/bots/whatsapp.md)
- [Teams Bots](guides/bots/teams.md)
- [Custom Bot Adapters And Source Generation](guides/bots/custom-adapters-and-source-generation.md)

Observability:

- [Logging And Telemetry](guides/observability/logging-and-telemetry.md)

Runtime systems:

- [Sandboxing Overview](guides/sandboxing/overview.md)
- [Local Process Isolation](guides/sandboxing/local-process-isolation.md)
- [Document Handling And Text Extraction](guides/content/document-handling-and-text-extraction.md)
- [Content Upload And Resolution](guides/content/content-upload-and-resolution.md)
- [FFI Overview](guides/ffi/overview.md)
- [Audio Overview](guides/audio/overview.md)
- [Audio Runtime Attachment](guides/audio/runtime-attachment.md)
- [Text To Speech Output](guides/audio/text-to-speech-output.md)
- [Speech To Text Input](guides/audio/speech-to-text-input.md)
- [Realtime Audio](guides/audio/realtime-audio.md)
- [Audio Events And Traces](guides/audio/audio-events-and-traces.md)

Evaluations:

- [Evaluations Overview](guides/evaluations/overview.md)
- [Batch Evals](guides/evaluations/batch-evals.md)
- [Evaluator Picker](guides/evaluations/evaluator-picker.md)
- [Datasets And Reports](guides/evaluations/datasets-and-reports.md)
- [Live Evaluation](guides/evaluations/live-evaluation.md)
- [LLM Judges And Safety](guides/evaluations/llm-judges-and-safety.md)
- [Red Team](guides/evaluations/red-team.md)

## Live Smoke Gates

- OpenAI text-to-speech: `HPD_AUDIO_LIVE_SMOKE=1 OPENAI_API_KEY=...`
- OpenAI realtime agent turn: `HPD_REALTIME_LIVE_SMOKE=1 OPENAI_API_KEY=...`
- ElevenLabs text-to-speech: `HPD_AUDIO_LIVE_SMOKE=1 ELEVENLABS_API_KEY=...`
- ONNX Runtime local inference: `ONNX_MODEL_PATH=/absolute/path/to/onnx-genai-model`
- ONNX Runtime structured tool calling: `ONNX_MODEL_PATH=/absolute/path/to/onnx-genai-model ONNX_TOOL_CALL_SMOKE=1`
