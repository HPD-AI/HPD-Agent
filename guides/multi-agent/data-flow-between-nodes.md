# Data Flow Between Nodes

Multi-agent workflows pass data through graph inputs and node outputs. Each agent node returns a dictionary of outputs. Downstream nodes and edge conditions read values from that dictionary.

For predictable workflows, define the contract between nodes explicitly with `WithOutputKey(...)`, `WithInputKey(...)`, `WithInputTemplate(...)`, or structured outputs.

Multi-agent workflows do not expose port-level routing in the agent workflow builder. Model agent-to-agent data flow with named output keys, input keys, input templates, structured outputs, and route conditions.

## String Output

String mode is the default. HPD buffers the child agent's streamed text and writes it to `answer` unless you set an output key:

```csharp
.AddAgent("research", researchConfig, node => node.WithOutputKey("research"))
.AddAgent("write", writerConfig, node => node.WithInputKey("research"))
.From("research").To("write")
```

In this workflow, the first node writes:

```json
{
  "research": "..."
}
```

The second node reads `research` as its input.

## Input Resolution

If a node does not set an input key or template, HPD chooses an input using this order:

1. `InputTemplate`
2. `InputKey`
3. `shared.input`
4. `question`, `input`, `message`, `query`, `prompt`
5. namespaced semantic keys such as `research.input`
6. original workflow input
7. first non-empty upstream string that is not `shared.*`
8. empty string

The sharp edge is `shared.input`: it is the original workflow input, and it is available to all nodes. A downstream node can keep seeing the original request instead of a previous node's output unless you explicitly wire the data contract.

## Input Templates

Use `WithInputTemplate(...)` when a node needs more than one value:

```csharp
.AddAgent("write", writerConfig, node => node.WithInputTemplate("""
User request:
{{shared.input}}

Research:
{{research}}

Risks:
{{risks}}
"""))
```

The template renderer performs simple `{{key}}` replacement over the graph input bag. Treat it as placeholder replacement, not full Handlebars.

## Structured Output

Structured mode asks the child agent for a structured result and flattens non-null public properties into lowercase output keys.

```csharp
public sealed record AnalysisResult(
    string Sentiment,
    double Confidence,
    string Recommendation);

var workflow = await AgentWorkflow.Create()
    .AddAgent("analyze", analyzerConfig, node => node.StructuredOutput<AnalysisResult>())
    .AddAgent("approve", approvalConfig)
    .From("analyze").To("approve").WhenGreaterThan("confidence", 0.8)
    .BuildAsync();
```

If the model returns `AnalysisResult`, downstream routing sees keys such as:

```json
{
  "sentiment": "positive",
  "confidence": 0.91,
  "recommendation": "ship",
  "result": {}
}
```

Property names are lowercased. Route on `confidence`, not `Confidence`.

## Union Output

Union mode asks the child agent to return one of several result types. HPD flattens the result and adds `matched_type`.

```csharp
public sealed record MathRoute(string Question);
public sealed record ResearchRoute(string Topic);
public sealed record GeneralRoute(string Request);

var workflow = await AgentWorkflow.Create()
    .AddAgent("classify", classifierConfig, node =>
        node.UnionOutput<MathRoute, ResearchRoute, GeneralRoute>())
    .AddAgent("solve_math", mathConfig)
    .AddAgent("research", researchConfig)
    .AddAgent("answer", generalConfig)
    .From("classify").RouteByType()
        .When<MathRoute>("solve_math")
        .When<ResearchRoute>("research")
        .When<GeneralRoute>("answer")
    .BuildAsync();
```

`RouteByType()` matches `matched_type` against `typeof(T).Name`.

## Child Agent Run Config

Node options can also affect each child agent run:

- `WithInstructions(...)` appends node-specific system instructions.
- `WithTimeout(...)` sets the child run timeout.
- `WithContext(...)` provides runtime context instances for tool harnesses.
- structured and union modes set structured-output options.
- handoff mode injects generated handoff tools and requires a handoff tool call.

Retry, graph timeout, and edge routing are graph-level concerns, not ordinary chat options.

## Related Pages

- [Build A Multi-Agent Workflow](build-a-workflow.md)
- [Routing And Handoffs](routing-and-handoffs.md)
- [Workflow Events](workflow-events.md)
