# Thread Execution Lifecycle

A thread execution is one accepted input that owns a thread's exclusive execution slot. It is different from a message turn, model request, tool call, workflow run, and the long-lived thread runtime.

## From Input To Finish

The coordinating runtime follows one ordered lifecycle:

```text
submit input
  -> reserve the thread and assign ThreadExecutionId
  -> commit THREAD_EXECUTION_STARTED
  -> dispatch the input through HPD Agent
  -> commit THREAD_EXECUTION_FINISHED
  -> release the thread
```

A thread with an active execution rejects overlapping queued work as busy. Active controls target that execution immediately and never create another thread execution. Different threads can execute concurrently.

The finish event has exactly one terminal outcome:

| Outcome | Error | Meaning |
| --- | --- | --- |
| `Succeeded` | none | The accepted input completed normally. |
| `Cancelled` | none | Cancellation ended the accepted input. |
| `Failed` | required `ThreadExecutionError` | Execution ended with a structured failure. |

`FinishedAt` records when the accepted input became terminal. The start and finish correlate through the same `ThreadExecutionId`.

The coordinator attempts to commit the finish event before releasing ownership. In `0.9.5`, release removes the in-process lease in a `finally` block even if finish persistence fails. A failed finish therefore leaves a committed start without a terminal fact; historical projection reports that execution as `Interrupted`, while the thread is available for new work.

## Live Ownership And Durable History

`activeExecution` is the runtime coordinator's authoritative present-tense ownership state. `THREAD_EXECUTION_STARTED` and `THREAD_EXECUTION_FINISHED` are durable historical facts used by projections and rehydration.

Replaying an old start event does not reserve the live thread. If recovery finds a committed start without a matching finish, historical execution projection reports `Interrupted`. `Interrupted` describes recovery from an incomplete historical pair; it is not a normal `ThreadExecutionOutcome` emitted by a functioning coordinator.

This separation matters for clients:

- use execution events to render history;
- use `activeExecution` to decide whether the thread is busy;
- use the live active execution id as steering and interruption authority;
- never infer present ownership from journal replay alone.

## Control An Active Execution Safely

Steering is a `UserMessagesInputEvent` with `Delivery = AgentInputDelivery.Steer` and the active `ThreadExecutionId`. Exact-execution cancellation is submitted through `IThreadExecutionController.CancelAsync(...)` with the same execution id. Both compare the requested id with the current live execution before dispatching:

| `AgentInputDisposition` | Meaning |
| --- | --- |
| `Accepted` | The matching active execution owns the control. |
| `NoActiveExecution` | The thread has no active execution. |
| `ActiveExecutionMismatch` | A different execution currently owns the thread. |
| `ActiveInputNotSteerable` | Steering targeted an active input that is not a message execution. |
| `ExecutionFinishing` | The active execution has closed steering acceptance. |

Do not automatically retry a mismatch. The reported active execution may be newer work started by another client.

Accepted steering is committed as ordinary thread-history messages with `AgentMessageSource.Steering`, then becomes visible to the same execution before its next model request. It does not emit another execution start or finish.

## Background Correlation

Work created during an execution may carry `OriginatingThreadExecutionId` after the execution has released the thread. This identifies provenance only. It does not extend ownership or make the originating execution active again.

## Hosted Projection

Hosting exposes two separate views:

- `/executions` returns historical thread executions;
- thread `/state` returns the live `activeExecution` and pending requests.

On restart or reconnection, rebuild transcript and historical status from committed events, then reconcile present-tense controls with authoritative runtime state. See [Hosted Lifecycle And Recovery](../hosting/hosted-lifecycle-and-recovery.md) and [Canonical Thread Journal](canonical-thread-journal.md).
