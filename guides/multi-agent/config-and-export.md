# Config And Export

Multi-agent workflows have a serializable configuration shape for agents, edges, and settings. Treat this as a workflow definition format, not a snapshot of a running workflow.

## Minimal Shape

```json
{
  "name": "ReviewWorkflow",
  "version": "1.0.0",
  "agents": {
    "draft": {
      "agent": {
        "name": "Drafter",
        "systemInstructions": "Draft a concise answer."
      },
      "outputMode": "String",
      "outputKey": "draft"
    },
    "review": {
      "agent": {
        "name": "Reviewer",
        "systemInstructions": "Improve the draft."
      },
      "inputKey": "draft"
    }
  },
  "edges": [
    {
      "from": "draft",
      "to": "review"
    }
  ],
  "settings": {
    "enableMetrics": true
  }
}
```

Config can include:

- workflow name, description, and version
- agent node definitions
- per-node output mode, timeout, retry, input/output keys, input template, and additional instructions
- routing edges and declarative conditions
- workflow settings and iteration options
- optional conversation routing settings

An agent node's serializable `AgentRunConfig` may also contain `harnesses` overrides. Binding occurs when the node's agent is built and run, using that child agent's registered harness descriptors. Parent harness owner identities and runtime configuration bindings are never blindly forwarded.

See [Configure A Harness Per Run](../tools/configure-a-harness-per-run.md).

## Load From Config

```csharp
var workflow = await AgentWorkflow
    .FromFile("review-workflow.json")
    .BuildAsync();
```

Or build from an in-memory config object:

```csharp
var workflow = await new MultiAgentFactory()
    .BuildAsync(config);
```

## Export

`AgentWorkflowInstance.ExportConfigJson()` reconstructs a config document from a built workflow.

Export is useful for inspection, tests, generated examples, and config authoring workflows. It does not serialize live agent instances, service providers, delegates, chat clients, runtime state, or in-flight execution state.

## Round-Trip Boundaries

These pieces export cleanly:

- config-backed agent definitions
- node ids and regular graph edges
- string output keys and input keys/templates
- declarative edge conditions
- compound conditions and regex options
- retry, timeout, and selected node options

These pieces need care:

- prebuilt agent object state is not serialized
- predicate edge delegates are non-serializable runtime code
- executable harness dependencies and runtime configuration bindings are not serialized
- a node's harness override requires a compatible harness registration in that node's built agent
- structured and union type names are represented, but workflows that depend on full `Type` restoration should verify import behavior in their target runtime
- handoff target descriptions are not fully represented by the current node config shape
- error/fallback settings exist in config/export, but execution wiring should be validated before relying on them as runtime policy
- export uses camel-case property names

## Iteration Settings

Prefer `IterationOptions` for cyclic and change-aware workflows:

```json
{
  "Settings": {
    "IterationOptions": {
      "MaxIterations": 10,
      "UseChangeAwareIteration": true,
      "EnableAutoConvergence": true,
      "IgnoreFieldsForChangeDetection": ["timestamp"]
    }
  }
}
```

`WorkflowSettingsConfig.MaxIterations` also exists, but the clearest wired path is fluent `WithMaxIterations(...)` or `Settings.IterationOptions.MaxIterations`.

## Durable Execution

The workflow configuration shape does not contain a checkpoint-store setting. Durable
execution is registered separately as an HPD Base graph activation.

Use `AgentWorkflowInstance` for direct in-process execution. Use HPD Base and
`HPD-Agent.MultiAgent.HpdBase` when the workflow must survive process restart and be
executed by activation workers. See [Checkpointing](checkpointing.md).

Use `WithSessionStore(...)` with a [Conversation Policy](conversation-policies.md) when
node agent transcripts should be saved into HPD sessions and threads.

## Related Pages

- [Build A Multi-Agent Workflow](build-a-workflow.md)
- [Conversation Policies](conversation-policies.md)
- [Routing And Handoffs](routing-and-handoffs.md)
- [Workflow Events](workflow-events.md)
