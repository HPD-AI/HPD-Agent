# HPD-Agent Observability Guide

## Table of Contents
- [Overview](#overview)
- [Quick Start](#quick-start)
- [Logging Configuration](#logging-configuration)
- [Telemetry Configuration](#telemetry-configuration)
- [Log Levels Explained](#log-levels-explained)
- [Sensitive Data Control](#sensitive-data-control)
- [Available Metrics](#available-metrics)
- [Common Debugging Scenarios](#common-debugging-scenarios)
- [Performance Considerations](#performance-considerations)
- [Production Best Practices](#production-best-practices)

---

## Overview

HPD-Agent provides **two complementary layers** of observability:

### 🔷 **Microsoft's LLM-Level Observability** (Built-in)
- `LoggingChatClient` - Logs LLM requests/responses
- `OpenTelemetryChatClient` - Tracks tokens, duration, errors

### 🔷 **HPD-Agent Orchestration Observability** (This Guide)
- `AgentLoggingService` - Logs agent-specific events
- `AgentTelemetryService` - Tracks orchestration metrics

**Key Insight:** Microsoft's middleware logs **what the LLM sees**. HPD-Agent's observability logs **what happens in the agent orchestration layer** (state management, filters, permissions, tools).

---

## Quick Start

### Enable Logging (appsettings.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",

      // 🔷 Microsoft's LLM Logging (what goes to/from LLM)
      "Microsoft.Extensions.AI.LoggingChatClient": "Trace",

      // 🔷 HPD-Agent Orchestration Logging (NEW)
      "HPD.Agent.AgentLoggingService": "Debug"
    }
  }
}
```

### Enable Telemetry (Program.cs)

```csharp
var agentConfig = new AgentConfig
{
    Name = "AI Assistant",
    // ... other config ...
};

var agent = new AgentBuilder(agentConfig)
    .WithLogging()        // ✅ Enable logging
    .WithTelemetry()      // ✅ Enable telemetry (OpenTelemetry metrics)
    .Build();
```

**That's it!** You now have full observability enabled.

---

## Logging Configuration

### Available Log Levels (Granularity Control)

HPD-Agent logging supports **fine-grained control** over what gets logged:

| Log Level | What You See | Use Case | Overhead |
|-----------|--------------|----------|----------|
| **None** | Nothing | Production (minimal observability) | 0% |
| **Critical** | Only critical failures | Production (error-only) | < 0.1% |
| **Error** | Errors + critical | Production (error tracking) | < 0.1% |
| **Warning** | Warnings + errors | Production (issues detection) | < 0.2% |
| **Information** | Key lifecycle events | Production (recommended) | < 0.3% |
| **Debug** | Detailed orchestration | Staging/Development | < 0.5% |
| **Trace** | Full message content | Development/Debugging only | < 1% |

### Configuration Examples

#### 🔷 **Production (Minimal Overhead)**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "HPD.Agent.AgentLoggingService": "Information"
    }
  }
}
```

**What you get:**
- ✅ Message turn start/end
- ✅ Permission requests/denials
- ✅ Plan mode activation
- ✅ Checkpoint operations
- ❌ No message content
- ❌ No iteration details
- ❌ No filter pipeline details

**Example output:**
```
[Info] Agent 'AI Assistant' message turn msg_123 started: conversation=conv_456, messages=25
[Info] Agent 'AI Assistant' message turn msg_123 completed: iterations=3, final_messages=7
[Warning] Agent 'AI Assistant' permission check for 'DeleteFile': ❌ Denied (User denied permission) [12ms]
```

---

#### 🔷 **Development (Detailed Orchestration)**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HPD.Agent.AgentLoggingService": "Debug",
      "Microsoft.Extensions.AI.LoggingChatClient": "Debug"
    }
  }
}
```

**What you get:**
- ✅ All Information-level logs
- ✅ State snapshots (counts only)
- ✅ Iteration message counts
- ✅ Filter pipeline execution
- ✅ Container expansions
- ✅ Permission check details
- ❌ No full message content

**Example output:**
```
[Info] Agent 'AI Assistant' message turn msg_123 started: conversation=conv_456, messages=25
[Debug] Agent 'AI Assistant' iteration 0: Sending 10 messages to LLM
[Debug] Agent 'AI Assistant' iteration 0 [BeforeLLM] State=10, Turn=0, Last=TextContent
[Debug] Agent 'AI Assistant' executed Prompt pipeline: 3 filters [45ms]
[Debug] Agent 'AI Assistant' permission check for 'ReadFile': ✅ Approved (None) [8ms]
[Debug] Agent 'AI Assistant' iteration 0: Executed 2 tools (batch=2, approved=2, denied=0) [234ms]
```

---

#### 🔷 **Deep Debugging (Full Visibility)**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HPD.Agent.AgentLoggingService": "Trace",
      "Microsoft.Extensions.AI.LoggingChatClient": "Trace"
    }
  }
}
```

**⚠️ WARNING:** Trace level logs **full message content**. Only enable when:
- You have `EnableSensitiveData = true`
- You're debugging in a development environment
- You understand privacy implications

**What you get:**
- ✅ All Debug-level logs
- ✅ **Full message content** at critical points
- ✅ Complete conversation history per turn
- ✅ Exact messages sent to LLM per iteration

**Example output:**
```
[Info] Agent 'AI Assistant' message turn msg_123 started: conversation=conv_456, messages=25
[Trace] Agent 'AI Assistant' message turn msg_123 input messages: [{"role":"user","content":"Analyze Q4 earnings"}, ...]

[Debug] Agent 'AI Assistant' iteration 0: Sending 10 messages to LLM
[Trace] Agent 'AI Assistant' iteration 0 LLM messages: [{"role":"assistant","content":"Summary: ..."}, {"role":"user","content":"Analyze Q4"}]

[Trace] Agent 'AI Assistant' iteration 0 [BeforeLLM] Full Messages: [{"role":"assistant","content":"..."}]
```

---

### Selective Logging by Component

You can enable different log levels for different components:

```json
{
  "Logging": {
    "LogLevel": {
      // General agent logging
      "HPD.Agent.AgentLoggingService": "Information",

      // LLM-level logging (Microsoft)
      "Microsoft.Extensions.AI.LoggingChatClient": "Debug",

      // Other components
      "HPD.Agent.PermissionManager": "Warning",
      "HPD.Agent.ToolScheduler": "Debug"
    }
  }
}
```

---

## Sensitive Data Control

### EnableSensitiveData Flag

By default, **full message content is NOT logged** for privacy. To enable:

```csharp
var agent = new AgentBuilder(agentConfig)
    .WithLogging(enableSensitiveData: true)  // ⚠️ Enable full message logging
    .Build();
```

**Or via environment variable:**

```bash
export OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true
```

### What Gets Logged at Each Level

| EnableSensitiveData | Log Level | What's Logged |
|---------------------|-----------|---------------|
| `false` (default) | Information | Message counts, summaries, no content |
| `false` | Debug | + Message types (TextContent, FunctionCall) |
| `false` | Trace | + Message types only |
| `true` | Information | Message counts, summaries, no content |
| `true` | Debug | + Message types |
| `true` | Trace | + **Full message content** ⚠️ |

### Privacy-Safe Logging Pattern

```csharp
// ✅ PRODUCTION: Safe - no sensitive data
var agent = new AgentBuilder(agentConfig)
    .WithLogging(enableSensitiveData: false)  // Default
    .Build();
```

```json
{
  "Logging": {
    "LogLevel": {
      "HPD.Agent.AgentLoggingService": "Information"  // Safe level
    }
  }
}
```

**Output:**
```
[Info] Agent 'AI Assistant' message turn started: messages=25
[Info] Agent 'AI Assistant' message turn completed: iterations=3, final_messages=7
```

---

## Telemetry Configuration

### Enable OpenTelemetry Metrics

```csharp
var agent = new AgentBuilder(agentConfig)
    .WithTelemetry(enableSensitiveData: false)  // Metrics don't log message content
    .Build();
```

### Export Metrics to Prometheus

```csharp
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("HPD.Agent")  // ✅ HPD-Agent metrics
            .AddPrometheusExporter();
    });

var app = builder.Build();
app.MapPrometheusScrapingEndpoint();  // Expose /metrics endpoint
app.Run();
```

### View Metrics

```bash
# Prometheus scraping endpoint
curl http://localhost:9090/metrics | grep hpd.agent

# Example output:
hpd_agent_state_message_count{agent_name="AI Assistant",iteration="0",source="BeforeLLM"} 10
hpd_agent_permission_checks{agent_name="AI Assistant",result="approved"} 15
hpd_agent_permission_denials{agent_name="AI Assistant"} 2
hpd_agent_filters_pipeline_duration{agent_name="AI Assistant",filter_type="Prompt"} 45
```

---

## Available Metrics

### State Management Metrics
```
hpd.agent.state.message_count           # Message count in state
hpd.agent.turn_history.message_count    # Message count in history
hpd.agent.state.divergence              # State corruption warnings
```

### Permission System Metrics
```
hpd.agent.permission.checks             # Total permission checks
hpd.agent.permission.duration           # Permission check duration
hpd.agent.permission.denials            # Permission denials
```

### Tool Execution Metrics
```
hpd.agent.tools.parallel_executions     # Parallel tool batches
hpd.agent.tools.parallel_batch_size     # Tools per batch
hpd.agent.tools.semaphore_wait          # Semaphore contention
```

### Filter Pipeline Metrics
```
hpd.agent.filters.pipeline_executions   # Filter pipeline runs
hpd.agent.filters.pipeline_duration     # Filter pipeline duration
hpd.agent.filters.exceptions            # Filter exceptions
```

### Advanced Metrics
```
hpd.agent.delta_sending.activations     # Delta sending activations
hpd.agent.retry.attempts                # Retry attempts
hpd.agent.reduction.cache_hits          # History reduction cache hits
hpd.agent.nested.invocations            # Nested agent calls
hpd.agent.checkpoint.operations         # Checkpoint/resume ops
```

**See [METRICS_REFERENCE.md](./METRICS_REFERENCE.md) for complete list.**

---

## Common Debugging Scenarios

### 🔍 **Scenario 1: Agent Responses Missing Context**

**Symptom:** Agent doesn't remember recent conversation

**Enable:**
```json
{
  "Logging": {
    "LogLevel": {
      "HPD.Agent.AgentLoggingService": "Debug"
    }
  }
}
```

**Look for:**
```bash
# Check message turn start
grep "LogMessageTurnStart" agent.log

# Output shows:
[Info] Agent 'AI Assistant' message turn started: messages=100

# Check iteration 0
grep "LogIterationMessages.*iteration 0" agent.log

# Output shows:
[Debug] Agent 'AI Assistant' iteration 0: Sending 10 messages to LLM

# ✅ Diagnosis: History reduced from 100 → 10 messages
# Check if reduction summary is complete
```

---

### 🔍 **Scenario 2: State Divergence Bug**

**Symptom:** Agent hallucinations, duplicate/missing messages

**Enable:**
```json
{
  "Logging": {
    "LogLevel": {
      "HPD.Agent.AgentLoggingService": "Debug"
    }
  }
}
```

**Look for:**
```bash
grep "DIVERGENCE DETECTED" agent.log

# Output:
[Warning] Agent 'AI Assistant' iteration 2 [AfterTools] State=10, Turn=8 ⚠️ DIVERGENCE DETECTED

# ✅ Diagnosis: State corruption - state has 2 extra messages
```

---

### 🔍 **Scenario 3: Permission Bottleneck**

**Symptom:** Slow agent iterations

**Enable:**
```json
{
  "Logging": {
    "LogLevel": {
      "HPD.Agent.AgentLoggingService": "Debug"
    }
  }
}
```

**Look for:**
```bash
grep "LogPermissionCheck" agent.log | grep "ms\]$"

# Output shows:
[Debug] Agent 'AI Assistant' permission check for 'DatabaseQuery': ✅ Approved [5234ms]

# ✅ Diagnosis: Permission check taking 5 seconds - slow filter
```

---

### 🔍 **Scenario 4: Delta Sending Not Working**

**Symptom:** Agent performance degraded after first iteration

**Enable:**
```json
{
  "Logging": {
    "LogLevel": {
      "HPD.Agent.AgentLoggingService": "Debug"
    }
  }
}
```

**Look for:**
```bash
# Check iteration message counts
grep "LogIterationMessages" agent.log

# Output:
[Debug] Agent 'AI Assistant' iteration 0: Sending 25 messages to LLM
[Debug] Agent 'AI Assistant' iteration 1: Sending 27 messages to LLM

# Expected: Only 2 new messages in iteration 1 (delta mode)
# Actual: 27 messages (full history)

# Check delta activation
grep "LogDeltaSendingActivated" agent.log
# Output: (empty)

# ✅ Diagnosis: LLM service didn't return ConversationId, delta mode never activated
```

---

## Performance Considerations

### Overhead by Log Level

Based on benchmarks:

| Configuration | Overhead | Use Case |
|---------------|----------|----------|
| Logging disabled | 0% | Not recommended |
| Information level | < 0.3% | ✅ Production default |
| Debug level | < 0.5% | Staging/troubleshooting |
| Trace level (no sensitive) | < 0.7% | Development |
| Trace level (with sensitive) | < 1% | Deep debugging only |

### Level-Gated Logging (Built-in)

All logging methods use **level-gated execution** for performance:

```csharp
// ✅ Efficient - check is done before expensive operations
public void LogStateSnapshot(...)
{
    if (!_logger.IsEnabled(LogLevel.Debug)) return;  // ← Early exit

    // Expensive operations only run if Debug is enabled
    var messagesJson = JsonSerializer.Serialize(messages, _jsonOptions);
    _logger.LogDebug("...", messagesJson);
}
```

**Result:** Production overhead is minimal even with observability enabled.

---

## Production Best Practices

### ✅ **Recommended Production Configuration**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "HPD.Agent.AgentLoggingService": "Information",
      "Microsoft.Extensions.AI.LoggingChatClient": "Warning"
    }
  }
}
```

```csharp
var agent = new AgentBuilder(agentConfig)
    .WithLogging(enableSensitiveData: false)  // ✅ Privacy-safe
    .WithTelemetry(enableSensitiveData: false)
    .Build();
```

**Why this configuration:**
- ✅ < 0.3% overhead
- ✅ Tracks key lifecycle events
- ✅ Alerts on warnings/errors
- ✅ No sensitive data logged
- ✅ Full metrics available

---

### ✅ **Alerting Rules (Prometheus)**

```yaml
groups:
  - name: hpd_agent_alerts
    rules:
      # Alert on state divergence (corruption bug)
      - alert: AgentStateDivergence
        expr: increase(hpd_agent_state_divergence[5m]) > 0
        labels:
          severity: critical
        annotations:
          summary: "Agent state corruption detected"

      # Alert on high permission denial rate
      - alert: HighPermissionDenialRate
        expr: rate(hpd_agent_permission_denials[5m]) / rate(hpd_agent_permission_checks[5m]) > 0.5
        labels:
          severity: warning
        annotations:
          summary: "Over 50% of permissions denied"

      # Alert on slow filter pipelines
      - alert: SlowFilterPipeline
        expr: histogram_quantile(0.95, hpd_agent_filters_pipeline_duration) > 1000
        labels:
          severity: warning
        annotations:
          summary: "Filter pipeline P95 duration > 1s"
```

---

### ✅ **Log Aggregation (Structured Logging)**

HPD-Agent uses **structured logging** for easy querying:

```bash
# Example: Query logs in Splunk/ELK
sourcetype=hpd_agent AgentName="AI Assistant" LogLevel=Warning

# Example: Query state divergence events
sourcetype=hpd_agent "DIVERGENCE DETECTED"

# Example: Query permission denials
sourcetype=hpd_agent PermissionResult=Denied FunctionName=*
```

---

### ✅ **Development vs Production**

| Aspect | Development | Production |
|--------|------------|------------|
| Log Level | Debug/Trace | Information |
| Sensitive Data | Enabled (debugging) | Disabled |
| Log Output | Console + File | Structured logger (ELK/Splunk) |
| Metrics | Optional | Required (Prometheus) |
| Alerting | Optional | Required |

---

## Advanced Configuration

### Custom Log Formatting

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole(options =>
    {
        options.FormatterName = "json";  // JSON formatting for structured logs
    });
});
```

### Correlation IDs

All logs include correlation data:

```
[Info] Agent 'AI Assistant' message turn msg_123 started: conversation=conv_456, messages=25
                                        ↑ messageTurnId   ↑ conversationId
```

Use these IDs to trace requests across logs:

```bash
# Find all logs for a specific message turn
grep "msg_123" agent.log

# Find all logs for a specific conversation
grep "conv_456" agent.log
```

---

## Summary

### 🎯 **Quick Decision Matrix**

| Goal | Log Level | EnableSensitiveData | Telemetry |
|------|-----------|---------------------|-----------|
| Production monitoring | Information | false | ✅ Yes |
| Troubleshooting in staging | Debug | false | ✅ Yes |
| Deep debugging (dev only) | Trace | true | ✅ Yes |
| Privacy-critical production | Warning | false | ✅ Yes |

### 🎯 **Key Takeaways**

1. **Information level** is the sweet spot for production (< 0.3% overhead)
2. **Debug level** for staging/troubleshooting (< 0.5% overhead)
3. **Trace level** only for development with `enableSensitiveData: true`
4. **Always enable telemetry** - metrics have negligible overhead
5. **Use structured logging** for easy querying in production

---

## Related Documentation

- [Metrics Reference](./METRICS_REFERENCE.md) - Complete list of all metrics
- [Debugging Scenarios](./DEBUGGING_SCENARIOS.md) - Common issues and solutions
- [Architecture Overview](./ARCHITECTURE.md) - How observability fits in HPD-Agent

---

**Questions or feedback?** Open an issue on GitHub!
