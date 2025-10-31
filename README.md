# HPD-Agent

**Production-ready implementation of Microsoft Agent Framework with batteries included.**

[![NuGet](https://img.shields.io/nuget/v/HPD-Agent.svg)](https://www.nuget.org/packages/HPD-Agent/)
[![License](https://img.shields.io/badge/license-Proprietary-blue.svg)](LICENSE.md)

HPD-Agent extends Microsoft's Agent Framework with memory systems, advanced error handling, permissions, web search, MCP support, and 11 LLM providers - all with Native AOT compatibility.


---

## Why HPD-Agent?

**Microsoft Agent Framework provides:**
- ✅ Clean abstractions (AIAgent, AgentThread)
- ✅ Multi-agent workflows (WorkflowBuilder, GroupChat)
- ✅ A2A Protocol (agent-to-agent communication)
- ✅ Hosting infrastructure (ASP.NET Core - works with HPD-Agent)

**HPD-Agent adds the production features:**
- 🔋 **3 Memory Systems** - Dynamic, Static, Planning (Microsoft: none)
- 🔋 **Advanced Error Handling** - Provider-aware retry, Retry-After headers, circuit breakers
- 🔋 **Permissions** - Function-level + human-in-the-loop + persistent storage
- 🔋 **11 LLM Providers** - vs Microsoft's 2 (OpenAI, Azure)
- 🔋 **Plugin System** - Scoped, conditional, permissioned (87.5% token reduction)
- 🔋 **Skills System** - Package expertise with functions (like Anthropic's Agent Skills) (Microsoft: none)
- 🔋 **Web Search** - Tavily, Brave, Bing (Microsoft: none)
- 🔋 **MCP Integration** - Built-in client with manifest loading (Microsoft: manual SDK usage)
- 🔋 **AG-UI Protocol** - Complete event system (vs Microsoft's Responses API)
- 🔋 **Native AOT** - 100% compatible (Microsoft: partial)

**Same abstractions. Same workflows. Better production features.**

---

## Core Principles

HPD-Agent is designed around key architectural principles that make it production-ready:

#### Fully Native AOT Compatible

#### Configuration-First

#### Event-Driven First


---

## Core Features

### Memory Systems
- **Dynamic Memory** - Agent-controlled working memory with auto-eviction
- **Static Memory** - Read-only knowledge base (RAG without vector DB)
- **Plan Mode** - Goal → Steps → Execution tracking
- **History Reduction** - LLM-based conversation compression

### Provider Support (11 Providers)
OpenAI • Anthropic • Azure OpenAI • Azure AI Inference • Google AI • Mistral • Ollama • HuggingFace • AWS Bedrock • OnnxRuntime • OpenRouter

### Error Handling & Resilience
- Provider-specific error categorization
- Retry-After header respect
- Exponential backoff with jitter
- Circuit breakers & timeouts

### Plugin System
- Source-generated (Native AOT)
- Conditional functions (type-safe)
- Plugin scoping (87.5% token reduction)
- Permission requirements

### Skills System
- **Package domain expertise with functions** - Same concept as Anthropic's Agent Skills
- Progressive disclosure: metadata → instructions → linked documents
- Load specialized knowledge only when needed (not in system prompt)
- Markdown files with procedural knowledge, SOPs, best practices
- Cross-plugin composition for semantic groupings
- Skills-only mode for simplified agent interfaces

### Additional Features
- **Project System** - Multi-conversation containers with shared document context (like workspaces)
- **Human-in-the-Loop Clarification** - Sub-agents can ask users for information mid-turn without breaking agentic flow
- **Web Search** - Multi-provider (Tavily, Brave, Bing)
- **MCP Integration** - Full Model Context Protocol client
- **AG-UI Protocol** - Standard event streaming for frontends
- **Document Handling** - PDF, DOCX, images, URLs
- **Observability** - OpenTelemetry, logging, caching
- **Conversation Management** - Token counting, cost tracking

---

## Documentation

- **[Overview & Full Feature List](OVERVIEW.md)** - Comprehensive feature documentation
- **[Agent Developer Guide](docs/Agent-Developer-Documentation.md)** - Build agents
- **[Getting Started Guide](docs/getting-started.md)** - Step-by-step tutorial
- **[Configuration Reference](docs/configuration-reference.md)** - All configuration options
- **[Provider Guide](docs/providers.md)** - Provider-specific details
- **[Plugin Development](docs/plugins.md)** - Create custom plugins
- **[Migration Guides](docs/migration/)** - From ChatClientAgent/Semantic Kernel

---


More examples in [`examples/`](examples/)

---

## Microsoft Compatibility

| Component | HPD-Agent |
|-----------|-----------|
| `AIAgent` abstraction | ✅ Implements |
| `AgentThread` | ✅ Extends with ConversationThread |
| `RunAsync()` / `RunStreamingAsync()` | ✅ Full implementation |
| `WorkflowBuilder` | ✅ Drop-in compatible |
| `GroupChatWorkflowBuilder` | ✅ Works seamlessly |
| A2A Protocol | ✅ Compatible (can communicate with A2A agents) |
| Service discovery | ✅ GetService pattern |
| Thread serialization | ✅ Supported |

**HPD-Agent is a drop-in replacement for ChatClientAgent with production features built-in.**

---

## Comparison

| Feature | ChatClientAgent | HPD-Agent |
|---------|----------------|-----------|
| Implements AIAgent | ✅ | ✅ |
| Works in Workflows | ✅ | ✅ |
| A2A Protocol | ✅ | ✅ Compatible |
| Memory Systems | ❌ | ✅ 3 types |
| Error Handling | ⚠️ Basic | ✅ Provider-aware |
| Permissions | ❌ | ✅ Built-in |
| Plugin System | ⚠️ Basic | ✅ Scoped/Conditional |
| Skills System | ❌ | ✅ Cross-plugin composition |
| Web Search | ❌ | ✅ 3 providers |
| MCP Support | ⚠️ Manual SDK | ✅ Built-in client |
| AG-UI Protocol | ❌ | ✅ Full implementation |
| Native AOT | ⚠️ Partial | ✅ Complete |
| Providers | OpenAI, Azure | 11+ providers |

---

## The Story

We built HPD-Agent on Microsoft.Extensions.AI before Microsoft released Agent Framework. When Agent Framework launched with clean abstractions and workflows, we kept their architecture and added our production features. The result: **Microsoft's blueprint + our batteries.**

**Read the full story in [OVERVIEW.md](OVERVIEW.md#the-story-why-hpd-agent-exists)**

---

## FAQ

**Is this compatible with Microsoft Agent Framework?**
Yes, 100%. We implement the same `AIAgent` specification.

**Can I use Microsoft's workflows?**
Absolutely. Both ChatClientAgent and HPD-Agent work seamlessly in Microsoft workflows.

**Why not just use ChatClientAgent?**
ChatClientAgent is currently minimal. HPD-Agent is intentionally batteries-included. Choose based on whether you want to build or buy.

**Is this open source?**
HPD-Agent is closed source.

---

## Support

- **Documentation**: [docs.hpd-agent.com](https://docs.hpd-agent.com)
- **Email**: [support@hpd-agent.com](mailto:support@hpd-agent.com)
- **Issues**: [GitHub Issues](https://github.com/yourorg/hpd-agent/issues)

---

## License

Proprietary. See [LICENSE.md](LICENSE.md) for details.

---

<div align="center">

**Microsoft Agent Framework, Batteries Included** 🔋

*Same abstractions · Same workflows · Better implementation*

[Get Started](docs/getting-started.md) · [Full Overview](OVERVIEW.md) · [Examples](examples/)

</div>
