# Evaluations Overview

Use evaluations to keep agent behavior from drifting. Start with a small batch eval, add more cases as bugs appear, then use live evaluation and red-team runs when you want signals from real traffic or adversarial prompts.

## Pick A Workflow

| Goal | Start here |
| --- | --- |
| Catch regressions in CI | [Batch Evals](batch-evals.md) |
| Choose the right evaluator | [Evaluator Picker](evaluator-picker.md) |
| Move test cases out of code | [Datasets And Reports](datasets-and-reports.md) |
| Score real runs after users get a response | [Live Evaluation](live-evaluation.md) |
| Grade subjective quality or safety | [LLM Judges And Safety](llm-judges-and-safety.md) |
| Try adversarial prompts against an agent | [Red Team](red-team.md) |

Batch evals are the fastest first win. You can run one without provider credentials by giving the agent a fixed or fake `IChatClient`.

## Smallest Shape

```csharp
using HPD.Agent.Evaluations;
using HPD.Agent.Evaluations.Integration;

var report = await agent.EvaluateAsync(
    ["What is the capital of France?"],
    [Eval.Contains("Paris")],
    experimentName: "capital-smoke");

Console.WriteLine(report.PassRate("Output Contains"));
```

That gives you one report with case results, metric values, pass-rate helpers, and JSON output for CI artifacts. When cases need stable ids, dataset versions, per-case metadata, or YAML/JSON files, use the full `Dataset<string>` + `RunEvals.ExecuteAsync(...)` shape in [Batch Evals](batch-evals.md).

## Evaluator Choice

If you are not sure which evaluator to start with, use the [Evaluator Picker](evaluator-picker.md).

Use deterministic evaluators when you want a repeatable test:

- exact ground-truth match
- output contains a required phrase
- JSON, XML, HTML, SQL, or schema checks
- tool-call and performance checks

Use judge evaluators when the question is subjective:

- correctness
- tone
- refusal quality
- policy fit
- answer completeness

Judge evaluators call another model or agent, so treat them as quality signals unless your own policy defines the threshold and review process.

## Storage

Use `InMemoryScoreStore` for local runs, tests, and examples. It is not durable storage.
For production, `HPD-Agent.Evaluations.HpdBase` provides restart-durable score, run, and
dataset stores backed by HPD Base. The dataset store requires a `DatasetCodec<TInput>` for
each stored input type.

## HTTP API

`HPD-Agent.AspNetCore.Evaluations` exposes the optional `/evals` surface:

```csharp
using HPD.Agent.AspNetCore.Evaluations;
using HPD.Agent.Evaluations.Storage;

builder.Services.AddHPDAgentEvaluations();

var app = builder.Build();
app.MapHPDAgentEvaluationApi();
```

The catalog routes work without storage:

```text
GET /evals/evaluators/catalog
GET /evals/red-team/plugins
GET /evals/red-team/strategies
```

Score, analytics, run, dataset, and persisted red-team routes require registered
`IScoreStore` and/or `IDatasetStore` services and return `503` when the needed store is
missing. `/evals/evaluators/catalog` returns the complete built-in evaluator registry,
including stable ids, metric names, default policies, judge requirements, and required
evaluation contexts.

The HTTP dataset registry accepts string-input datasets only. Typed datasets remain an
in-process API. The package does not choose authentication; apply the host's normal
authorization policy to the mapped evaluation route group.
