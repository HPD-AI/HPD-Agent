# Documentation Audit Ledger

Baseline: HPD Agent `0.9.5` / framework tag `v0.9.5`.

Status is updated after each navigation section is compared with the vendored source and
published NuGet contracts.

| Section | Status | Commit | Notes |
| --- | --- | --- | --- |
| Start Here / Getting Started | Complete | `c0fed7a` | Session and thread APIs, persistence setup, workflow packages, and all executable examples corrected. |
| Core Concepts | Complete | `91cac1b` | Provider references, agent operations, session durability, and middleware state contracts corrected. |
| Tools | Complete | `de9ee01` | Harness inheritance, constructor diagnostics, multi-agent receipts, and client-tool operation semantics corrected. |
| Skills | Complete | `e9e9314` | All APIs current; activation lifetime version corrected to `0.9.5`. |
| Events | Complete | `000ad37`, `ef7b1c2` | Replaced obsolete background-task events; corrected durability, request signatures, operation projections, and event diagnostics. |
| Middleware | Complete | `16e75a1` | Corrected runtime middleware invocation, durable event publication, state persistence guarantees, streaming wrappers, service-owned harness middleware, and compile-checked permissions/error examples. |
| Providers | Complete | `efcbce0` | Corrected provider references, authentication, family configuration, request options, and JSON shapes; all provider examples compile against `0.9.5`. |
| Sessions And Streaming | Complete | `83defed` | Corrected journal replacement, generation-aware cursors, fork APIs, lifecycle controls, compaction identities, and event projection imports. |
| Multi-Agent | Complete | `29e3938` | Corrected node option APIs, error helpers, input resolution, config casing, export boundaries, public recovery events, and HPD Base durability setup. |
| Hosting And Clients | Complete | `6bfe2a3` | Corrected SSE envelopes and cursors, input/control routes, stored-definition config, OpenAPI versioning, and HPD Base-backed durable store setup. |
| Runtime Systems | Complete | `35f5a95` | Corrected coding run-config usage, deprecated FileSystem guidance, content lifecycle references, document extraction ownership, FFI exports/subscriptions, and sandbox response imports. |
| Audio | Next | — | — |
| Bots | Pending | — | — |
| Evaluations | Pending | — | — |
| Observability And UI | Pending | — | — |

## Verification Standard

For each section:

1. Compare install versions, public type names, members, signatures, and configuration shapes with
   the `v0.9.5` source.
2. Compile representative C# and executable snippets against the published `0.9.5` NuGet packages.
3. Build the existing cookbook samples for the same feature area when available.
4. Run the VitePress production build.
5. Record intentionally historical content and current framework limitations instead of presenting
   them as supported behavior.

TypeScript examples are checked against the C# event, request, and DTO contracts. A TypeScript
compilation check is added when the repository has an application project that references the
client package.

## Framework Issues Found

- `[MiddlewareState]` in the global namespace causes the 0.9.5 middleware-state source generator to
  emit invalid extension code. Documentation currently uses an explicit namespace; the generator
  still needs a framework fix.
- `WorkflowSettingsConfig.MaxIterations`, `DefaultTimeout`, `EnableMetrics`, and `StreamingMode`
  serialize but are not consumed by direct `AgentWorkflowInstance` execution in `0.9.5`.
