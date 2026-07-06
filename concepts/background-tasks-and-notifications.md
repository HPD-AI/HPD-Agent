# Background Tasks, Handles, And Notifications

Background tasks let runtime code continue work after the active model turn has yielded. They are useful for long-running work such as command observation, indexing, monitors, subagent work, or maintenance tasks that should not block the current response.

This page covers the three pieces of HPD background work together because they are meant to be understood as one model:

```text
BackgroundTask
  records lifecycle and drives final-state events

BackgroundHandle
  exposes operations for a live resource after launch

Notification
  decides whether final-state task facts should wake the model
```

Keep these concepts separate in code, but learn them together. A task id is for lifecycle. A handle id is for control. A notification rule is for model wakeups.

Rule of thumb:

```text
Need only final completion? Use a BackgroundTask.
Need to operate it while alive? Add a BackgroundHandle.
```

## Function Vs Handle

A function is the capability the model can call. A handle is the live resource created by one call.

```text
Function
  the callable tool or capability

Handle
  one live instance created by a call
```

For example:

```text
ExecuteCommand
  function that can run, list, read, or stop commands

cmd_abc123
  handle for one background command process
```

Follow-up operations still call a function, but the handle id tells that function which live resource to operate:

```json
{
  "action": "ReadOutput",
  "backgroundHandleId": "cmd_abc123"
}
```

In short:

```text
Function = what can be done
Handle = which live thing to do it to
```

The runtime owns the lifecycle:

```text
background task registered
  -> BackgroundTaskStartedEvent
  -> task runs under runtime cancellation
  -> BackgroundTaskCompletedEvent / BackgroundTaskCancelledEvent / BackgroundTaskFaultedEvent
  -> notification rule decides whether to wake the model
  -> queued notification becomes hidden model context
```

Background tasks are not a separate permission flow. They are runtime-owned work with lifecycle events and optional model wakeups.

## Background Handles

Some background work is also a live resource that can be operated after launch. Use a background handle for that second layer.

```text
BackgroundTask
  records lifecycle and drives notifications

BackgroundHandle
  exposes status, read, stop, cancel, artifacts, or events for a live resource
```

For example, a background command registers both:

```text
BackgroundTask
  observes process completion and may wake the model

BackgroundHandle
  lets the model list the command, read output, stop it, and inspect artifacts
```

Function bodies can use `FunctionExecutionContext.BackgroundHandles` or `RegisterBackgroundHandle(...)` when the active runtime supports handles. A handle advertises supported operations with `BackgroundHandleOperation`, and lookup/list operations are scoped by session and optionally thread.

Do not use the background task id for handle operations. Handle-backed tools should return and accept a `handleId` or source-specific equivalent such as `backgroundHandleId`.

Handles are optional. Use a background task by itself for fire-and-complete work such as indexing, cleanup, report generation, or summarization. Add a handle only when the launched resource needs follow-up operations such as status, read, stop, cancel, artifacts, events, or list.

## Register Background Work

Middleware and function bodies can register background work when an active runtime exposes `IAgentBackgroundTaskRegistry`.

```csharp
var registration = context.BackgroundTasks!.RegisterBackgroundTask(
    new BackgroundTaskDescriptor
    {
        Name = "IndexWorkspace",
        SourceKind = BackgroundTaskSourceKind.ToolCall,
        SourceId = context.FunctionCallId,
        Invocation = context.InvocationSnapshot,
        Notification = new BackgroundTaskNotificationRule.OnFinalStateRule(
            Completed: true,
            Faulted: true)
    },
    async (backgroundContext, runtimeToken) =>
    {
        await IndexWorkspaceAsync(runtimeToken);
        backgroundContext.SetCompletion(
            summary: "Workspace index completed.",
            metadata: new Dictionary<string, string>
            {
                ["workspace"] = "default"
            });
    });
```

`BackgroundTaskDescriptor` describes the work. The runtime creates the task id, returns it in `BackgroundTaskRegistration`, emits lifecycle events, applies runtime cancellation, and dispatches selected final-state facts back into the model loop.

Use `BackgroundTaskContext.SetCompletion(...)` when the task has a useful final summary or metadata. `BackgroundTaskCompletedEvent.Summary` flows into the notification summary when the rule wakes the model.

Common source kinds include `ToolCall`, `Command`, `SubAgent`, `MultiAgent`, `Runtime`, `Maintenance`, and `Other`.

## Notification Rules

Each background task carries a `Notification` rule:

```csharp
Notification = BackgroundTaskNotificationRule.None
```

```csharp
Notification = new BackgroundTaskNotificationRule.OnFinalStateRule(
    Completed: true,
    Faulted: true,
    Cancelled: false)
```

```csharp
Notification = new BackgroundTaskNotificationRule.StrategyRule(
    "coding-command",
    Parameters: new Dictionary<string, string>
    {
        ["commandId"] = commandId
    },
    Fallback: new BackgroundTaskNotificationRule.OnFinalStateRule(
        Completed: true,
        Faulted: true))
```

`None` never wakes the model. `OnFinalStateRule` queues notifications only for selected final states. `StrategyRule` delegates the decision and notification content to a named source-specific strategy, with an optional fallback rule if the strategy is unavailable.

Keep source-specific semantics out of `HPD.Agent`. The core runtime should not know what a shell, process, test runner, PR URL, or output file means. Those decisions belong in the harness or package that owns the source.

## Dispatcher Behavior

The background task notification dispatcher handles delivery mechanics:

- subscribes to completed, cancelled, and faulted task events
- batches final-state events briefly
- resolves `sessionId` and `threadId`
- suppresses runtime shutdown cancellations
- suppresses duplicate final-state notifications for the same task id
- evaluates the task notification rule
- emits queued or suppressed notification events
- writes hidden notification input into the runtime inbox

Queued notifications become hidden system context with:

```text
source: BackgroundNotification
visibility: Hidden
persistence: ModelContextOnly
```

UIs should not render the hidden XML payload as assistant text. Use the lifecycle, queued, delivered, and suppressed events for visible status if the app needs to show background activity.

## Events

Background work uses these event families:

| Event | Meaning |
| --- | --- |
| `BackgroundTaskStartedEvent` | Background task began. |
| `BackgroundTaskCompletedEvent` | Background task completed successfully. |
| `BackgroundTaskCancelledEvent` | Background task observed cancellation. |
| `BackgroundTaskFaultedEvent` | Background task failed with an exception. |
| `BackgroundHandleRegisteredEvent` | A controllable background handle was registered. |
| `BackgroundHandleStatusChangedEvent` | A controllable background handle reported a status change. |
| `BackgroundTaskNotificationQueuedEvent` | A rule selected final-state facts for model delivery. |
| `BackgroundTaskNotificationSuppressedEvent` | A rule or runtime integrity check suppressed model delivery. |
| `BackgroundTaskNotificationDeliveredEvent` | A queued notification was delivered into a model turn. |

The event payload carries the full `notification` rule. Hosted thread-run projections expose a lighter `notification` summary:

```json
{
  "notification": {
    "kind": "strategy",
    "strategyName": "coding-command"
  }
}
```

Use live background task events when an app needs the full rule payload. Use the thread-run projection for run lists, dashboards, and status panes.

## Suppression Reasons

Suppressed events carry machine-readable reasons. Common reasons include:

| Reason | Meaning |
| --- | --- |
| `runtime-stopping-cancellation` | Runtime shutdown cancelled the task. |
| `missing-thread-scope` | The dispatcher could not resolve a session/thread. |
| `duplicate-final-state-notification` | The task already produced a final-state notification decision. |
| `runtime-input-closed` | The runtime input channel was closed before delivery. |
| `rule-suppressed:none:{status}` | The task used `None`. |
| `rule-suppressed:on-final-state:{status}` | The task reached a final state not selected by the rule. |
| `strategy-not-found:{name}` | A named strategy was unavailable and no fallback handled it. |
| `strategy-faulted:{name}:{exceptionType}` | A strategy threw while evaluating the task. |

Suppression is observable by design. It lets hosts and tests distinguish quiet expected behavior from lost work.

## TypeScript Shape

Hosted clients see rules as discriminated objects:

```typescript
type BackgroundTaskNotificationRule =
  | { kind: 'none' }
  | {
      kind: 'on_final_state';
      completed?: boolean;
      faulted?: boolean;
      cancelled?: boolean;
    }
  | {
      kind: 'strategy';
      name: string;
      parameters?: Record<string, string> | null;
      fallback?: BackgroundTaskNotificationRule | null;
    };
```

Treat the rule as descriptive runtime metadata. Do not infer product behavior from it unless your package owns the named strategy.

## Related Pages

- [Events Reference](../reference/events.md)
- [TypeScript Client Events](../guides/events/typescript-client.md)
- [Live Vs Durable Events](../guides/events/live-vs-durable-events.md)
- [Middleware Lifecycle](middleware-lifecycle.md)
