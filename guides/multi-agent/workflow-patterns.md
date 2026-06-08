# Workflow Patterns

This page shows common multi-agent workflow shapes. The examples focus on topology: which agent runs, what it outputs, and how the next node is chosen.

## Sequential Pipeline

Use a sequential pipeline when each stage depends on the previous stage.

```csharp
var workflow = await AgentWorkflow.Create()
    .WithName("ResearchWriteReview")
    .AddAgent("research", researchConfig, node => node.WithOutputKey("research"))
    .AddAgent("write", writeConfig, node => node
        .WithInputKey("research")
        .WithOutputKey("draft"))
    .AddAgent("review", reviewConfig, node => node.WithInputKey("draft"))
    .From("research").To("write")
    .From("write").To("review")
    .BuildAsync();
```

Use this for draft -> review -> final, classify -> handle -> summarize, or extract -> transform -> validate.

## Parallel Fan-Out

Use fan-out when several agents can work from the same upstream input.

```csharp
var workflow = await AgentWorkflow.Create()
    .WithName("ParallelReview")
    .AddAgent("draft", draftConfig, node => node.WithOutputKey("draft"))
    .AddAgent("accuracy", accuracyConfig, node => node.WithInputKey("draft"))
    .AddAgent("style", styleConfig, node => node.WithInputKey("draft"))
    .From("draft").To("accuracy", "style")
    .BuildAsync();
```

The downstream nodes can run in the same layer once `draft` completes. Render layer events when the UI should show parallel progress.

## Fan-In Synthesis

Use fan-in when a final node should combine several upstream results.

```csharp
var workflow = await AgentWorkflow.Create()
    .WithName("ResearchSynthesis")
    .AddAgent("web", webResearchConfig, node => node.WithOutputKey("web_notes"))
    .AddAgent("docs", docsResearchConfig, node => node.WithOutputKey("doc_notes"))
    .AddAgent("synthesize", synthesizeConfig, node => node.WithInputTemplate("""
        Web notes:
        {{web_notes}}

        Documentation notes:
        {{doc_notes}}

        Write one answer from both sources.
        """))
    .From("START").To("web", "docs")
    .From("web", "docs").To("synthesize")
    .BuildAsync();
```

Use input templates for fan-in. They make the merge contract visible and keep the synthesizer from accidentally reading the original input instead of the upstream outputs.

## Field Router

Use field routing when one classifier should choose a path by structured output.

```csharp
var workflow = await AgentWorkflow.Create()
    .WithName("SupportTriage")
    .AddAgent("classify", classifierConfig, node => node
        .StructuredOutput<TicketClassification>())
    .AddAgent("billing", billingConfig)
    .AddAgent("technical", technicalConfig)
    .From("classify").To("billing").WhenEquals("intent", "billing")
    .From("classify").To("technical").WhenEquals("intent", "technical")
    .BuildAsync();
```

Use this when routing should be inspectable, testable, and serializable.

## Router Handoff

Use handoff routing when the router agent should choose from generated handoff tools.

```csharp
var workflow = await AgentWorkflow.Create()
    .WithName("SupportHandoff")
    .AddRouterAgent("router", routerConfig)
        .WithHandoff("billing", "Use for invoices, payments, or plan changes.")
        .WithHandoff("technical", "Use for bugs, errors, or setup problems.")
    .AddAgent("billing", billingConfig)
    .AddAgent("technical", technicalConfig)
    .BuildAsync();
```

Use this when natural-language target descriptions help the router choose the next specialist.

## Review Gate

Use a review gate when the workflow should inspect work before continuing.

```csharp
var workflow = await AgentWorkflow.Create()
    .WithName("DraftGate")
    .AddAgent("draft", draftConfig, node => node.WithOutputKey("draft"))
    .AddAgent("review", reviewConfig, node => node
        .WithInputKey("draft")
        .StructuredOutput<ReviewDecision>())
    .AddAgent("final", finalConfig, node => node.WithInputKey("draft"))
    .AddAgent("revise", reviseConfig, node => node.WithInputKey("draft"))
    .From("draft").To("review")
    .From("review").To("final").WhenEquals("approved", true)
    .From("review").To("revise").WhenEquals("approved", false)
    .BuildAsync();
```

Use structured output for the reviewer so route fields are stable.

## Approval Step

Use approval when a node result needs an application or user decision before the workflow uses it.

```csharp
.AddAgent("publish", publishConfig, node => node
    .WithInputKey("draft")
    .RequiresApproval("Approve publishing this draft?"))
```

Run with a `WorkflowEventCoordinator` when the app needs to approve or deny while streaming:

```csharp
var coordinator = new WorkflowEventCoordinator();

await foreach (var evt in workflow.ExecuteStreamingAsync(
    "Prepare the release note.",
    coordinator,
    cancellationToken))
{
    if (evt is NodeApprovalRequestEvent approval &&
        ShouldApprove(approval))
    {
        coordinator.Approve(approval.RequestId, "Looks good.");
    }
}
```

See [Bidirectional Events](../events/bidirectional-events.md) and [Permissions Middleware](../middleware/permissions.md) for the broader event-response pattern.

## Validator Loop

Use a bounded loop when an agent may need to revise until a validator passes.

```csharp
var workflow = await AgentWorkflow.Create()
    .WithName("ReviseUntilValid")
    .WithMaxIterations(4)
    .AddAgent("draft", draftConfig, node => node.WithOutputKey("draft"))
    .AddAgent("validate", validateConfig, node => node
        .WithInputKey("draft")
        .StructuredOutput<ValidationResult>())
    .AddAgent("revise", reviseConfig, node => node
        .WithInputKey("draft")
        .WithOutputKey("draft"))
    .From("draft").To("validate")
    .From("validate").To("revise").WhenEquals("passed", false)
    .From("revise").To("validate")
    .From("validate").To("END").WhenEquals("passed", true)
    .BuildAsync();
```

Keep loops bounded. Route fields should be simple and testable.

## Related Pages

- [Choose A Composition Pattern](choose-a-pattern.md)
- [Execution Model](execution-model.md)
- [Data Flow Between Nodes](data-flow-between-nodes.md)
- [Routing And Handoffs](routing-and-handoffs.md)
