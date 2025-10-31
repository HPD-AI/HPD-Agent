# Project System Guide

**Multi-conversation containers with shared document context**

---

## Table of Contents

- [Overview](#overview)
- [Core Concepts](#core-concepts)
- [Getting Started](#getting-started)
- [Thread Association Patterns](#thread-association-patterns)
- [Document Management](#document-management)
- [Project Management](#project-management)
- [Use Cases](#use-cases)
- [Architecture](#architecture)
- [API Reference](#api-reference)

---

## Overview

The **Project** class is HPD-Agent's solution for organizing multiple related conversations with shared context. Think of it like:

- **Slack workspaces** - Multiple channels sharing team context
- **VS Code projects** - Multiple files sharing project configuration
- **Notion databases** - Multiple pages sharing common properties

### The Problem It Solves

Most agent frameworks treat each conversation as isolated. But real-world applications need:

- ✅ **Multiple related conversations** (frontend, backend, database discussions)
- ✅ **Shared knowledge** across conversations (API specs, company policies, research papers)
- ✅ **Conversation organization** (search, filter, track activity)
- ✅ **Persistent context** (team members access the same knowledge base)

### Key Features

- 🗂️ **Multi-conversation container** - Organize related threads in one project
- 📄 **Shared document context** - Upload once, available in all conversations
- 🔍 **Built-in search** - Find conversations by content
- 📊 **Project analytics** - Summary, activity tracking, metadata
- 🔄 **Flexible thread association** - Create threads independently or within projects
- 🚀 **Zero vector DB required** - Full-text injection, no infrastructure

---

## Core Concepts

### Projects

A **Project** is a container for:
- Multiple conversation threads
- Shared documents (PDFs, DOCX, URLs, Markdown)
- Project metadata (name, description, timestamps)
- Activity tracking

### Threads

A **ConversationThread** is:
- An individual conversation (message history)
- Optionally associated with a project
- Automatically receives project documents when associated

### Document Injection

When a thread is associated with a project:
1. Documents uploaded to the project are extracted (PDF → text, etc.)
2. Document text is automatically injected into every conversation turn
3. Agent sees documents wrapped in `[PROJECT_DOCUMENTS_START]...[/PROJECT_DOCUMENTS_END]` tags
4. Documents are cached (2-minute TTL) for performance

---

## Getting Started

### Creating a Project

```csharp
using HPD_Agent;

// Create a new project
var project = Project.Create("E-Commerce Rebuild");

// Optional: Specify custom storage directory
var project = Project.Create(
    "E-Commerce Rebuild",
    storageDirectory: "./my-projects"
);
```

### Adding Documents

```csharp
// Upload local files
await project.UploadDocumentAsync(
    "./docs/api-design-standards.md",
    description: "Team API design guidelines"
);

await project.UploadDocumentAsync(
    "./specs/database-schema.pdf",
    description: "Current database architecture"
);

// Upload from URLs
await project.UploadDocumentFromUrlAsync(
    "https://docs.stripe.com/api",
    description: "Stripe API reference"
);
```

**Supported formats:**
- `.txt`, `.md` - Plain text and Markdown
- `.pdf` - PDF documents
- `.docx` - Word documents
- URLs - Web pages (HTML extraction)

### Creating Conversations

```csharp
var agent = new AgentBuilder()
    .WithName("Assistant")
    .WithProvider("openai", "gpt-4o", apiKey)
    .Build();

// Create thread within project
var thread = project.CreateThread();

// Agent automatically sees project documents
await agent.RunAsync("What are our API design standards?", thread);
// Agent response will reference the uploaded design-standards.md
```

---

## Thread Association Patterns

HPD-Agent supports **three flexible patterns** for associating threads with projects:

### Pattern 1: Project Creates Thread (Recommended)

The simplest approach - let the project create the thread.

```csharp
var project = Project.Create("Customer Support");
var thread = project.CreateThread();

await agent.RunAsync("Hello", thread);
// Thread automatically has access to project documents
```

**When to use:**
- Starting fresh conversations within a known project
- Most common use case
- Clearest intent

---

### Pattern 2: Thread Joins Project Later

Create the thread independently, then associate it with a project when needed.

```csharp
var project = Project.Create("Research");
var thread = new ConversationThread();

// Use thread independently first
await agent.RunAsync("What is quantum computing?", thread);

// Later: add to project for shared context
project.AddThread(thread);

// Now thread receives project documents
await agent.RunAsync("Explain based on our research papers", thread);
```

**When to use:**
- Upgrading a standalone conversation to project context
- Conditionally joining projects based on conversation content
- Multi-tenancy scenarios (determine project after auth)

---

### Pattern 3: Direct Thread Association

Explicitly associate thread with project using `SetProject()`.

```csharp
var project = Project.Create("ML Research");
var thread = new ConversationThread();

// Explicit association
thread.SetProject(project);

await agent.RunAsync("Summarize our transformer papers", thread);
```

**When to use:**
- Maximum explicitness in code
- Building custom thread management abstractions
- Testing and debugging scenarios

---

### Querying Project Association

Check if a thread is associated with a project:

```csharp
var thread = new ConversationThread();

var project = thread.GetProject(); // Returns null

thread.SetProject(myProject);
project = thread.GetProject(); // Returns myProject
```

---

## Document Management

### Uploading Documents

```csharp
// Upload with description
var doc = await project.UploadDocumentAsync(
    "./contracts/acme-corp.pdf",
    description: "Acme Corp service agreement"
);

// Document metadata
Console.WriteLine($"Document ID: {doc.Id}");
Console.WriteLine($"File size: {doc.FileSize} bytes");
Console.WriteLine($"Extracted text: {doc.ExtractedTextLength} characters");
Console.WriteLine($"Uploaded: {doc.UploadedAt}");
```

### Listing Documents

```csharp
var documents = await project.DocumentManager.GetDocumentsAsync();

foreach (var doc in documents)
{
    Console.WriteLine($"{doc.FileName} - {doc.Description}");
    Console.WriteLine($"  Uploaded: {doc.UploadedAt}");
    Console.WriteLine($"  Last accessed: {doc.LastAccessed}");
}
```

### Deleting Documents

```csharp
await project.DocumentManager.DeleteDocumentAsync(documentId);
```

### Getting Combined Document Text

```csharp
// Get all documents as combined text (respects token limit)
var combinedText = await project.DocumentManager.GetCombinedDocumentTextAsync(
    maxTokens: 8000
);

// Documents are returned in priority order (most recently accessed first)
```

---

## Project Management

### Project Metadata

```csharp
var project = Project.Create("API Refactoring");

// Basic properties
Console.WriteLine($"ID: {project.Id}");
Console.WriteLine($"Name: {project.Name}");
Console.WriteLine($"Description: {project.Description}");
Console.WriteLine($"Created: {project.CreatedAt}");
Console.WriteLine($"Last activity: {project.LastActivity}");

// Update properties
project.Name = "API Refactoring v2.0";
project.Description = "Complete API redesign for Q1";
```

### Thread Management

```csharp
// Get thread by ID
var thread = project.GetThread(threadId);

// Remove thread from project
bool removed = project.RemoveThread(threadId);

// Count threads
int count = project.ThreadCount;

// Access all threads
foreach (var thread in project.Threads)
{
    Console.WriteLine($"Thread {thread.Id}: {thread.GetDisplayName()}");
}
```

### Project Summary

```csharp
var summary = await project.GetSummaryAsync();

Console.WriteLine($"Total conversations: {summary.ConversationCount}");
Console.WriteLine($"Active conversations: {summary.ActiveConversationCount}");
Console.WriteLine($"Total messages: {summary.TotalMessages}");
Console.WriteLine($"Documents: {summary.DocumentCount}");
Console.WriteLine($"Last activity: {summary.LastActivity}");
```

### Searching Conversations

```csharp
// Find threads containing specific text
var results = project.SearchThreads("authentication bug", maxResults: 10);

foreach (var thread in results)
{
    Console.WriteLine($"Found in: {thread.GetDisplayName()}");
    Console.WriteLine($"Last activity: {thread.LastActivity}");
}
```

### Most Recent Thread

```csharp
// Get the most recently active thread
var latestThread = project.GetMostRecentThread();

if (latestThread != null)
{
    Console.WriteLine($"Continue conversation: {latestThread.GetDisplayName()}");
}
```

---

## Use Cases

### Use Case 1: Software Team Workspace

Multiple developers working on the same project with shared technical context.

```csharp
var teamProject = Project.Create("API Refactoring Q1");

// Upload shared context
await teamProject.UploadDocumentAsync("./docs/api-design-standards.md");
await teamProject.UploadDocumentAsync("./docs/current-api-schema.json");
await teamProject.UploadDocumentAsync("./docs/migration-plan.md");

// Each developer gets their own conversation
var aliceThread = teamProject.CreateThread(); // Frontend work
var bobThread = teamProject.CreateThread();   // Backend work
var carolThread = teamProject.CreateThread(); // Database migration

// All conversations see the same standards and schema
// But each maintains independent discussion history
```

---

### Use Case 2: Customer Support Context

Multiple support conversations for the same customer with shared customer docs.

```csharp
var supportProject = Project.Create("Acme Corp Support");

// Upload customer-specific documents
await supportProject.UploadDocumentAsync("./customers/acme-contract.pdf");
await supportProject.UploadDocumentAsync("./customers/acme-integrations.md");
await supportProject.UploadDocumentFromUrlAsync("https://acme.com/api-docs");

// Different support conversations for different issues
var billingThread = supportProject.CreateThread();
var technicalThread = supportProject.CreateThread();
var onboardingThread = supportProject.CreateThread();

// All support agents see customer context automatically
```

---

### Use Case 3: Research Project

Multiple analysis conversations sharing research literature.

```csharp
var research = Project.Create("Quantum Computing Literature Review");

// Upload research papers
await research.UploadDocumentFromUrlAsync("https://arxiv.org/abs/2301.12345");
await research.UploadDocumentAsync("./papers/nielsen-chuang-chapter5.pdf");
await research.UploadDocumentAsync("./papers/quantum-algorithms-survey.pdf");

// Different threads for different research aspects
var theoryThread = research.CreateThread();        // Theoretical analysis
var implementationThread = research.CreateThread(); // Implementation details
var comparisonThread = research.CreateThread();     // Algorithm comparisons

// Each conversation has access to all papers
```

---

### Use Case 4: Multi-Tenant SaaS Application

Dynamically associate threads with customer projects after authentication.

```csharp
// User starts conversation without project
var thread = new ConversationThread();
await agent.RunAsync("I need help with my account", thread);

// After authentication, determine customer's project
var customerProject = await GetCustomerProject(userId);

// Add thread to customer's project
customerProject.AddThread(thread);

// Now agent has access to customer-specific docs
await agent.RunAsync("What's my current subscription tier?", thread);
```

---

## Architecture

### Document Injection Pipeline

```
User Message → Agent.RunAsync()
                    ↓
            PromptFilterContext
                    ↓
    ProjectInjectedMemoryFilter.InvokeAsync()
                    ↓
        Checks thread metadata for "Project"
                    ↓
        ProjectDocumentManager.GetDocumentsAsync()
                    ↓
        Builds document context:
        [PROJECT_DOCUMENTS_START]
        [DOCUMENT: file1.pdf]
        ... extracted text ...
        [/DOCUMENT]
        [DOCUMENT: file2.md]
        ... extracted text ...
        [/DOCUMENT]
        [PROJECT_DOCUMENTS_END]
                    ↓
        Injects as System message
                    ↓
                  LLM
```

### Caching Strategy

Documents are cached for performance:
- **Cache duration:** 2 minutes
- **Cache invalidation:** Automatic when documents are added/removed
- **Thread-safe:** Lock-based cache access
- **Callback system:** `DocumentManager.RegisterCacheInvalidationCallback()`

### Storage Structure

```
./project-storage/
├── project-documents_{project-id}.json     # Document metadata
└── injected-memory-storage/                # Document text storage
    └── {project-id}/
        ├── document1.txt
        ├── document2.txt
        └── ...
```

---

## API Reference

### Project Class

#### Static Factory Methods

```csharp
// Create project with default storage
Project.Create(string name, string? storageDirectory = null)
```

#### Properties

```csharp
string Id { get; }                        // Unique project identifier
string Name { get; set; }                 // Project name
string Description { get; set; }          // Project description
DateTime CreatedAt { get; }               // Creation timestamp (UTC)
DateTime LastActivity { get; }            // Last activity timestamp (UTC)
List<ConversationThread> Threads { get; } // All threads in project
ProjectDocumentManager DocumentManager { get; } // Document manager
```

#### Thread Management

```csharp
// Create new thread within project
ConversationThread CreateThread()

// Add existing thread to project
void AddThread(ConversationThread thread)

// Get thread by ID
ConversationThread? GetThread(string threadId)

// Remove thread by ID
bool RemoveThread(string threadId)

// Thread count
int ThreadCount { get; }
```

#### Document Management

```csharp
// Upload local file
Task<ProjectDocument> UploadDocumentAsync(
    string filePath,
    string? description = null,
    CancellationToken cancellationToken = default
)

// Upload from URL
Task<ProjectDocument> UploadDocumentFromUrlAsync(
    string url,
    string? description = null,
    CancellationToken cancellationToken = default
)
```

#### Project Helpers

```csharp
// Get project summary
Task<ProjectSummary> GetSummaryAsync()

// Get most recent thread
ConversationThread? GetMostRecentThread()

// Search threads by text
IEnumerable<ConversationThread> SearchThreads(
    string searchTerm,
    int maxResults = 10
)

// Update last activity
void UpdateActivity()
```

---

### ConversationThread Extensions

#### Project Association

```csharp
// Associate thread with project
void SetProject(Project project)

// Get associated project
Project? GetProject()
```

**Note:** These methods are on `ConversationThread`, not `Project`.

---

### ProjectDocumentManager

```csharp
// Get all documents
Task<List<ProjectDocument>> GetDocumentsAsync(
    CancellationToken cancellationToken = default
)

// Get specific document
Task<ProjectDocument?> GetDocumentAsync(
    string documentId,
    CancellationToken cancellationToken = default
)

// Delete document
Task DeleteDocumentAsync(
    string documentId,
    CancellationToken cancellationToken = default
)

// Get combined document text
Task<string> GetCombinedDocumentTextAsync(
    int maxTokens,
    CancellationToken cancellationToken = default
)

// Set project context
void SetContext(string? context)

// Register cache invalidation callback
void RegisterCacheInvalidationCallback(Action invalidateCallback)
```

---

### ProjectDocument

```csharp
class ProjectDocument
{
    string Id { get; set; }              // Unique document ID
    string FileName { get; set; }         // Original filename
    string OriginalPath { get; set; }     // File path or URL
    string ExtractedText { get; set; }    // Extracted text content
    string MimeType { get; set; }         // Detected MIME type
    long FileSize { get; set; }           // File size in bytes
    int ExtractedTextLength { get; }      // Character count
    DateTime UploadedAt { get; set; }     // Upload timestamp
    DateTime LastAccessed { get; set; }   // Last access timestamp
    string Description { get; set; }      // User description
}
```

---

### ProjectSummary

```csharp
class ProjectSummary
{
    string Id { get; set; }
    string Name { get; set; }
    string Description { get; set; }
    int ConversationCount { get; set; }
    int ActiveConversationCount { get; set; }
    int TotalMessages { get; set; }
    int DocumentCount { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime LastActivity { get; set; }
}
```

---

## Best Practices

### 1. Use Descriptive Project Names

```csharp
// ✅ Good
var project = Project.Create("Acme Corp - Q1 2025 Integration");

// ❌ Bad
var project = Project.Create("Project1");
```

### 2. Add Document Descriptions

```csharp
// ✅ Good
await project.UploadDocumentAsync(
    "./spec.pdf",
    description: "API specification v2.3 - approved by architecture team"
);

// ⚠️ Works but less useful
await project.UploadDocumentAsync("./spec.pdf");
```

### 3. Use Projects for Related Conversations

```csharp
// ✅ Good - Related conversations in one project
var apiProject = Project.Create("API Redesign");
var designThread = apiProject.CreateThread();
var implementationThread = apiProject.CreateThread();
var testingThread = apiProject.CreateThread();

// ❌ Bad - Unrelated conversations in one project
var mixedProject = Project.Create("Everything");
var apiThread = mixedProject.CreateThread();
var marketingThread = mixedProject.CreateThread(); // Should be separate project
```

### 4. Clean Up Old Threads

```csharp
// Remove inactive threads periodically
var cutoffDate = DateTime.UtcNow.AddDays(-30);
var oldThreads = project.Threads
    .Where(t => t.LastActivity < cutoffDate)
    .ToList();

foreach (var thread in oldThreads)
{
    project.RemoveThread(thread.Id);
}
```

### 5. Use Search for Navigation

```csharp
// Find relevant past conversations
var authThreads = project.SearchThreads("authentication", maxResults: 5);

// Resume most relevant conversation
var thread = authThreads.FirstOrDefault() ?? project.CreateThread();
```

---

## Comparison with Other Frameworks

| Feature | LangChain | Semantic Kernel | HPD-Agent |
|---------|-----------|-----------------|-----------|
| Multi-conversation containers | ❌ | ❌ | ✅ Projects |
| Shared document context | ⚠️ Manual | ⚠️ Manual | ✅ Automatic injection |
| Document extraction (PDF, DOCX) | ⚠️ Via loaders | ⚠️ Via plugins | ✅ Built-in |
| URL document support | ⚠️ Manual | ❌ | ✅ Built-in |
| Conversation search | ❌ | ❌ | ✅ Built-in |
| Project analytics | ❌ | ❌ | ✅ Summaries |
| Thread management API | ❌ | ❌ | ✅ Full CRUD |
| Vector DB required | ⚠️ For RAG | ⚠️ For memory | ❌ Full-text injection |
| Flexible thread association | ❌ | ❌ | ✅ Three patterns |

---

## FAQ

### Q: Do I need a vector database to use Projects?

**A:** No. Projects use full-text injection - documents are extracted and injected directly into the prompt context. This is simpler and works great for most use cases (up to ~8000 tokens of documents).

### Q: Can a thread belong to multiple projects?

**A:** No. Each thread can be associated with at most one project. If you need multi-project context, upload the same documents to both projects or use a different architecture.

### Q: What happens if project documents exceed the context window?

**A:** `GetCombinedDocumentTextAsync()` respects a `maxTokens` parameter and only includes documents up to that limit. Documents are returned in priority order (most recently accessed first).

### Q: Can I change a thread's project association?

**A:** Yes. Call `thread.SetProject(newProject)` to change the association. The thread will be removed from the old project and added to the new one.

### Q: Are documents re-extracted on every message?

**A:** No. Extracted text is cached for 2 minutes. The cache is invalidated when documents are added/removed. This balances freshness with performance.

### Q: How do I share projects across application instances?

**A:** Projects are stored on disk. Use a shared `storageDirectory` path accessible to all instances (e.g., network drive, S3 mount). The same project ID will load the same documents.

### Q: Can I use Projects with Microsoft's WorkflowBuilder?

**A:** Yes! Projects work seamlessly with Microsoft Agent Framework. Create threads within a project and pass them to any agent or workflow:

```csharp
var project = Project.Create("Team Workspace");
var thread = project.CreateThread();

var workflow = new WorkflowBuilder(researcherAgent)
    .AddEdge(researcherAgent, coderAgent)
    .Build();

await workflow.RunAsync("Build a feature", thread);
// Both agents see project documents
```

---

## Next Steps

- **[Agent Developer Guide](../docs/Agent-Developer-Documentation.md)** - Build agents that use projects
- **[Configuration Reference](../docs/configuration-reference.md)** - Configure document extraction
- **[Filter Guide](../Filters/PromptFiltering/PROMPT_FILTER_GUIDE.md)** - Understand document injection
- **[Getting Started](../docs/getting-started.md)** - Complete tutorial

---

## Support

If you have questions about the Project system:

- 📧 Email: support@hpd-agent.com
- 📚 Documentation: docs.hpd-agent.com
- 🐛 Issues: GitHub Issues

---

**Projects = Workspaces for Conversations** 🗂️

*Organize, share context, and scale your multi-agent applications.*
