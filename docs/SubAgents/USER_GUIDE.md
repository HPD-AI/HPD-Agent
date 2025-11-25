# SubAgent User Guide

## Table of Contents
- [Introduction](#introduction)
- [Why Use SubAgents?](#why-use-subagents)
- [Quick Start](#quick-start)
- [Thread Modes](#thread-modes)
- [Best Practices](#best-practices)
- [Common Patterns](#common-patterns)
- [Examples](#examples)
- [Troubleshooting](#troubleshooting)

---

## Introduction

SubAgents enable **hierarchical agent composition** where specialized child agents can be invoked as tools by parent agents. This allows you to:

- Break complex tasks into specialized sub-tasks
- Create domain-specific expert agents
- Compose agents with different capabilities and models
- Maintain conversation context across agent boundaries

SubAgents appear as **normal function calls** to the orchestrator but run as **complete autonomous agents** with their own system instructions, models, and tool sets.

---

## Why Use SubAgents?

### **1. Specialization**
Different models excel at different tasks. Use GPT-4 for complex reasoning, Claude for writing, and Gemini for coding:

```csharp
[SubAgent(Category = "Domain Experts")]
public SubAgent WritingExpert()
{
    return SubAgentFactory.Create(
        "WritingExpert",
        "Expert copywriter for marketing content",
        new AgentConfig
        {
            SystemInstructions = "You are a professional copywriter...",
            Provider = new ProviderConfig { ModelName = "claude-3-5-sonnet" }
        });
}

[SubAgent(Category = "Domain Experts")]
public SubAgent CodingExpert()
{
    return SubAgentFactory.Create(
        "CodingExpert",
        "Expert software engineer",
        new AgentConfig
        {
            SystemInstructions = "You are a senior software engineer...",
            Provider = new ProviderConfig { ModelName = "gemini-2.0-flash" }
        });
}
```

### **2. Tool Segregation**
Give each SubAgent only the tools it needs:

```csharp
[SubAgent(Category = "Engineering")]
public SubAgent DatabaseAdmin()
{
    return SubAgentFactory.Create(
        "DatabaseAdmin",
        "Database administration expert",
        new AgentConfig { /* config */ },
        typeof(DatabasePlugin), typeof(SqlPlugin)  // Only database tools
    );
}

[SubAgent(Category = "Engineering")]
public SubAgent DeploymentExpert()
{
    return SubAgentFactory.Create(
        "DeploymentExpert",
        "CI/CD and deployment specialist",
        new AgentConfig { /* config */ },
        typeof(DockerPlugin), typeof(KubernetesPlugin)  // Only deployment tools
    );
}
```

### **3. Reduced Context Window Pressure**
Each SubAgent maintains its own context, preventing the orchestrator from becoming overloaded:

```
Orchestrator (short context)
  ├─> CodeReviewer (focused on code review)
  ├─> TestWriter (focused on tests)
  └─> DocumentationWriter (focused on docs)
```

### **4. Observable Multi-Agent Workflows**
Events from SubAgents bubble up to the orchestrator with full attribution:

```csharp
orchestrator.OnEventAsync(evt =>
{
    var who = evt.ExecutionContext?.AgentName;  // "CodeReviewer"
    var depth = evt.ExecutionContext?.Depth;     // 1

    Console.WriteLine($"[{who}] emitted {evt.GetType().Name}");
});
```

---

## Quick Start

### **Step 1: Define a SubAgent**

Create a method that returns a `SubAgent`:

```csharp
using HPD.Agent;

public class MySubAgents
{
    [SubAgent(Category = "Helpers", Priority = 1)]
    public SubAgent MathExpert()
    {
        return SubAgentFactory.Create(
            "MathExpert",
            "Solves complex mathematical problems",
            new AgentConfig
            {
                Name = "Math Expert",
                SystemInstructions = "You are a mathematics professor. Solve problems step-by-step.",
                MaxAgenticIterations = 10,
                Provider = new ProviderConfig
                {
                    ProviderKey = "openrouter",
                    ModelName = "google/gemini-2.0-flash-exp:free"
                }
            });
    }
}
```

### **Step 2: Register the Plugin**

Register the class containing SubAgents with your orchestrator:

```csharp
var orchestrator = new AgentBuilder(new AgentConfig
{
    Name = "Orchestrator",
    SystemInstructions = "You coordinate specialized agents to help the user.",
    Provider = new ProviderConfig { /* config */ }
})
.WithPlugin<MySubAgents>()  // Register SubAgents
.Build();
```

### **Step 3: Use It!**

The orchestrator now sees `MathExpert` as a callable function:

```csharp
var response = await orchestrator.RunAsync(
    "What is the derivative of x^2 + 3x + 5?");

// Orchestrator automatically calls MathExpert SubAgent when needed
```

Behind the scenes:
1. Orchestrator sees "math problem" → calls `MathExpert(query: "What is the derivative...")`
2. MathExpert SubAgent spins up with its own config and system instructions
3. MathExpert solves the problem
4. Result returns to orchestrator
5. Orchestrator continues with the answer

---

## Thread Modes

SubAgents support three thread modes to control conversation history:

### **1. Stateless (Default)**

Each invocation creates a **new** conversation thread. No memory between calls.

```csharp
return SubAgentFactory.Create(
    "WeatherExpert",
    "Weather forecast specialist",
    config  // Stateless by default
);
```

**Use when:**
- Each query is independent
- No follow-up questions expected
- Fresh context for each invocation

**Example:**
```
User: "What's the weather in NYC?"
→ SubAgent: "Sunny, 75°F"

User: "How about tomorrow?"
→ SubAgent: "Where? I don't have context from the previous call"  ❌
```

---

### **2. SharedThread (Stateful)**

All invocations share the **same** conversation thread. SubAgent remembers previous interactions.

```csharp
return SubAgentFactory.CreateStateful(
    "MathExpert",
    "Math problem solver with memory",
    config
);
```

**Use when:**
- Multi-turn conversations expected
- SubAgent needs to remember previous answers
- Building on previous work

**Example:**
```
User: "What's 5 + 5?"
→ SubAgent: "10"

User: "Now multiply that by 2"
→ SubAgent: "20"  ✅ (remembers the previous answer)

User: "And divide by 4"
→ SubAgent: "5"  ✅ (still has full context)
```

---

### **3. PerSession (User-Managed)**

You control the thread lifecycle. Useful for per-user or per-workflow scoping.

```csharp
return SubAgentFactory.CreatePerSession(
    "ChatBot",
    "Personalized chat agent",
    config
);
```

**Use when:**
- Per-user session management needed
- Custom thread scoping required
- Different users need separate contexts

**Implementation:**
```csharp
// You'd set the thread manually before invocation
// (Advanced usage - typically handled by custom orchestration logic)
```

---

## Best Practices

### **✅ DO:**

#### **1. Use Descriptive Names and Descriptions**
The orchestrator uses these to decide when to call the SubAgent:

```csharp
// ✅ GOOD
return SubAgentFactory.Create(
    "PythonCodeReviewer",
    "Reviews Python code for bugs, performance issues, and style violations. Provides detailed feedback with code examples.",
    config);

// ❌ BAD
return SubAgentFactory.Create(
    "CodeAgent",
    "Reviews code",
    config);
```

#### **2. Give SubAgents Focused Responsibilities**
Each SubAgent should have a clear, specific purpose:

```csharp
// ✅ GOOD - Specific purpose
[SubAgent]
public SubAgent SecurityAuditor() { /* Focuses only on security */ }

[SubAgent]
public SubAgent PerformanceOptimizer() { /* Focuses only on performance */ }

// ❌ BAD - Too broad
[SubAgent]
public SubAgent CodeHelper() { /* Does everything */ }
```

#### **3. Use Categories and Priority**
Help organize SubAgents in large systems:

```csharp
[SubAgent(Category = "Domain Experts", Priority = 10)]  // Higher priority
public SubAgent SeniorArchitect() { /* ... */ }

[SubAgent(Category = "Domain Experts", Priority = 5)]
public SubAgent JuniorDeveloper() { /* ... */ }

[SubAgent(Category = "Tools", Priority = 1)]
public SubAgent Calculator() { /* ... */ }
```

#### **4. Choose the Right Thread Mode**
- **Stateless**: Independent queries (weather, calculations, translations)
- **SharedThread**: Conversations (tutoring, debugging sessions, creative writing)
- **PerSession**: Custom scoping (per-user chatbots, workflow-specific agents)

---

### **❌ DON'T:**

#### **1. Don't Create Circular Dependencies**
SubAgents calling parent agents creates infinite loops:

```csharp
// ❌ BAD - Circular dependency
public class OrchestratorPlugin
{
    [SubAgent]
    public SubAgent HelperAgent()
    {
        // This agent has OrchestratorPlugin registered
        // HelperAgent can call Orchestrator which calls HelperAgent... infinite loop!
    }
}
```

#### **2. Don't Overuse SharedThread**
Shared threads consume memory and aren't thread-safe:

```csharp
// ❌ BAD - Using SharedThread for independent queries
return SubAgentFactory.CreateStateful(
    "WeatherLookup",  // Each weather query is independent!
    "Gets current weather",
    config);

// ✅ GOOD - Use Stateless
return SubAgentFactory.Create(
    "WeatherLookup",
    "Gets current weather",
    config);
```

#### **3. Don't Make SubAgents Too Granular**
Too many tiny SubAgents creates coordination overhead:

```csharp
// ❌ BAD - Too granular
[SubAgent] public SubAgent AddNumbers() { /* Only adds */ }
[SubAgent] public SubAgent SubtractNumbers() { /* Only subtracts */ }
[SubAgent] public SubAgent MultiplyNumbers() { /* Only multiplies */ }

// ✅ GOOD - Right level of granularity
[SubAgent] public SubAgent MathExpert() { /* Handles all basic math */ }
```

---

## Common Patterns

### **Pattern 1: Domain Expert Ensemble**

Multiple specialized agents for different domains:

```csharp
public class DomainExperts
{
    [SubAgent(Category = "Experts")]
    public SubAgent LegalExpert()
    {
        return SubAgentFactory.Create(
            "LegalExpert",
            "Provides legal analysis and contract review",
            new AgentConfig
            {
                SystemInstructions = "You are a licensed attorney...",
                Provider = new ProviderConfig { ModelName = "gpt-4" }
            });
    }

    [SubAgent(Category = "Experts")]
    public SubAgent MedicalExpert()
    {
        return SubAgentFactory.Create(
            "MedicalExpert",
            "Provides medical information (not advice)",
            new AgentConfig
            {
                SystemInstructions = "You are a medical doctor...",
                Provider = new ProviderConfig { ModelName = "claude-3-5-sonnet" }
            });
    }

    [SubAgent(Category = "Experts")]
    public SubAgent FinancialExpert()
    {
        return SubAgentFactory.Create(
            "FinancialExpert",
            "Analyzes financial data and provides insights",
            new AgentConfig
            {
                SystemInstructions = "You are a financial analyst...",
                Provider = new ProviderConfig { ModelName = "gpt-4" }
            });
    }
}
```

### **Pattern 2: Workflow Stages**

SubAgents representing stages in a pipeline:

```csharp
public class ContentCreationWorkflow
{
    [SubAgent(Category = "Workflow", Priority = 3)]
    public SubAgent Researcher()
    {
        return SubAgentFactory.CreateStateful(  // Remembers research
            "Researcher",
            "Researches topics and gathers information",
            new AgentConfig { /* config */ },
            typeof(WebSearchPlugin), typeof(WikipediaPlugin)
        );
    }

    [SubAgent(Category = "Workflow", Priority = 2)]
    public SubAgent Writer()
    {
        return SubAgentFactory.CreateStateful(  // Builds on research
            "Writer",
            "Writes articles based on research",
            new AgentConfig { /* config */ }
        );
    }

    [SubAgent(Category = "Workflow", Priority = 1)]
    public SubAgent Editor()
    {
        return SubAgentFactory.Create(  // Stateless review
            "Editor",
            "Edits and polishes written content",
            new AgentConfig { /* config */ }
        );
    }
}
```

### **Pattern 3: Specialist + Generalist**

Combine a general-purpose orchestrator with specialized SubAgents:

```csharp
var orchestrator = new AgentBuilder(new AgentConfig
{
    Name = "GeneralAssistant",
    SystemInstructions = @"
        You are a helpful assistant. When users ask specialized questions:
        - Use CodeExpert for programming questions
        - Use DataAnalyst for data analysis
        - Use ContentWriter for writing tasks
        Answer simple questions yourself.",
    Provider = new ProviderConfig { ModelName = "gpt-4o-mini" }  // Fast, cheap
})
.WithPlugin<SpecialistAgents>()  // Expensive specialists
.Build();
```

---

## Examples

### **Example 1: Multi-Language Translation Pipeline**

```csharp
public class TranslationPipeline
{
    [SubAgent]
    public SubAgent Translator()
    {
        return SubAgentFactory.Create(
            "Translator",
            "Translates text between languages while preserving tone and context",
            new AgentConfig
            {
                SystemInstructions = @"
                    You are a professional translator.
                    Preserve:
                    - Original tone and style
                    - Cultural context
                    - Technical accuracy

                    Translate naturally, not word-for-word.",
                Provider = new ProviderConfig { ModelName = "gpt-4" }
            });
    }

    [SubAgent]
    public SubAgent CulturalAdvisor()
    {
        return SubAgentFactory.Create(
            "CulturalAdvisor",
            "Advises on cultural sensitivities and localization",
            new AgentConfig
            {
                SystemInstructions = @"
                    You are a cultural consultant.
                    Identify cultural issues in translated content.
                    Suggest localizations for idioms and references.",
                Provider = new ProviderConfig { ModelName = "gpt-4" }
            });
    }
}
```

**Usage:**
```csharp
User: "Translate 'It's raining cats and dogs' to Spanish and check for cultural issues"

Orchestrator:
  1. Calls Translator → "Está lloviendo a cántaros"
  2. Calls CulturalAdvisor → "Good choice - this is the Spanish equivalent idiom"
```

---

### **Example 2: Code Review System**

```csharp
public class CodeReviewTeam
{
    [SubAgent(Category = "Review", Priority = 3)]
    public SubAgent SecurityReviewer()
    {
        return SubAgentFactory.Create(
            "SecurityReviewer",
            "Reviews code for security vulnerabilities (SQL injection, XSS, etc.)",
            new AgentConfig
            {
                SystemInstructions = "You are a security expert. Find vulnerabilities...",
                MaxAgenticIterations = 15,
                Provider = new ProviderConfig { ModelName = "gpt-4" }
            },
            typeof(CodeAnalysisPlugin)
        );
    }

    [SubAgent(Category = "Review", Priority = 2)]
    public SubAgent PerformanceReviewer()
    {
        return SubAgentFactory.Create(
            "PerformanceReviewer",
            "Reviews code for performance issues and optimization opportunities",
            new AgentConfig
            {
                SystemInstructions = "You are a performance engineer. Identify bottlenecks...",
                MaxAgenticIterations = 15,
                Provider = new ProviderConfig { ModelName = "gpt-4" }
            },
            typeof(CodeAnalysisPlugin)
        );
    }

    [SubAgent(Category = "Review", Priority = 1)]
    public SubAgent StyleReviewer()
    {
        return SubAgentFactory.Create(
            "StyleReviewer",
            "Reviews code style and best practices",
            new AgentConfig
            {
                SystemInstructions = "You enforce code style. Check formatting, naming...",
                MaxAgenticIterations = 10,
                Provider = new ProviderConfig { ModelName = "gemini-2.0-flash" }
            },
            typeof(CodeAnalysisPlugin)
        );
    }
}
```

---

### **Example 3: Research Assistant with Memory**

```csharp
public class ResearchAssistants
{
    [SubAgent]
    public SubAgent Librarian()
    {
        return SubAgentFactory.CreateStateful(  // Remembers what was researched
            "Librarian",
            "Searches for and summarizes academic papers and articles",
            new AgentConfig
            {
                SystemInstructions = @"
                    You are a research librarian.
                    Find and summarize relevant academic sources.
                    Track what you've already researched to avoid duplication.",
                Provider = new ProviderConfig { ModelName = "gpt-4" }
            },
            typeof(WebSearchPlugin), typeof(ArxivPlugin), typeof(ScholarPlugin)
        );
    }

    [SubAgent]
    public SubAgent Synthesizer()
    {
        return SubAgentFactory.Create(
            "Synthesizer",
            "Synthesizes research findings into coherent summaries",
            new AgentConfig
            {
                SystemInstructions = @"
                    You synthesize research.
                    Identify patterns, contradictions, and gaps.
                    Create comprehensive summaries with citations.",
                Provider = new ProviderConfig { ModelName = "claude-3-5-sonnet" }
            });
    }
}
```

**Usage:**
```csharp
User: "Research machine learning interpretability"
→ Librarian finds papers (remembers what was found)

User: "Find more on SHAP values specifically"
→ Librarian searches with context of previous research

User: "Synthesize everything you found"
→ Synthesizer creates summary from all findings
```

---

## Troubleshooting

### **Issue: SubAgent not being called**

**Symptom:** Orchestrator doesn't invoke your SubAgent

**Causes:**
1. **Poor description** - Description doesn't match user query
2. **Wrong system instructions** - Orchestrator told not to use tools
3. **Not registered** - Plugin not added with `.WithPlugin<>`

**Solution:**
```csharp
// ✅ Make description match user intent
return SubAgentFactory.Create(
    "PythonExpert",
    "Expert Python programmer. Call this when user asks about Python code, debugging, or libraries.",
    config);

// ✅ Don't restrict tool use in orchestrator
new AgentConfig
{
    SystemInstructions = "You are helpful. Use available tools to answer questions."
    // ❌ NOT: "Answer questions yourself. Never use tools."
}
```

---

### **Issue: SubAgent called too often**

**Symptom:** SubAgent invoked for simple questions it shouldn't handle

**Solution:**
```csharp
// ✅ Be specific about when to call
return SubAgentFactory.Create(
    "DatabaseExpert",
    "Call ONLY for complex SQL queries, database design, or performance tuning. NOT for simple database questions.",
    config);
```

---

### **Issue: SharedThread context gets too long**

**Symptom:** SubAgent becomes slow or runs out of context

**Solution:**
```csharp
// Option 1: Use dynamic memory to auto-summarize
new AgentConfig
{
    Memory = new MemoryConfig
    {
        DynamicMemory = new DynamicMemoryConfig
        {
            Enabled = true,
            MaxTokens = 4000  // Auto-summarize when exceeded
        }
    }
}

// Option 2: Switch to Stateless if context isn't needed
return SubAgentFactory.Create(/* ... */);  // Not CreateStateful
```

---

### **Issue: Events not showing up**

**Symptom:** Can't see events from SubAgent

**Causes:**
1. Not subscribed to event stream
2. Event coordinator not set up

**Solution:**
```csharp
// ✅ Subscribe to events
orchestrator.OnEventAsync(async evt =>
{
    var agentName = evt.ExecutionContext?.AgentName;
    Console.WriteLine($"[{agentName}] {evt.GetType().Name}");
});

// Then run
await orchestrator.RunAsync(/* ... */);
```

---

## Next Steps

- **API Reference**: Detailed API documentation → [API_REFERENCE.md](API_REFERENCE.md)
- **Architecture**: How SubAgents work internally → [ARCHITECTURE.md](ARCHITECTURE.md)
- **Examples**: Full working examples → `examples/SubAgents/`

---

## Summary

SubAgents enable powerful multi-agent architectures:

✅ **Specialization** - Different agents for different tasks
✅ **Tool segregation** - Each agent has only what it needs
✅ **Observable** - Full event tracking with attribution
✅ **Flexible** - Three thread modes for different use cases
✅ **Composable** - Mix and match agents freely

Start simple with one or two SubAgents, then grow your agent ecosystem as needed!
