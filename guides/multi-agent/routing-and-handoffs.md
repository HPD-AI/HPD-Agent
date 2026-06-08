# Routing And Handoffs

Routing decides which workflow nodes run after a node completes. Routes can be unconditional, field-based, type-based, predicate-based, or selected by a router agent through handoff tools.

## Linear Routes

```csharp
var workflow = await AgentWorkflow.Create()
    .AddAgent("draft", draftConfig)
    .AddAgent("review", reviewConfig)
    .From("draft").To("review")
    .BuildAsync();
```

Without explicit `START` and `END` edges, HPD infers them around nodes that have no declared incoming or outgoing edge.

HPD does not infer `draft -> review` from the order of `.AddAgent(...)` calls. Add the route when one agent must run after another.

## Field Conditions

Use conditions when a node's outputs decide the next node:

```csharp
.AddAgent("analyze", analyzerConfig, node => node.StructuredOutput<AnalysisResult>())
.AddAgent("publish", publishConfig)
.AddAgent("revise", reviseConfig)
.From("analyze").To("publish").WhenGreaterThan("confidence", 0.8)
.From("analyze").To("revise").AsDefault()
```

These helpers create declarative edge conditions. Declarative conditions are serializable and can appear in workflow config/export.

Common condition helpers include:

- `WhenEquals(...)`
- `WhenNotEquals(...)`
- `WhenExists(...)`
- `WhenNotExists(...)`
- `WhenGreaterThan(...)`
- `WhenLessThan(...)`
- `WhenContains(...)`
- `WhenStartsWith(...)`
- `WhenEndsWith(...)`
- `WhenMatchesRegex(...)`
- `WhenContainsAny(...)`
- `WhenContainsAll(...)`
- `AsDefault()`

Compound conditions use `Condition.And(...)`, `Condition.Or(...)`, and `Condition.Not(...)`.

## Type Routes

Use union output when a classifier should return one of several route types:

```csharp
var workflow = await AgentWorkflow.Create()
    .AddAgent("classify", classifierConfig, node =>
        node.UnionOutput<MathRoute, ResearchRoute, GeneralRoute>())
    .AddAgent("math", mathConfig)
    .AddAgent("research", researchConfig)
    .AddAgent("general", generalConfig)
    .From("START").To("classify")
    .From("classify").RouteByType()
        .When<MathRoute>("math")
        .When<ResearchRoute>("research")
        .When<GeneralRoute>("general")
    .From("math", "research", "general").To("END")
    .BuildAsync();
```

`RouteByType()` can only be used from one source node. It matches `matched_type` to `typeof(T).Name`, so avoid union types that share the same simple type name.

## Router Agents And Handoffs

A router agent chooses the next node by calling a generated handoff tool.

```csharp
var workflow = await AgentWorkflow.Create()
    .AddRouterAgent("router", new AgentConfig
    {
        Name = "Router",
        SystemInstructions = "Route the request to the best specialist.",
    })
        .WithHandoff("math", "Use for calculations and numeric reasoning.")
        .WithHandoff("research", "Use for information gathering.")
        .WithDefaultHandoff("general")
    .AddAgent("math", mathConfig)
    .AddAgent("research", researchConfig)
    .AddAgent("general", generalConfig)
    .BuildAsync();
```

At runtime, HPD gives the router tools such as `handoff_to_math` and `handoff_to_research`, requires the router to call one, and writes the selected target to `handoff_target`. Edges from the router are conditioned on that value.

Keep handoff target ids tool-name-safe because they become part of generated tool names.

## Predicate Routes

Predicate routes run in-process code and are the non-serializable conditional route form:

```csharp
.From("analyze").To("publish").When(ctx =>
    ctx.Get<double>("confidence") > 0.8)
```

That overload takes a `Func<EdgeConditionContext, bool>`. The function can run during an in-process workflow, but it cannot be represented in JSON config or exported as an executable condition. Use declarative helpers such as `WhenGreaterThan(...)`, `WhenEquals(...)`, or `Condition.And(...)` when a workflow needs to round-trip through config.

## Serialization Boundary

These route forms are config-friendly:

- unconditional edges
- declarative field/string/regex/collection conditions
- compound conditions
- default edges
- type routes, because they are `matched_type` conditions
- handoff edges, because they are `handoff_target` conditions

These need care:

- predicate routes are non-serializable and export without their predicate
- handoff target descriptions are not fully represented by the current config shape
- structured and union type names exist in config, but workflows that depend on type restoration should verify that import behavior in their target runtime

## Related Pages

- [Data Flow Between Nodes](data-flow-between-nodes.md)
- [Workflow Events](workflow-events.md)
- [Config And Export](config-and-export.md)
