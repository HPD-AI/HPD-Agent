# Middleware Hook Fields

This reference lists the fields available to each `IAgentMiddleware` hook in
HPD-Agent 1.0.0. The exact context types are public; middleware should depend on
the fields documented here rather than internal agent state.

## Shared Hook Context

Turn, iteration, function, thread-fork, and error hooks derive from
`HookContext`. They share:

| Field | Purpose |
| --- | --- |
| `AgentName` | Current agent name |
| `ConversationId` | Session/conversation identity |
| `TraceId` | Trace identity shared by the turn |
| `ThreadExecutionId` | Accepted execution identity, when available |
| `MessageTurnId` | Durable message-turn identity |
| `Config` | Agent-level configuration |
| `ClientSet` | Resolved client-family instances |
| `SubAgentRunConfig` | Direct subagent run configuration, when applicable |
| `Session`, `SessionId` | Session metadata and identity |
| `Thread`, `ThreadId` | Current thread and identity |
| `Services` | Agent/input child-scope service provider |
| `ContentStore` | Configured content store |
| `RuntimeCapabilities` | Runtime-scoped capability registry |
| `EventFlows` | Interruptible event-flow registry |

Shared methods:

- `Analyze<T>(...)` reads loop state under the state lock.
- `UpdateState(...)` atomically updates core or multiple middleware states.
- `UpdateMiddlewareState<T>(...)` and `GetMiddlewareState<T>()` handle one state record.
- `PublishAsync(...)` publishes one-way events.
- `RequestAsync<TRequest,TResponse>(...)` publishes and waits for a response.
- `RunAsync(...)` submits runtime control input.

## Runtime Hooks

`BeforeStartContext`, `AfterStartedContext`, `BeforeStopContext`, and
`AfterStoppedContext` derive from `RuntimeHookContext`.

Shared runtime fields: `AgentName`, `Config`, `RunConfig`, `ClientSet`, `Services`,
`EventCoordinator`, `ThreadEvents`, `EventFlows`, `StructEvents`, `ContentStore`,
`RuntimeCapabilities`, `RuntimeId`, `CreatedAt`, `RuntimeCancellationToken`, and
`HasActiveRuntimeInputs`.

Shared runtime methods: `PublishAsync`, `RunAsync`,
`RegisterClientToolOperation`, `TryResolveClientToolOperation`,
`RegisterDisposable`, and `RegisterAsyncDisposable`.

| Hook | Additional fields | Writable control |
| --- | --- | --- |
| `BeforeStartAsync` | Shared runtime fields | `CancelStart`, `CancelReason` |
| `AfterStartedAsync` | `StartedAt` | None |
| `BeforeStopAsync` | `Reason` | `DrainPendingInputs`, `DrainTimeout` |
| `AfterStoppedAsync` | `Reason`, `StartedAt`, `StoppedAt`, `Duration`, `Error` | None |

## Input Hooks

Input hooks use a narrower surface than `HookContext`.

| Hook | Fields | Control |
| --- | --- | --- |
| `BeforeInputAsync` | `Input`, `Cancelled`, `CancelReason`, `AgentName`, `Config`, `Services` | `ReplaceInput(...)`, `CancelInput(...)` |
| `AfterInputAsync` | `Input`, `Result`, `Error`, `Cancelled`, `Duration`, `Cancellation`, `AgentName`, `Config`, `Services` | Observation only |

Session and thread identities carried by the input are available through
`context.Input.SessionId`, `context.Input.ThreadId`, and process-local
`Session`/`Thread` properties where the input type provides them.

## Message-Turn Hooks

| Hook | Fields | Mutation |
| --- | --- | --- |
| `BeforeMessageTurnAsync` | `SourceInput`, `TriggerSource`, `UserInputMessages`, `RuntimeContextMessages`, `ThreadHistory`, `RunConfig`, shared context fields | User input messages and thread history are mutable. The number of input messages cannot change. Runtime-context messages can be replaced individually with `ReplaceRuntimeContextMessage(...)`. |
| `AfterMessageTurnAsync` | `SourceInput`, `TriggerSource`, `UserInputMessages`, `RuntimeContextMessages`, `FinalResponse`, `TurnHistory`, `RunConfig`, `TurnUsage`, `IterationUsage`, shared context fields | `TurnHistory` is mutable before accounting closes and history is persisted. |

`AgentTurnTriggerSource` is one of `UserInput`, `BackgroundNotification`,
`SteeringContinuation`, `RuntimeContext`, or `Resume`.

## Iteration Hooks

| Hook | Fields | Control |
| --- | --- | --- |
| `BeforeIterationAsync` | `Iteration`, `Messages`, `Options`, `RunConfig`, `IsFirstIteration`, `PreviousIterationsUsage`, `PreviousIterationUsage`, shared context fields | `Messages` and `Options` are mutable. Set `SkipLLMCall` and supply `OverrideResponse` to bypass the model call. |
| `AfterIterationAsync` | `Iteration`, `ToolResults`, `RunConfig`, `AllToolsSucceeded`, `AnyToolFailed`, shared context fields | Observation and middleware-state updates. |

`AllToolsSucceeded` checks that every result has no exception.
`AnyToolFailed` reports whether at least one result carries an exception.

## Tool and Function Hooks

| Hook | Fields | Control |
| --- | --- | --- |
| `BeforeToolExecutionAsync` | `Response`, `ToolCalls`, `RunConfig`, shared context fields | Set `SkipToolExecution` and `OverrideResponse` to bypass every tool requested in the current model response. |
| `BeforeParallelBatchAsync` | `ParallelFunctions`, `BatchId`, `RunConfig`, shared context fields | Batch inspection only. Per-function control remains in `BeforeFunctionAsync`. |
| `BeforeFunctionAsync` | `Function`, `FunctionCallId`, `Invocation`, `ToolCallIndex`, `Arguments`, `InvocationMode`, `PermissionGrant`, `PermissionRequired`, `ToolHarnessName`, `SkillName`, `ClientToolOperations`, `RunConfig`, shared context fields | Set `BlockExecution` and `OverrideResult` for one function call. |
| `AfterFunctionAsync` | `Function`, `FunctionCallId`, `Invocation`, `ToolCallIndex`, `InvocationMode`, `OriginalResult`, `Result`, `Exception`, `ResultMetadata`, `CommittedToolBodyOperation`, `ToolHarnessName`, `SkillName`, `RunConfig`, shared context fields | `Result` and `Exception` are mutable. Helpers include `IsSuccess`, `IsFailure`, `IsSkillFunction`, and `IsToolHarnessFunction`. |

`BeforeFunctionContext.Function` may be null for an unknown or unavailable function.
`ClientToolOperations` is present only when the runtime owns client-tool operations.
`SkillName` is an informational attribution field and may be null for a
referenced function; use `ForSkill(...)` for reliable skill scoping.

### Parallel batch entries

Each `ParallelFunctionInfo` contains:

- `Function` and `FunctionName`
- `CallId`
- `Arguments`
- `Invocation`
- `ResolvedInvocation`

`ToolInvocationInfo` contains `BatchId`, `CallId`, `FunctionName`, and
`ToolCallIndex`.

## Model Wrapper

`WrapModelTurnStreamingAsync` receives `AgentModelTurnRequest`:

| Field | Purpose |
| --- | --- |
| `Transport` | `Chat` or `Realtime` |
| `ChatModel`, `RealtimeModel` | Selected transport client |
| `Messages`, `Options` | Provider-bound request |
| `State`, `Iteration` | Current loop snapshot and iteration |
| `EventFlows`, `EventCoordinator`, `EventPublisher` | Observation and event coordination |
| `Session`, `ContentStore`, `ClientSet`, `RunConfig` | Resolved invocation context |
| `StructEvents` | Process-local realtime struct events |

Return `null` to pass through. Otherwise return an async stream around
`handler(request)`. Side effects belong inside the iterator, not in wrapper setup.

`AgentModelUpdate` variants include chat updates, text and reasoning deltas, audio
deltas, input transcripts, tool-call updates, response lifecycle updates, and
usage updates.

## Function Wrapper

`WrapFunctionCallAsync` receives an immutable `FunctionRequest`:

| Field | Purpose |
| --- | --- |
| `Function`, `FunctionName`, `CallId` | Invocation identity |
| `Arguments` | Immutable argument dictionary |
| `State`, `RunConfig`, `SubAgentRunConfig` | Agent and run context |
| `Invocation`, `InvocationMode` | Batch and action/invocation-mode facts |
| `PermissionGrant`, `PermissionRequired` | Permission admission facts |
| `ResultMetadata` | Mutable per-call metadata side channel |
| `ToolHarnessName`, `SkillName` | Capability attribution |
| `CancellationToken` | Optional timeout/retry cancellation override |
| `EventCoordinator`, `EventPublisher`, `StructEvents` | Observation surfaces |

Use `request.Override(function: ..., arguments: ...)` to create a modified request
without mutating the original.

## Thread Fork

`BeforeThreadForkCommitContext` provides:

- `SourceThread`
- `TargetThread`, before persistence
- `ForkedAtMessageIndex`
- `ForkedAtMessageId`
- `ForkOptions`
- `HistoricalEvents`, writable by framework-approved middleware history mutations
- Shared context fields

Framework-owned target identity, topology, registry, grants, and operation facts
cannot be replaced.

## Error Hook

`ErrorContext` provides:

| Field | Purpose |
| --- | --- |
| `Error` | Original terminal exception |
| `Source` | Typed `ErrorSource` |
| `Iteration` | Current iteration when the failure occurred |
| `RunConfig` | Associated run configuration, when available |
| `ErrorDetails`, `ErrorCategory` | Provider-aware classification |
| `IsModelError`, `IsToolError`, `IsIterationError`, `IsMessageTurnError` | Turn-path helpers |
| `IsInputError`, `IsRuntimeError`, `IsThreadForkError` | Lifecycle-path helpers |
| `IsModelNotFoundError`, `IsRetryableError` | Classification helpers |
| Shared context fields and state/event methods | Observation and policy |

`OnErrorAsync` is observation-first. The original failure still propagates after
all handlers complete.

## ToolHarness Lifecycle

| Hook | Fields |
| --- | --- |
| `OnHarnessActivatedAsync` | `HarnessIdentity`, `InputExecutionId`, `HarnessConfiguration`, `Services`, `RunConfig`, `SessionId`, `ThreadId` |
| `OnHarnessExpandedAsync` | `HarnessIdentity`, `InputExecutionId`, `Reason`, `ExpansionCallId`, `RevealedFunctions` |
| `OnHarnessDeactivatingAsync` | `HarnessIdentity`, `InputExecutionId`, `Reason` |

`ToolHarnessActivationContext` also provides:

- `GetHarnessConfiguration<T>()`
- `GetConfiguration<T>(...)`
- `GetConfigurationOrDefault<T>(...)`
- `GetService<T>()` and `GetRequiredService<T>()`
- `GetRequiredServicesOwned<TMiddleware>()`
- `GetRequiredAgentResource<T>()`
- `GetCanonicalWorkspaceIdentity()`
