# HPD-Agent Observability - Practical Examples

This guide provides **copy-paste ready examples** for common observability scenarios.

## Table of Contents
- [Basic Setup](#basic-setup)
- [Example 1: Production Deployment](#example-1-production-deployment)
- [Example 2: Development Debugging](#example-2-development-debugging)
- [Example 3: Performance Monitoring](#example-3-performance-monitoring)
- [Example 4: Alerting Setup](#example-4-alerting-setup)
- [Example 5: Log Analysis](#example-5-log-analysis)

---

## Basic Setup

### Minimal Configuration (Console App)

```csharp
using HPD.Agent;
using Microsoft.Extensions.Logging;

// Create agent with logging enabled
var agentConfig = new AgentConfig
{
    Name = "AI Assistant",
    SystemInstructions = "You are a helpful assistant",
    Provider = new ProviderConfig
    {
        ProviderKey = "openrouter",
        ModelName = "google/gemini-2.0-flash-exp"
    }
};

var agent = new AgentBuilder(agentConfig)
    .WithLogging()  // ✅ Enable logging (defaults to Information level)
    .Build();
```

**appsettings.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HPD.Agent.AgentLoggingService": "Information"
    }
  }
}
```

**Output:**
```
[Information] Agent 'AI Assistant' message turn msg_abc123 started: conversation=conv_xyz, messages=5
[Information] Agent 'AI Assistant' message turn msg_abc123 completed: iterations=2, final_messages=9
```

---

## Example 1: Production Deployment

### Web API with OpenTelemetry and Prometheus

**Program.cs:**
```csharp
using HPD.Agent;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════
// LOGGING CONFIGURATION
// ═══════════════════════════════════════════════════════════════
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.FormatterName = "json";  // Structured logging
});
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
    options.UseUtcTimestamp = true;
});

// ═══════════════════════════════════════════════════════════════
// OPENTELEMETRY CONFIGURATION
// ═══════════════════════════════════════════════════════════════
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("HPD.Agent")  // ✅ HPD-Agent metrics
            .AddPrometheusExporter();
    });

// ═══════════════════════════════════════════════════════════════
// BUILD AGENT
// ═══════════════════════════════════════════════════════════════
var agentConfig = new AgentConfig
{
    Name = "Production Assistant",
    SystemInstructions = "You are a production AI assistant",
    Provider = new ProviderConfig
    {
        ProviderKey = "openrouter",
        ModelName = "google/gemini-2.0-flash-exp"
    }
};

var agent = new AgentBuilder(agentConfig)
    .WithLogging(enableSensitiveData: false)  // ✅ Privacy-safe
    .WithTelemetry(enableSensitiveData: false)
    .Build();

builder.Services.AddSingleton(agent);

var app = builder.Build();

// Expose Prometheus metrics endpoint
app.MapPrometheusScrapingEndpoint();  // /metrics

app.MapPost("/chat", async (ChatRequest request, Agent agent) =>
{
    var thread = Project.Create("API Session").CreateThread();
    var userMessage = new ChatMessage(ChatRole.User, request.Message);

    var response = await agent.RunAsync([userMessage], thread);

    return Results.Ok(new { response = response.Message.Text });
});

app.Run();

record ChatRequest(string Message);
```

**appsettings.json:**
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

**Docker Compose (with Prometheus):**
```yaml
version: '3.8'
services:
  hpd-agent-api:
    build: .
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production

  prometheus:
    image: prom/prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'

  grafana:
    image: grafana/grafana
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
```

**prometheus.yml:**
```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'hpd-agent'
    static_configs:
      - targets: ['hpd-agent-api:5000']
    metrics_path: '/metrics'
```

**View Metrics:**
```bash
# Prometheus queries
curl http://localhost:9090/api/v1/query?query=hpd_agent_permission_checks

# Grafana dashboards available at http://localhost:3000
```

---

## Example 2: Development Debugging

### Full Trace Logging with Message Content

**Program.cs:**
```csharp
using HPD.Agent;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Enable detailed console logging for development
builder.Logging.AddConsole(options =>
{
    options.FormatterName = "simple";
});

var agentConfig = new AgentConfig
{
    Name = "Debug Assistant",
    SystemInstructions = "You are a debugging assistant",
    Provider = new ProviderConfig
    {
        ProviderKey = "openrouter",
        ModelName = "google/gemini-2.0-flash-exp"
    }
};

var agent = new AgentBuilder(agentConfig)
    .WithLogging(enableSensitiveData: true)  // ⚠️ DEVELOPMENT ONLY
    .WithTelemetry()
    .Build();

// ... rest of app setup
```

**appsettings.Development.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HPD.Agent.AgentLoggingService": "Trace",  // Full visibility
      "Microsoft.Extensions.AI.LoggingChatClient": "Trace"
    }
  }
}
```

**Example Output:**
```
[Information] Agent 'Debug Assistant' message turn msg_123 started: conversation=conv_456, messages=100

[Trace] Agent 'Debug Assistant' message turn msg_123 input messages:
[
  {"role":"user","content":"Analyze Q4 earnings"},
  {"role":"assistant","content":"I'll analyze the Q4 earnings..."},
  // ... 98 more messages
]

[Debug] Agent 'Debug Assistant' iteration 0: Sending 10 messages to LLM

[Trace] Agent 'Debug Assistant' iteration 0 LLM messages:
[
  {"role":"assistant","content":"Summary: Previous conversation discussed..."},
  {"role":"user","content":"Analyze Q4 earnings"}
]

[Trace] Agent 'Debug Assistant' iteration 0 [BeforeLLM] Full Messages:
[
  {"role":"assistant","content":"Summary..."},
  // ... full message content
]

[Debug] Agent 'Debug Assistant' executed Prompt pipeline: 3 filters [45ms]

[Information] Agent 'Debug Assistant' message turn msg_123 completed: iterations=2, final_messages=104
```

**Debugging Specific Issues:**

```bash
# Find all logs for a specific message turn
grep "msg_123" agent.log

# Find state divergence warnings
grep "DIVERGENCE DETECTED" agent.log

# Find permission denials
grep "permission check.*Denied" agent.log

# Find slow filter pipelines
grep "pipeline.*\[.*ms\]" agent.log | awk -F'[' '{print $NF}' | sort -n
```

---

## Example 3: Performance Monitoring

### Custom Metrics Dashboard

**Prometheus Alerts (alerts.yml):**
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
          summary: "Agent {{ $labels.agent_name }} state corruption detected"
          description: "State divergence count increased by {{ $value }}"

      # Alert on high permission denial rate
      - alert: HighPermissionDenialRate
        expr: |
          rate(hpd_agent_permission_denials[5m]) /
          rate(hpd_agent_permission_checks[5m]) > 0.5
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High permission denial rate for {{ $labels.agent_name }}"
          description: "{{ $value | humanizePercentage }} of permissions denied"

      # Alert on slow filter pipelines
      - alert: SlowFilterPipeline
        expr: |
          histogram_quantile(0.95,
            rate(hpd_agent_filters_pipeline_duration_bucket[5m])
          ) > 1000
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Slow {{ $labels.filter_type }} filter pipeline"
          description: "P95 duration is {{ $value }}ms"

      # Alert on retry exhaustion
      - alert: HighRetryExhaustion
        expr: increase(hpd_agent_retry_exhaustion[15m]) > 10
        labels:
          severity: warning
        annotations:
          summary: "High retry exhaustion for {{ $labels.function_name }}"
          description: "{{ $value }} retry exhaustions in 15 minutes"

      # Alert on low history reduction cache hit rate
      - alert: LowReductionCacheHitRate
        expr: |
          rate(hpd_agent_reduction_cache_hits[5m]) /
          (rate(hpd_agent_reduction_cache_hits[5m]) +
           rate(hpd_agent_reduction_cache_misses[5m])) < 0.5
        for: 10m
        labels:
          severity: info
        annotations:
          summary: "Low reduction cache hit rate for {{ $labels.agent_name }}"
          description: "Cache hit rate is {{ $value | humanizePercentage }}"
```

**Grafana Dashboard (JSON):**
```json
{
  "dashboard": {
    "title": "HPD-Agent Performance",
    "panels": [
      {
        "title": "Message Turn Lifecycle",
        "targets": [
          {
            "expr": "rate(hpd_agent_state_message_count[5m])",
            "legendFormat": "Messages in State"
          },
          {
            "expr": "rate(hpd_agent_turn_history_message_count[5m])",
            "legendFormat": "Messages in History"
          }
        ]
      },
      {
        "title": "Permission System",
        "targets": [
          {
            "expr": "rate(hpd_agent_permission_checks[5m])",
            "legendFormat": "Checks/sec"
          },
          {
            "expr": "rate(hpd_agent_permission_denials[5m])",
            "legendFormat": "Denials/sec"
          }
        ]
      },
      {
        "title": "Filter Pipeline P95 Duration by Type",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(hpd_agent_filters_pipeline_duration_bucket[5m])) by (filter_type)"
          }
        ]
      },
      {
        "title": "Tool Parallelization",
        "targets": [
          {
            "expr": "avg(hpd_agent_tools_parallel_batch_size)",
            "legendFormat": "Avg Batch Size"
          }
        ]
      },
      {
        "title": "Reduction Effectiveness",
        "targets": [
          {
            "expr": "avg(hpd_agent_reduction_token_savings)",
            "legendFormat": "Avg Token Savings"
          }
        ]
      }
    ]
  }
}
```

**Query Examples:**
```promql
# Permission denial rate
rate(hpd_agent_permission_denials[5m]) / rate(hpd_agent_permission_checks[5m])

# P95 filter duration by type
histogram_quantile(0.95, rate(hpd_agent_filters_pipeline_duration_bucket[5m])) by (filter_type)

# Average parallel tool batch size
avg(hpd_agent_tools_parallel_batch_size)

# Reduction cache hit rate
rate(hpd_agent_reduction_cache_hits[5m]) /
(rate(hpd_agent_reduction_cache_hits[5m]) + rate(hpd_agent_reduction_cache_misses[5m]))

# State divergence events
increase(hpd_agent_state_divergence[1h])
```

---

## Example 4: Alerting Setup

### Slack Integration

**alertmanager.yml:**
```yaml
global:
  resolve_timeout: 5m
  slack_api_url: 'https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK'

route:
  group_by: ['alertname', 'agent_name']
  group_wait: 10s
  group_interval: 10s
  repeat_interval: 1h
  receiver: 'slack'

receivers:
  - name: 'slack'
    slack_configs:
      - channel: '#hpd-agent-alerts'
        title: '{{ .GroupLabels.alertname }}'
        text: |
          {{ range .Alerts }}
            *Alert:* {{ .Annotations.summary }}
            *Description:* {{ .Annotations.description }}
            *Severity:* {{ .Labels.severity }}
            *Agent:* {{ .Labels.agent_name }}
          {{ end }}
```

**Example Alert (Slack):**
```
🚨 AgentStateDivergence
━━━━━━━━━━━━━━━━━━━━━━━━
Alert: Agent 'AI Assistant' state corruption detected
Description: State divergence count increased by 5
Severity: critical
Agent: AI Assistant
Time: 2025-01-15 14:32:00
```

---

## Example 5: Log Analysis

### ELK Stack Integration

**filebeat.yml:**
```yaml
filebeat.inputs:
  - type: log
    enabled: true
    paths:
      - /var/log/hpd-agent/*.log
    json.keys_under_root: true
    json.add_error_key: true

output.elasticsearch:
  hosts: ["localhost:9200"]
  index: "hpd-agent-%{+yyyy.MM.dd}"

setup.kibana:
  host: "localhost:5601"
```

**Kibana Queries:**

```json
// Find all state divergence events
{
  "query": {
    "match": {
      "message": "DIVERGENCE DETECTED"
    }
  }
}

// Find slow permission checks (> 1s)
{
  "query": {
    "bool": {
      "must": [
        { "match": { "LoggerName": "HPD.Agent.AgentLoggingService" } },
        { "match": { "message": "permission check" } },
        { "range": { "Duration": { "gte": 1000 } } }
      ]
    }
  }
}

// Find all permission denials
{
  "query": {
    "bool": {
      "must": [
        { "match": { "message": "permission check" } },
        { "match": { "Result": "Denied" } }
      ]
    }
  }
}

// Aggregate permission denials by function
{
  "size": 0,
  "aggs": {
    "denials_by_function": {
      "terms": {
        "field": "FunctionName.keyword"
      }
    }
  },
  "query": {
    "match": { "Result": "Denied" }
  }
}
```

**Splunk Queries:**

```spl
# Find state divergence
sourcetype=hpd_agent "DIVERGENCE DETECTED"

# Permission denial rate by function
sourcetype=hpd_agent "permission check"
| stats count by FunctionName, Result
| eval denial_rate = if(Result="Denied", count, 0)
| stats sum(denial_rate) as denials, sum(count) as total by FunctionName
| eval rate = round(denials/total*100, 2)
| sort -rate

# P95 filter pipeline duration
sourcetype=hpd_agent "executed * pipeline"
| rex field=_raw "\\[(?<duration>\\d+)ms\\]"
| stats perc95(duration) by FilterType

# Message turn lifecycle analysis
sourcetype=hpd_agent ("message turn * started" OR "message turn * completed")
| transaction MessageTurnId startswith="started" endswith="completed"
| stats avg(duration) as avg_duration, max(Iterations) as max_iterations
```

---

## Example 6: Correlation ID Tracing

### Trace a Full Request

**Log output with correlation IDs:**
```
[Info] Agent 'AI Assistant' message turn msg_abc123 started: conversation=conv_xyz456, messages=25
[Debug] Agent 'AI Assistant' iteration 0: Sending 10 messages to LLM
[Debug] Agent 'AI Assistant' permission check for 'ReadFile': ✅ Approved [8ms]
[Debug] Agent 'AI Assistant' iteration 0: Executed 2 tools (batch=2, approved=2) [234ms]
[Info] Agent 'AI Assistant' message turn msg_abc123 completed: iterations=2, final_messages=29
```

**Trace by Message Turn ID:**
```bash
# Get all logs for a specific message turn
grep "msg_abc123" agent.log

# Get all logs for a specific conversation
grep "conv_xyz456" agent.log

# Structured log query (JSON logs)
jq 'select(.MessageTurnId == "msg_abc123")' agent.jsonl
```

---

## Summary

### Quick Reference

| Scenario | Log Level | EnableSensitiveData | Configuration |
|----------|-----------|---------------------|---------------|
| **Production** | Information | false | [Example 1](#example-1-production-deployment) |
| **Development** | Trace | true | [Example 2](#example-2-development-debugging) |
| **Performance Monitoring** | Information | false | [Example 3](#example-3-performance-monitoring) |
| **Troubleshooting** | Debug | false | See [OBSERVABILITY_GUIDE.md](./OBSERVABILITY_GUIDE.md) |

### Related Documentation

- [Observability Guide](./OBSERVABILITY_GUIDE.md) - Complete configuration guide
- [Metrics Reference](./METRICS_REFERENCE.md) - All available metrics
- [Configuration Examples](./appsettings.observability.example.json) - appsettings.json templates

---

**Need help?** Open an issue on GitHub or consult the full [Observability Guide](./OBSERVABILITY_GUIDE.md).
