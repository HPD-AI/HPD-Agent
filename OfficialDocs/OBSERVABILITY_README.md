# HPD-Agent Observability Documentation

**Complete observability infrastructure for debugging, monitoring, and optimizing HPD-Agent applications.**

## 📚 Documentation Index

| Document | Purpose | Audience |
|----------|---------|----------|
| **[OBSERVABILITY_GUIDE.md](./OBSERVABILITY_GUIDE.md)** | Complete configuration guide | All users |
| **[OBSERVABILITY_EXAMPLES.md](./OBSERVABILITY_EXAMPLES.md)** | Copy-paste ready examples | Developers |
| **[METRICS_REFERENCE.md](./METRICS_REFERENCE.md)** | All available metrics | DevOps/SRE |
| **[appsettings.observability.example.json](./appsettings.observability.example.json)** | Configuration templates | All users |

---

## 🚀 Quick Start (5 Minutes)

### Step 1: Enable Logging

**Program.cs:**
```csharp
var agent = new AgentBuilder(agentConfig)
    .WithLogging()  // ✅ Enable logging
    .Build();
```

**appsettings.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "HPD.Agent.AgentLoggingService": "Information"
    }
  }
}
```

### Step 2: Enable Telemetry

```csharp
var agent = new AgentBuilder(agentConfig)
    .WithLogging()
    .WithTelemetry()  // ✅ Enable metrics
    .Build();
```

### Step 3: View Metrics (Optional)

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("HPD.Agent")
            .AddPrometheusExporter();
    });

app.MapPrometheusScrapingEndpoint();  // /metrics
```

**That's it!** You now have full observability.

---

## 📖 What Gets Logged?

### Information Level (Production Default)
- ✅ Message turn start/end
- ✅ Permission requests/denials
- ✅ Document processing events
- ✅ Plan mode activation
- ✅ Checkpoint operations
- ❌ No message content (privacy-safe)

### Debug Level (Development)
- ✅ All Information-level logs
- ✅ State snapshots (counts only)
- ✅ Iteration message counts
- ✅ Filter pipeline execution
- ✅ Tool execution details
- ❌ No full message content

### Trace Level (Deep Debugging Only)
- ✅ All Debug-level logs
- ✅ **Full message content** (requires `enableSensitiveData: true`)
- ✅ Complete conversation history
- ⚠️ **Development only!**

---

## 📊 Available Metrics

### State Management
```
hpd.agent.state.message_count           # Message count in state
hpd.agent.turn_history.message_count    # Message count in history
hpd.agent.state.divergence              # State corruption warnings ⚠️
```

### Permission System
```
hpd.agent.permission.checks             # Total permission checks
hpd.agent.permission.denials            # Permission denials
hpd.agent.permission.duration           # Permission check duration
```

### Tool Execution
```
hpd.agent.tools.parallel_executions     # Parallel tool batches
hpd.agent.tools.parallel_batch_size     # Tools per batch
hpd.agent.tools.semaphore_wait          # Semaphore contention
```

### Filter Pipelines
```
hpd.agent.filters.pipeline_executions   # Filter pipeline runs
hpd.agent.filters.pipeline_duration     # Filter execution time
hpd.agent.filters.exceptions            # Filter failures
```

**[See full metrics reference →](./METRICS_REFERENCE.md)**

---

## 🔍 Common Debugging Scenarios

### Scenario 1: Agent Missing Context

**Symptom:** Agent doesn't remember recent conversation

**Solution:**
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
grep "LogMessageTurnStart" agent.log
# Shows: messages=100 (full history)

grep "LogIterationMessages.*iteration 0" agent.log
# Shows: Sending 10 messages to LLM

# ✅ Diagnosis: History reduced 100 → 10, check if summary complete
```

### Scenario 2: State Corruption

**Symptom:** Agent hallucinations, duplicate messages

**Solution:**
```bash
grep "DIVERGENCE DETECTED" agent.log

# Output:
# [Warning] Agent 'AI' iteration 2 [AfterTools] State=10, Turn=8 ⚠️ DIVERGENCE

# ✅ Diagnosis: State has 2 extra messages - corruption bug
```

### Scenario 3: Permission Bottleneck

**Symptom:** Slow agent responses

**Solution:**
```bash
grep "permission check" agent.log | grep "ms\]$"

# Output:
# [Debug] permission check for 'Query': ✅ Approved [5234ms]

# ✅ Diagnosis: Permission check taking 5 seconds
```

**[See more debugging scenarios →](./OBSERVABILITY_GUIDE.md#common-debugging-scenarios)**

---

## ⚙️ Configuration Examples

### Production (< 0.3% overhead)
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

### Development (Full Visibility)
```json
{
  "Logging": {
    "LogLevel": {
      "HPD.Agent.AgentLoggingService": "Trace"
    }
  }
}
```
```csharp
var agent = new AgentBuilder(config)
    .WithLogging(enableSensitiveData: true)  // ⚠️ Dev only
    .Build();
```

**[See all configuration examples →](./appsettings.observability.example.json)**

---

## 🎯 Production Best Practices

### ✅ Recommended Configuration

```csharp
var agent = new AgentBuilder(config)
    .WithLogging(enableSensitiveData: false)  // ✅ Privacy-safe
    .WithTelemetry()                          // ✅ Always enable metrics
    .Build();
```

```json
{
  "Logging": {
    "LogLevel": {
      "HPD.Agent.AgentLoggingService": "Information"  // ✅ Sweet spot
    }
  }
}
```

### ✅ Alerting Rules

```yaml
# Alert on state corruption
- alert: AgentStateDivergence
  expr: increase(hpd_agent_state_divergence[5m]) > 0
  severity: critical

# Alert on high denial rate
- alert: HighPermissionDenialRate
  expr: rate(hpd_agent_permission_denials[5m]) / rate(hpd_agent_permission_checks[5m]) > 0.5
  severity: warning

# Alert on slow filters
- alert: SlowFilterPipeline
  expr: histogram_quantile(0.95, hpd_agent_filters_pipeline_duration) > 1000
  severity: warning
```

**[See alerting setup →](./OBSERVABILITY_EXAMPLES.md#example-4-alerting-setup)**

---

## 📈 Performance Impact

| Configuration | Overhead | Use Case |
|---------------|----------|----------|
| None | 0% | ❌ Not recommended |
| Information | < 0.3% | ✅ **Production default** |
| Debug | < 0.5% | Staging/troubleshooting |
| Trace (no sensitive) | < 0.7% | Development |
| Trace (with sensitive) | < 1% | Deep debugging only |

**Key Insight:** Level-gated logging ensures minimal overhead. All expensive operations only run when the log level is enabled.

---

## 🔐 Privacy & Security

### Safe by Default

```csharp
// ✅ DEFAULT: No sensitive data logged
var agent = new AgentBuilder(config)
    .WithLogging()  // enableSensitiveData defaults to false
    .Build();
```

**What's logged:**
- ✅ Message counts, types, summaries
- ✅ Performance metrics
- ✅ Error details
- ❌ **No full message content**

### Opt-in for Debugging

```csharp
// ⚠️ DEVELOPMENT ONLY: Full message content
var agent = new AgentBuilder(config)
    .WithLogging(enableSensitiveData: true)
    .Build();
```

**Or via environment variable:**
```bash
export OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true
```

**[See sensitive data control →](./OBSERVABILITY_GUIDE.md#sensitive-data-control)**

---

## 🛠️ Integration Examples

### Docker + Prometheus + Grafana

```yaml
version: '3.8'
services:
  hpd-agent:
    build: .
    ports:
      - "5000:5000"

  prometheus:
    image: prom/prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml

  grafana:
    image: grafana/grafana
    ports:
      - "3000:3000"
```

**[See full setup →](./OBSERVABILITY_EXAMPLES.md#example-1-production-deployment)**

### ELK Stack

```yaml
# filebeat.yml
filebeat.inputs:
  - type: log
    paths:
      - /var/log/hpd-agent/*.log
    json.keys_under_root: true

output.elasticsearch:
  hosts: ["localhost:9200"]
```

**[See log analysis →](./OBSERVABILITY_EXAMPLES.md#example-5-log-analysis)**

---

## 🆚 Microsoft vs HPD-Agent Observability

| Aspect | Microsoft Middleware | HPD-Agent Observability |
|--------|---------------------|------------------------|
| **Scope** | LLM I/O only | Agent orchestration |
| **What it logs** | Requests to LLM | State, filters, permissions, tools |
| **When it runs** | During LLM call | Throughout agent lifecycle |
| **Token tracking** | ✅ Yes | Not needed (Microsoft handles) |
| **State tracking** | ❌ No | ✅ Yes |
| **Filter visibility** | ❌ No | ✅ Yes |
| **Permission tracking** | ❌ No | ✅ Yes |
| **Implementation** | Middleware pattern | Internal services |

**Conclusion:** Both are necessary and complementary.

**[See architecture explanation →](./OBSERVABILITY_GUIDE.md#overview)**

---

## 📚 Documentation Structure

```
Docs/
├── OBSERVABILITY_README.md              ← You are here (start here)
├── OBSERVABILITY_GUIDE.md               ← Complete configuration guide
├── OBSERVABILITY_EXAMPLES.md            ← Copy-paste examples
├── METRICS_REFERENCE.md                 ← All metrics documented
└── appsettings.observability.example.json ← Configuration templates
```

### Navigation Guide

**I want to...** | **Read this document**
---|---
Get started quickly | [Quick Start](#-quick-start-5-minutes) (above)
Configure logging levels | [OBSERVABILITY_GUIDE.md](./OBSERVABILITY_GUIDE.md#logging-configuration)
Set up production deployment | [OBSERVABILITY_EXAMPLES.md](./OBSERVABILITY_EXAMPLES.md#example-1-production-deployment)
Debug a specific issue | [OBSERVABILITY_GUIDE.md](./OBSERVABILITY_GUIDE.md#common-debugging-scenarios)
Understand all metrics | [METRICS_REFERENCE.md](./METRICS_REFERENCE.md)
Copy configuration templates | [appsettings.observability.example.json](./appsettings.observability.example.json)
Set up Prometheus alerts | [OBSERVABILITY_EXAMPLES.md](./OBSERVABILITY_EXAMPLES.md#example-4-alerting-setup)
Integrate with ELK/Splunk | [OBSERVABILITY_EXAMPLES.md](./OBSERVABILITY_EXAMPLES.md#example-5-log-analysis)

---

## 🎓 Learning Path

### Beginner
1. Read this README (you are here)
2. Try [Quick Start](#-quick-start-5-minutes)
3. Read [OBSERVABILITY_GUIDE.md - Log Levels](./OBSERVABILITY_GUIDE.md#log-levels-explained)
4. Try [Example 2: Development Debugging](./OBSERVABILITY_EXAMPLES.md#example-2-development-debugging)

### Intermediate
1. Read [OBSERVABILITY_GUIDE.md - Complete](./OBSERVABILITY_GUIDE.md)
2. Try [Example 1: Production Deployment](./OBSERVABILITY_EXAMPLES.md#example-1-production-deployment)
3. Explore [METRICS_REFERENCE.md](./METRICS_REFERENCE.md)
4. Set up basic alerts

### Advanced
1. Read [Example 3: Performance Monitoring](./OBSERVABILITY_EXAMPLES.md#example-3-performance-monitoring)
2. Set up [ELK/Splunk Integration](./OBSERVABILITY_EXAMPLES.md#example-5-log-analysis)
3. Create custom Grafana dashboards
4. Implement correlation ID tracing

---

## ❓ FAQ

### Q: Do I need both logging and telemetry?

**A:** Yes, they serve different purposes:
- **Logging:** For debugging specific issues (what happened?)
- **Telemetry:** For monitoring trends (how is the system performing?)

### Q: What's the production overhead?

**A:** < 0.3% with Information-level logging and full telemetry.

### Q: Is message content logged by default?

**A:** No. Full message content requires both:
1. `enableSensitiveData: true`
2. Log level set to `Trace`

### Q: Can I use this with Azure App Insights?

**A:** Yes! Configure OpenTelemetry to export to Azure Monitor:
```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("HPD.Agent")
        .AddAzureMonitorMetricExporter());
```

### Q: How do I trace a specific request?

**A:** Use correlation IDs (messageTurnId, conversationId):
```bash
grep "msg_abc123" agent.log
```

**[See more FAQ →](./OBSERVABILITY_GUIDE.md)**

---

## 🤝 Contributing

Found a bug or have suggestions? Open an issue on GitHub!

---

## 📄 License

This observability infrastructure is part of HPD-Agent and follows the same license.

---

**Ready to get started?**

👉 [Quick Start Guide](#-quick-start-5-minutes) or [Complete Configuration Guide](./OBSERVABILITY_GUIDE.md)
