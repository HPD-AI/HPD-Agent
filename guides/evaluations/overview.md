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
var report = await RunEvals.ExecuteAsync(
    agent,
    dataset,
    evaluators:
    [
        new EqualsGroundTruthEvaluator(),
        new OutputContainsEvaluator("Paris"),
    ],
    experimentName: "capital-smoke");

Console.WriteLine(report.PassRate("Output Contains"));
```

That gives you one report with case results, metric values, pass-rate helpers, and JSON output for CI artifacts.

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

Use `InMemoryScoreStore` for local runs, tests, and examples. It is not durable storage. For production score history, wire `IScoreStore` to application storage.
