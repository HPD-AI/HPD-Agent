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
| Events | Complete | `000ad37` | Replaced obsolete background-task events; corrected durability, request signatures, operation projections, and event diagnostics. |
| Middleware | Next | — | — |
| Providers | Pending | — | — |
| Sessions And Streaming | Pending | — | — |
| Multi-Agent | Pending | — | — |
| Hosting And Clients | Pending | — | — |
| Runtime Systems | Pending | — | Includes harnesses, sandboxing, FFI, and content. |
| Audio | Pending | — | — |
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
