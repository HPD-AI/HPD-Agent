# Evaluator Picker

Draft boundary: this catalog is source-grounded against the evaluator classes under `HPD-Agent.Evaluations/Evaluators` and the corresponding evaluator tests. It is meant to help developers choose a first evaluator without reading source. It is not a complete API reference, and long-tail evaluators are grouped when that makes first use clearer.

## Pick The Smallest Useful Evaluator

| Need | Start with | Judge required | Gate in CI? | Trend instead? |
| --- | --- | --- | --- | --- |
| Exact expected answer | `EqualsGroundTruthEvaluator`, `OutputEqualsEvaluator` | No | Yes | Usually no |
| Required phrase, prefix, regex, length | `OutputContainsEvaluator`, `ContainsAllEvaluator`, `StartsWithEvaluator`, `OutputMatchesRegexEvaluator`, `WordCountEvaluator` | No | Yes | Sometimes |
| Valid JSON/XML/HTML/SQL shape | `JsonValidityEvaluator`, `XmlValidityEvaluator`, `HtmlShapeEvaluator`, `SqlShapeEvaluator` | No | Yes | Usually no |
| JSON fields and light schema checks | `SchemaConformanceEvaluator`, `FieldCompletenessEvaluator`, `FieldAccuracyEvaluator` | No | Yes for contract tests | Track field completeness over time |
| Tool behavior | `ToolWasCalledEvaluator`, `ToolCallCountEvaluator`, `ToolArgumentMatchesEvaluator`, `ToolResultContainsEvaluator`, `NoToolsCalledEvaluator`, `ToolCallOrderEvaluator`, `ToolCallF1Evaluator` | No | Yes when tool behavior is required | Use F1 as a trend when tool choice can vary |
| Latency, tokens, cost, iterations | `MaxDurationEvaluator`, `MaxTokensEvaluator`, `MaxInputTokensEvaluator`, `MaxOutputTokensEvaluator`, `MaxCostEvaluator`, `MaxIterationsEvaluator`, `LatencyEvaluator` | No | Gate only hard limits | Trend latency/cost |
| Semantic answer quality | `AnswerSimilarityEvaluator`, `GoalAccuracyEvaluator`, `TaskSuccessEvaluator`, `AspectCriticEvaluator`, `CustomJudgeEvaluator` | Yes | Only with owned thresholds/review | Yes |
| Retrieval quality and hallucination | `ContextRelevanceEvaluator`, `HallucinationEvaluator`, `ContextRecallEvaluator`, `ContextPrecisionEvaluator` | Yes | Rarely | Yes |
| Safety and policy review | Safety evaluators such as `PromptInjectionEvaluator`, `SensitiveDataLeakEvaluator`, `CodeSecurityRiskEvaluator`, `PolicyComplianceEvaluator` | Yes | Yes only after product policy defines thresholds | Yes for discovery and tuning |
| Conversation and memory | `ConversationCoherenceEvaluator`, `GoalProgressionEvaluator`, `RepetitionDetectionEvaluator`, `MemoryAccuracyEvaluator` | Yes | Rarely | Yes |
| Risk/autonomy monitoring | `TurnRiskEvaluator` plus `TurnAutonomyEvaluator` | Mixed: risk yes, autonomy no | Rarely | Yes |

Default policy in batch evals is source-defined: deterministic evaluators are `MustAlwaysPass`; other evaluators default to `TrackTrend`. Live `AddEvaluator` defaults to `MustAlwaysPass`, so pass `policy: EvalPolicy.TrackTrend` explicitly for judge metrics you want to monitor rather than fail.

For quick deterministic checks, `HPD.Agent.Evaluations.Eval` provides aliases over the same evaluator classes:

```csharp
await agent.CheckAsync(
    "What is the capital of France?",
    Eval.Contains("Paris"));
```

Use the concrete evaluator classes when you need constructor options not exposed by the alias, or when the class name is clearer in a reusable suite.

## CI Gate Or Trend Signal?

Use `MustAlwaysPass` when the answer is mechanically checkable and a failure means the agent is broken: exact output, output contract, required tool call, no tool call, maximum tokens, or a bounded duration.

Use `TrackTrend` when the score is probabilistic, subjective, or depends on a judge model: semantic quality, hallucination, safety discovery, reasoning quality, conversation memory, risk, autonomy, and aggregate quality scores.

There are two practical exceptions:

- Wrap a numeric deterministic score with `ThresholdGate` when you need a hard gate, for example `new ThresholdGate(new ToolCallF1Evaluator("Search", "Fetch"), 0.8)`.
- Safety evaluators emit both a numeric score and a `... Passed` boolean through `SafetyPolicy`. They can become gates after the application owns the policy, threshold, and review process.

## First Evaluator Families

### Output Assertions

Use these when the response text itself is the contract. They do not need a judge model.

- `OutputContainsEvaluator`, `ContainsAnyEvaluator`, `ContainsAllEvaluator`, `CaseInsensitiveContainsEvaluator`, and `StartsWithEvaluator` check simple string presence or prefix rules.
- `OutputMatchesRegexEvaluator` checks a regex against `modelResponse.Text`.
- `OutputEqualsEvaluator` checks exact text; `EqualsGroundTruthEvaluator` checks exact text against `GroundTruthContext`.
- `WordCountEvaluator` checks min, max, or exact word count and stores the observed count as metadata.
- `RefusalEvaluator` detects common refusal phrases. Use `NotEvaluator(new RefusalEvaluator())` when refusal is a failure.
- `KeywordCoverageEvaluator`, `ContentSimilarityEvaluator`, and `LevenshteinEvaluator` return numeric similarity or coverage scores. Gate them only with an explicit threshold.

```csharp
evaluators:
[
    new EqualsGroundTruthEvaluator(),
    new ContainsAllEvaluator("Paris", "France"),
    new OutputMatchesRegexEvaluator(@"\b\d{4}\b"),
]
```

### Structured Output

Use these when the answer must be machine-readable.

- `JsonValidityEvaluator` and `XmlValidityEvaluator` parse the response.
- `HtmlShapeEvaluator` checks for plausible HTML tags and optional required tags. It is not W3C validation.
- `SqlShapeEvaluator` checks plausible SQL statement shape, balanced parentheses, and balanced quotes. It is not dialect validation.
- `SchemaConformanceEvaluator` validates a JSON schema subset: `type`, `required`, `properties`, `minLength`, `maxLength`, `minimum`, `maximum`, and `enum`.
- `FieldCompletenessEvaluator` scores the fraction of named top-level JSON fields present and non-null.
- `FieldAccuracyEvaluator` checks a top-level JSON field value with case-insensitive string comparison.
- `SemanticFieldEqualityEvaluator` is the judge-backed escape hatch when a field can be semantically right without matching exactly.

```csharp
var schema = """
{
  "type": "object",
  "required": ["city", "country"],
  "properties": {
    "city": { "type": "string" },
    "country": { "enum": ["France"] }
  }
}
""";

evaluators:
[
    new JsonValidityEvaluator(),
    new SchemaConformanceEvaluator(schema),
    new FieldAccuracyEvaluator("country", "France"),
]
```

### Tool And Trace Behavior

Use these when success depends on how the agent acted, not only what it said. These evaluators require `TurnEvaluationContext`; live evaluation and batch runs through the HPD evaluation pipeline provide it.

- `ToolWasCalledEvaluator` checks that a named tool was called at least once.
- `ToolCallCountEvaluator` checks an exact count.
- `ToolArgumentMatchesEvaluator` parses the tool call JSON arguments and compares one argument value exactly.
- `ToolResultContainsEvaluator` checks a named tool result for expected text.
- `NoToolsCalledEvaluator` asserts that no tools were used.
- `ToolCallOrderEvaluator` checks that expected tools appear in order as a subsequence; extra calls may exist.
- `ToolCallF1Evaluator` returns unordered precision/recall/F1 over expected tool names.
- `HasMatchingSpanEvaluator` is the broader trace assertion for matching a `SpanQuery` against `TurnTrace`.

```csharp
evaluators:
[
    new ToolWasCalledEvaluator("Search"),
    new ToolArgumentMatchesEvaluator("Search", "query", "capital of France"),
    new ToolCallOrderEvaluator(["Search", "Summarize"]),
]
```

### Performance And Cost

Use hard limits for regressions that should fail immediately. Use numeric trends for everything else.

- `MaxDurationEvaluator`, `MaxIterationsEvaluator`, `MaxTokensEvaluator`, `MaxInputTokensEvaluator`, and `MaxOutputTokensEvaluator` return booleans.
- `LatencyEvaluator` reports duration in seconds and `latency-ms` metadata.
- `MaxCostEvaluator` looks for cost in turn metrics under `cost_usd`, `turn_cost_usd`, `estimated_cost_usd`, or `cost`. If no cost metric exists, it returns a warning diagnostic rather than inventing cost.

These also require `TurnEvaluationContext`.

### NLP Overlap Metrics

Use these when you need deterministic overlap metrics against references. They are better for summaries, translations, or extraction-style tasks than open-ended assistant quality.

- `BleuEvaluator`, `GleuEvaluator`, and `TextF1Evaluator` wrap Microsoft NLP evaluators.
- `RougeEvaluator` supports `Rouge1`, `Rouge2`, `RougeL`, and `RougeS`.
- `MeteorEvaluator` is a lightweight dependency-free METEOR-style unigram alignment score with configurable `MeteorEvaluatorOptions`.

Treat these as trends unless your dataset and threshold are already calibrated.

### LLM Judges

Use judge evaluators when deterministic checks cannot express the quality bar. They require judge configuration through `EvalJudgeConfig`, `UseEvalJudgeConfig`, or `UseEvalJudgeAgent`.

- `AnswerSimilarityEvaluator` compares the response to `GroundTruthContext`.
- `GoalAccuracyEvaluator` scores goal achievement from ground truth or turn context.
- `TaskSuccessEvaluator` returns a boolean for whether the user task appears complete.
- `AspectCriticEvaluator` is the fastest custom boolean rubric.
- `CustomJudgeEvaluator` is a custom 0-1 rubric.
- `TopicAdherenceEvaluator` scores whether the response stays within allowed topics.

```csharp
var report = await RunEvals.ExecuteAsync(
    agent,
    dataset,
    evaluators:
    [
        new AspectCriticEvaluator("The answer is correct, concise, and cites the retrieved source."),
        new AnswerSimilarityEvaluator(),
    ],
    options: new RunEvalsOptions<string>
    {
        JudgeConfig = new EvalJudgeConfig
        {
            OverrideChatClient = judgeChatClient,
        },
    });
```

### Retrieval And Hallucination

Use these when the agent is grounded in retrieved or supplied documents.

- `ContextRelevanceEvaluator` judges whether retrieved context helps answer the query.
- `HallucinationEvaluator` extracts factual claims from the output, verifies them against grounding documents, and scores contradicted claims divided by total claims. Unsupported claims are not counted as hallucinations.
- `ContextRecallEvaluator` extracts factual claims from ground truth and checks whether the grounding context supports them.
- `ContextPrecisionEvaluator` scores ranked grounding chunks with a mean-average-precision style aggregate.

These require a judge model and relevant contexts such as `GroundingDocumentContext` and, for recall, `GroundTruthContext`.

### Safety And Policy

Safety evaluators are JSON judges. Each returns a numeric score on a 0-7 scale plus a boolean `... Passed` metric after applying `SafetyPolicy`. The judge prompt includes recent conversation, the assistant response, HPD tool calls/results, and reasoning text when present.

Use the specific class when the risk is known:

- Harm categories: `ContentHarmEvaluator`, `HateHarassmentEvaluator`, `ViolenceSafetyEvaluator`, `SelfHarmSafetyEvaluator`, `SexualContentSafetyEvaluator`.
- Prompt and instruction attacks: `PromptInjectionEvaluator`, `JailbreakAttemptEvaluator`.
- Data and IP risks: `SensitiveDataLeakEvaluator`, `ProtectedMaterialEvaluator`.
- Code and human-attribute risks: `CodeSecurityRiskEvaluator`, `UngroundedSensitiveAttributeEvaluator`.
- Product policy: `PolicyComplianceEvaluator`.

```csharp
evaluators:
[
    new PromptInjectionEvaluator(),
    new SensitiveDataLeakEvaluator(),
    new CodeSecurityRiskEvaluator(new SafetyPolicy { BlockThreshold = 4.0 }),
]
```

Keep early safety runs as trend/review signals. Promote the `... Passed` metric to a gate only after the product policy, block threshold, and escalation workflow are explicit.

### Conversation, Memory, Reasoning, Risk

These are useful signals for live evaluation and longer-running agents. Most should remain trend metrics.

- Conversation: `ConversationCoherenceEvaluator`, `GoalProgressionEvaluator`, `RepetitionDetectionEvaluator`, and `MemoryAccuracyEvaluator`.
- Reasoning: `ReasoningCoherenceEvaluator`, `ReasoningGroundednessEvaluator`, and `ReasoningEfficiencyEvaluator`. They depend on reasoning context or captured turn reasoning text.
- Risk/autonomy: `TurnRiskEvaluator` judges potential harm on a 1-10 scale; `TurnAutonomyEvaluator` deterministically scores autonomy from iteration count, permission-denied rate, stop kind, and duration.
- Specialty: `SqlSemanticEquivalenceEvaluator` judges whether generated SQL is equivalent to reference SQL for a schema; `NoiseSensitivityEvaluator` compares a noisy-response run against a baseline response.

Use these to compare prompts, models, tool permissions, or releases. Do not use them as hard gates until the team has calibrated scores against reviewed examples.

### Composite Helpers

Use composition after individual metrics are already useful.

- `ThresholdGate` converts a numeric metric to a boolean gate.
- `WeightedScoreEvaluator` averages numeric sub-evaluator scores and excludes inconclusive or failed sub-evaluators from the weighted average.
- `NotEvaluator` inverts the primary metric of another evaluator.

```csharp
evaluators:
[
    new ThresholdGate(new RougeEvaluator(reference, RougeVariant.RougeL), 0.75),
    new NotEvaluator(new OutputContainsEvaluator("internal_secret")),
]
```

## Reference-Only Notes

This page intentionally does not fully document base classes such as `HpdEvaluatorBase`, `HpdLlmJudgeEvaluatorBase`, `HpdJsonJudgeEvaluatorBase`, `HpdDecomposeVerifyEvaluatorBase`, or `TaskOracleEvaluator`. They matter for authors of new evaluators, but they are not first-use picker material.

The source includes `TaskOracleEvaluator` for custom task-specific oracle subclasses. Treat it as an extension point until there is a documented public oracle example.

Long-tail judge metrics such as reasoning, memory, noise sensitivity, and SQL semantic equivalence should be considered reference-only until calibrated with reviewed datasets. Their class names and prompts are source-grounded, but recommended thresholds are intentionally not supplied here.

## Source Grounding

This page is based on:

- `src/HPD-Agent.Evaluations/Evaluators/Deterministic/OutputEvaluators.cs`
- `src/HPD-Agent.Evaluations/Evaluators/Deterministic/StructuredOutputEvaluators.cs`
- `src/HPD-Agent.Evaluations/Evaluators/Deterministic/ToolCallEvaluators.cs`
- `src/HPD-Agent.Evaluations/Evaluators/Deterministic/PerformanceEvaluators.cs`
- `src/HPD-Agent.Evaluations/Evaluators/Deterministic/TurnAutonomyEvaluator.cs`
- `src/HPD-Agent.Evaluations/Evaluators/Deterministic/SpanQueryEvaluator.cs`
- `src/HPD-Agent.Evaluations/Evaluators/Nlp/NlpEvaluators.cs`
- `src/HPD-Agent.Evaluations/Evaluators/Composite/CompositeEvaluators.cs`
- `src/HPD-Agent.Evaluations/Evaluators/LlmJudge/*.cs`
- `src/HPD-Agent.Evaluations/Evaluators/Safety/*.cs`
- evaluator tests under `test/HPD-Agent.Evaluations.Tests/Evaluators`
