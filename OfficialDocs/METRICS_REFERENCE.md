# HPD-Agent Metrics Reference

Complete reference for all OpenTelemetry metrics exposed by HPD-Agent.

## Metric Naming Convention

All HPD-Agent metrics follow the pattern:
```
hpd.agent.<category>.<metric_name>
```

**Categories:**
- `state` - State management and message tracking
- `permission` - Permission system
- `tools` - Tool execution
- `filters` - Filter pipelines
- `containers` - Container expansion
- `delta_sending` - Delta sending optimization
- `retry` - Retry system
- `reduction` - History reduction
- `documents` - Document processing
- `nested` - Nested agent calls
- `preparation` - Message preparation
- `checkpoint` - Checkpoint operations
- `plan_mode` - Plan mode
- `events` - Bidirectional events

---

## State Management Metrics

### `hpd.agent.state.message_count`
**Type:** Histogram
**Unit:** messages
**Description:** Distribution of message counts in AgentLoopState.CurrentMessages

**Tags:**
- `agent.name` - Agent name
- `iteration` - Iteration number
- `source` - Execution phase (BeforeLLM, AfterLLM, AfterTools, BeforeFinalization)

**Use Case:** Track state growth, detect bloat

**Example Prometheus Query:**
```promql
# P95 state message count
histogram_quantile(0.95, hpd_agent_state_message_count)

# State message count by source
hpd_agent_state_message_count{source="BeforeLLM"}
```

---

### `hpd.agent.turn_history.message_count`
**Type:** Histogram
**Unit:** messages
**Description:** Distribution of message counts in turnHistory

**Tags:**
- `agent.name` - Agent name
- `iteration` - Iteration number
- `source` - Execution phase

**Use Case:** Compare with state message count to detect divergence

---

### `hpd.agent.state.divergence`
**Type:** Counter
**Unit:** warnings
**Description:** Counts state divergence warnings (state count != expected turn count)

**Tags:**
- `agent.name` - Agent name
- `iteration` - Iteration number
- `source` - Where divergence detected

**Use Case:** Alert on state corruption bugs

**Alert Rule:**
```yaml
- alert: StateDivergence
  expr: increase(hpd_agent_state_divergence[5m]) > 0
  severity: critical
```

---

## Delta Sending Metrics

### `hpd.agent.delta_sending.activations`
**Type:** Counter
**Unit:** activations
**Description:** Number of times delta sending was activated

**Tags:**
- `agent.name` - Agent name
- `conversation.id` - Conversation ID from LLM

**Use Case:** Track delta sending adoption rate

---

### `hpd.agent.delta_sending.message_count`
**Type:** Histogram
**Unit:** messages
**Description:** Distribution of message counts sent in delta mode

**Tags:**
- `agent.name` - Agent name

**Use Case:** Measure delta sending effectiveness (should be low)

**Example:**
```promql
# Average messages sent in delta mode
avg(hpd_agent_delta_sending_message_count)
```

---

## Permission System Metrics

### `hpd.agent.permission.checks`
**Type:** Counter
**Unit:** checks
**Description:** Total number of permission checks performed

**Tags:**
- `agent.name` - Agent name
- `function.name` - Function being checked
- `result` - "approved" or "denied"

**Use Case:** Track permission check volume

---

### `hpd.agent.permission.duration`
**Type:** Histogram
**Unit:** milliseconds
**Description:** Permission check duration

**Tags:**
- `agent.name` - Agent name
- `function.name` - Function name

**Use Case:** Detect slow permission filters

**Alert Rule:**
```yaml
- alert: SlowPermissionCheck
  expr: histogram_quantile(0.95, hpd_agent_permission_duration) > 1000
  severity: warning
```

---

### `hpd.agent.permission.denials`
**Type:** Counter
**Unit:** denials
**Description:** Number of permission denials

**Tags:**
- `agent.name` - Agent name
- `function.name` - Function name
- `denial_reason` - Reason for denial

**Use Case:** Monitor security policy effectiveness

**Example:**
```promql
# Denial rate
rate(hpd_agent_permission_denials[5m]) / rate(hpd_agent_permission_checks[5m])
```

---

## Tool Execution Metrics

### `hpd.agent.tools.parallel_executions`
**Type:** Counter
**Unit:** executions
**Description:** Number of parallel tool execution batches

**Tags:**
- `agent.name` - Agent name
- `tool_count` - Total tools in batch
- `parallel_count` - Tools executed in parallel

**Use Case:** Track parallelization effectiveness

---

### `hpd.agent.tools.parallel_batch_size`
**Type:** Histogram
**Unit:** tools
**Description:** Distribution of parallel tool batch sizes

**Tags:**
- `agent.name` - Agent name

**Use Case:** Measure concurrency levels

**Example:**
```promql
# Average batch size
avg(hpd_agent_tools_parallel_batch_size)
```

---

### `hpd.agent.tools.semaphore_wait`
**Type:** Histogram
**Unit:** milliseconds
**Description:** Time tools wait for semaphore slots (contention metric)

**Tags:**
- `agent.name` - Agent name

**Use Case:** Detect semaphore bottlenecks

**Alert Rule:**
```yaml
- alert: HighToolContention
  expr: histogram_quantile(0.95, hpd_agent_tools_semaphore_wait) > 5000
  severity: warning
```

---

## Filter Pipeline Metrics

### `hpd.agent.filters.pipeline_executions`
**Type:** Counter
**Unit:** executions
**Description:** Number of filter pipeline executions

**Tags:**
- `agent.name` - Agent name
- `filter_type` - "Prompt", "AIFunction", "Permission", "MessageTurn"

**Use Case:** Track filter usage

---

### `hpd.agent.filters.pipeline_duration`
**Type:** Histogram
**Unit:** milliseconds
**Description:** Filter pipeline execution duration

**Tags:**
- `agent.name` - Agent name
- `filter_type` - Filter type
- `filter_count` - Number of filters in pipeline

**Use Case:** Identify slow filter pipelines

**Example:**
```promql
# P95 duration by filter type
histogram_quantile(0.95, hpd_agent_filters_pipeline_duration) by (filter_type)
```

---

### `hpd.agent.filters.exceptions`
**Type:** Counter
**Unit:** exceptions
**Description:** Number of exceptions during filter execution

**Tags:**
- `agent.name` - Agent name
- `filter_type` - Filter type
- `exception_type` - Exception class name

**Use Case:** Alert on filter failures

---

## Container Expansion Metrics

### `hpd.agent.containers.expansions`
**Type:** Counter
**Unit:** expansions
**Description:** Number of container expansions (plugin/skill scoping)

**Tags:**
- `agent.name` - Agent name
- `container_type` - "Plugin" or "Skill"
- `container_name` - Container name

**Use Case:** Track scoping usage

---

### `hpd.agent.containers.member_count`
**Type:** Histogram
**Unit:** members
**Description:** Number of member functions exposed per expansion

**Tags:**
- `agent.name` - Agent name
- `container_type` - Container type

**Use Case:** Measure scoping effectiveness

---

## Retry System Metrics

### `hpd.agent.retry.attempts`
**Type:** Counter
**Unit:** attempts
**Description:** Number of retry attempts

**Tags:**
- `agent.name` - Agent name
- `function.name` - Function being retried
- `error_category` - "RateLimit", "Timeout", "Network", etc.
- `attempt_number` - Attempt number (1-based)

**Use Case:** Track retry patterns by error category

---

### `hpd.agent.retry.delay`
**Type:** Histogram
**Unit:** milliseconds
**Description:** Retry delay durations (backoff effectiveness)

**Tags:**
- `agent.name` - Agent name
- `error_category` - Error category

**Use Case:** Measure backoff strategy effectiveness

---

### `hpd.agent.retry.exhaustion`
**Type:** Counter
**Unit:** exhaustions
**Description:** Number of retry exhaustion events (all retries failed)

**Tags:**
- `agent.name` - Agent name
- `function.name` - Function name
- `final_error_category` - Final error category

**Use Case:** Alert on persistent failures

---

## History Reduction Metrics

### `hpd.agent.reduction.cache_hits`
**Type:** Counter
**Unit:** hits
**Description:** Number of reduction cache hits

**Tags:**
- `agent.name` - Agent name

**Use Case:** Track cache effectiveness

---

### `hpd.agent.reduction.cache_misses`
**Type:** Counter
**Unit:** misses
**Description:** Number of reduction cache misses

**Tags:**
- `agent.name` - Agent name

**Use Case:** Calculate cache hit rate

**Example:**
```promql
# Cache hit rate
rate(hpd_agent_reduction_cache_hits[5m]) /
(rate(hpd_agent_reduction_cache_hits[5m]) + rate(hpd_agent_reduction_cache_misses[5m]))
```

---

### `hpd.agent.reduction.token_savings`
**Type:** Histogram
**Unit:** tokens
**Description:** Token savings from reduction (original - reduced)

**Tags:**
- `agent.name` - Agent name
- `cache_hit` - "true" or "false"

**Use Case:** Measure reduction effectiveness

---

## Document Processing Metrics

### `hpd.agent.documents.processing`
**Type:** Counter
**Unit:** operations
**Description:** Number of document processing operations

**Tags:**
- `agent.name` - Agent name
- `document_count` - Number of documents processed

**Use Case:** Track document processing volume

---

### `hpd.agent.documents.duration`
**Type:** Histogram
**Unit:** milliseconds
**Description:** Document processing duration

**Tags:**
- `agent.name` - Agent name

**Use Case:** Measure document processing performance

---

## Nested Agent Metrics

### `hpd.agent.nested.invocations`
**Type:** Counter
**Unit:** invocations
**Description:** Number of nested agent invocations

**Tags:**
- `orchestrator_agent` - Orchestrator agent name
- `nested_agent` - Nested agent name

**Use Case:** Track multi-agent patterns

---

### `hpd.agent.nested.depth`
**Type:** Histogram
**Unit:** levels
**Description:** Nesting depth distribution

**Tags:**
- `orchestrator_agent` - Orchestrator agent name

**Use Case:** Detect deep nesting issues

---

## Message Preparation Metrics

### `hpd.agent.preparation.duration`
**Type:** Histogram
**Unit:** milliseconds
**Description:** Message preparation phase duration

**Tags:**
- `agent.name` - Agent name
- `reduction_applied` - "true" or "false"

**Use Case:** Measure preparation overhead

---

## Checkpoint Metrics

### `hpd.agent.checkpoint.operations`
**Type:** Counter
**Unit:** operations
**Description:** Number of checkpoint and resume operations

**Tags:**
- `thread.id` - Thread ID
- `operation` - "checkpoint" or "resume"
- `success` - "true" or "false"

**Use Case:** Track checkpoint reliability

---

### `hpd.agent.checkpoint.size`
**Type:** Histogram
**Unit:** bytes
**Description:** Checkpoint size distribution

**Tags:**
- `thread.id` - Thread ID

**Use Case:** Monitor checkpoint storage requirements

---

## Plan Mode Metrics

### `hpd.agent.plan_mode.activations`
**Type:** Counter
**Unit:** activations
**Description:** Number of plan mode activations

**Tags:**
- `agent.name` - Agent name
- `operation` - "created", "updated", "completed"

**Use Case:** Track plan mode adoption

---

### `hpd.agent.plan_mode.operations`
**Type:** Counter
**Unit:** operations
**Description:** Number of plan operations

**Tags:**
- `agent.name` - Agent name
- `plan_id` - Plan identifier
- `operation_type` - Operation type

**Use Case:** Track plan lifecycle

---

## Bidirectional Event Metrics

### `hpd.agent.events.bidirectional`
**Type:** Counter
**Unit:** events
**Description:** Number of bidirectional events (permissions, clarifications)

**Tags:**
- `agent.name` - Agent name
- `event_type` - "PermissionRequest", "ClarificationRequest", etc.
- `direction` - "request" or "response"

**Use Case:** Track event flow patterns

---

### `hpd.agent.events.response_duration`
**Type:** Histogram
**Unit:** milliseconds
**Description:** Time waiting for event responses

**Tags:**
- `agent.name` - Agent name
- `event_type` - Event type

**Use Case:** Measure user response times

---

## Metric Cardinality

**Cardinality Impact** (number of unique time series):

| Metric | Cardinality | Notes |
|--------|-------------|-------|
| State metrics | Low (< 100) | Limited by agent count × iterations |
| Permission metrics | Medium (< 1000) | Agent × function combinations |
| Filter metrics | Low (< 50) | Only 4 filter types |
| Tool metrics | Medium (< 500) | Agent × batch size |
| Retry metrics | Medium (< 1000) | Agent × function × error category |
| Reduction metrics | Low (< 50) | Only agent name tag |
| Nested metrics | High (> 1000) | Agent × nested agent combinations |

**Recommendation:** All metrics are safe for production use with proper tag pruning.

---

## Example Grafana Dashboard

```json
{
  "dashboard": {
    "title": "HPD-Agent Observability",
    "panels": [
      {
        "title": "Permission Denial Rate",
        "targets": [
          {
            "expr": "rate(hpd_agent_permission_denials[5m]) / rate(hpd_agent_permission_checks[5m])"
          }
        ]
      },
      {
        "title": "Filter Pipeline P95 Duration",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, hpd_agent_filters_pipeline_duration) by (filter_type)"
          }
        ]
      },
      {
        "title": "State Divergence Warnings",
        "targets": [
          {
            "expr": "increase(hpd_agent_state_divergence[1h])"
          }
        ]
      }
    ]
  }
}
```

---

## Related Documentation

- [Observability Guide](./OBSERVABILITY_GUIDE.md) - How to configure logging and telemetry
- [Debugging Scenarios](./DEBUGGING_SCENARIOS.md) - Using metrics to debug issues
