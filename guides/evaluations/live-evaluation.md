# Live Evaluation

Live evaluation scores real agent runs after the agent response has returned. Use it for monitoring, dashboards, review queues, and trend signals from production-like traffic.

## Add A Live Evaluator

```csharp
using HPD.Agent;
using HPD.Agent.Evaluations.Evaluators.Deterministic;
using HPD.Agent.Evaluations.Integration;
using HPD.Agent.Evaluations.Storage;

var store = new InMemoryScoreStore();

var agent = await new AgentBuilder()
    .WithChatClient(chatClient)
    .UseScoreStore(store)
    .AddEvaluator(new OutputContainsEvaluator("approved"))
    .BuildAsync();
```

When the agent runs, live evaluation starts after the response path. The run can return before the evaluator finishes.

```csharp
await agent.RunAsync("Draft the approval note.");

await foreach (var score in store.GetScoresAsync())
{
    Console.WriteLine($"{score.EvaluatorName}: {score.MetricName}");
}
```

If your UI needs the score immediately, poll or subscribe through your app's score/telemetry path. Do not assume the score is ready when `RunAsync` returns.

## Use It For Signals

Live evaluation is not a request gate. If the response must be blocked, changed, or escalated before the user sees it, use middleware or permissions in the run path. Live evaluation is for after-turn scoring, dashboards, and review queues.

## Add Evaluators For One Run

Use run config overrides when a specific turn needs extra scoring:

```csharp
var runConfig = new AgentRunConfig()
    .WithAdditionalEvaluators(new OutputContainsEvaluator("approved"));

await agent.RunAsync("Draft the approval note.", runConfig);
```

## Observe Live Eval Events

Live evaluation can emit score, failure, and policy-violation events:

```csharp
using HPD.Agent.Evaluations.Integration;

using var scores = agent.Subscribe<EvalScoreEvent>(evt =>
{
    Console.WriteLine($"{evt.EvaluatorName}: {evt.TurnIndex}");
});

using var failures = agent.Subscribe<EvalFailedEvent>(evt =>
{
    Console.WriteLine($"{evt.EvaluatorName} failed: {evt.ErrorMessage}");
});
```

Use score events for dashboards. Use policy-violation events when a `MustAlwaysPass` evaluator ran successfully but the response failed the metric.

## Disable During Batch Runs

Batch evals use their own evaluator list. `RunEvals.ExecuteAsync(...)` disables live evaluators on the run config so batch metrics are not duplicated by the agent's live evaluator configuration.
