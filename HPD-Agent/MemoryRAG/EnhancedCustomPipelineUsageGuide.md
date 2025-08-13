# Updated Custom Pipeline Usage Guide

## What We've Enhanced ✅

All three memory builders now support **three different ways** to register custom handlers:

### 1. **Type Registration** (for handlers with DI dependencies)
```csharp
.WithCustomHandler<MyAdvancedHandler>("advanced_step")
```

### 2. **Factory Registration** (for complex dependency scenarios)
```csharp
.WithCustomHandler<MyHandler>("my_step", provider => 
    new MyHandler(
        provider.GetService<IDocumentStorage>(),
        provider.GetService<ILogger<MyHandler>>()))
```

### 3. **Instance Registration** (for simple handlers)
```csharp
.WithCustomHandler("simple_log", new SimpleLoggingHandler())
```

## Updated Usage Examples

### **AgentMemoryBuilder - Enhanced**
```csharp
// Enterprise-grade agent with multiple handler types
var result = new AgentMemoryBuilder("enterprise-agent")
    // Simple handler instance
    .WithCustomHandler("audit_start", new AuditStartHandler())
    
    // Factory with dependencies
    .WithCustomHandler<SecurityValidator>("security_check", provider => 
        new SecurityValidator(
            provider.GetService<ISecurityService>(),
            provider.GetService<ILogger<SecurityValidator>>()))
    
    // Type registration (requires DI setup)
    .WithCustomHandler<ComplianceHandler>("compliance")
    
    // Define the pipeline
    .WithCustomPipeline("audit_start", "security_check", "compliance", "extract", "partition", "gen_embeddings", "save_records")
    .Build();

// Enhanced wrapper with enterprise features
if (result is CustomPipelineMemoryWrapper wrapper)
{
    // Use the configured enterprise pipeline
    var docId = await wrapper.ImportDocumentWithDefaultPipelineAsync("sensitive-doc.pdf");
    
    // Access underlying memory for queries
    var answer = await wrapper.Memory.AskAsync("What compliance requirements apply?");
    Console.WriteLine($"Compliance Answer: {answer.Result}");
}
```

### **ConversationMemoryBuilder - Enhanced**
```csharp
// Conversation memory with content moderation and privacy protection
var result = new ConversationMemoryBuilder("secure-conversation")
    // Privacy protection handler
    .WithCustomHandler("privacy_filter", new PrivacyFilterHandler())
    
    // Content moderation with AI service
    .WithCustomHandler<ContentModerationHandler>("moderation", provider =>
        new ContentModerationHandler(provider.GetService<IAIContentModerator>()))
    
    // Custom pipeline for secure conversations
    .WithCustomPipeline("privacy_filter", "moderation", "extract", "partition", "gen_embeddings", "save_records")
    .Build();

if (result is CustomPipelineMemoryWrapper conversationWrapper)
{
    // Import conversation with privacy and moderation
    var chatDoc = new Document("chat-session-001")
        .AddFile("meeting-transcript.txt")
        .AddTag("conversation", "secure-conversation")
        .AddTag("privacy-level", "high");
    
    var docId = await conversationWrapper.ImportDocumentWithDefaultPipelineAsync(chatDoc);
    Console.WriteLine($"Secure conversation document imported: {docId}");
}
```

### **ProjectMemoryBuilder - Enhanced**
```csharp
// Project memory with team collaboration and quality assurance
var result = new ProjectMemoryBuilder("team-project")
    // Quality gate handler
    .WithCustomHandler("quality_gate", new QualityGateHandler())
    
    // Team notification with service integration
    .WithCustomHandler<TeamNotificationHandler>("notify_team", provider =>
        new TeamNotificationHandler(
            provider.GetService<ITeamNotificationService>(),
            provider.GetService<IProjectConfiguration>()))
    
    // Metadata enrichment for project context
    .WithCustomHandler<ProjectMetadataHandler>("enrich_metadata")
    
    .WithCustomPipeline("quality_gate", "enrich_metadata", "extract", "partition", "gen_embeddings", "save_records", "notify_team")
    .WithMultiUserAccess()
    .WithRuntimeManagement()
    .Build();

if (result is CustomPipelineMemoryWrapper projectWrapper)
{
    // Import with team collaboration pipeline
    var projectDoc = new Document("architecture-spec-v3")
        .AddFile("system-design.pdf")
        .AddFile("api-spec.yaml")
        .AddTag("project", "team-project")
        .AddTag("version", "3.0")
        .AddTag("reviewer", "tech-lead");
    
    var docId = await projectWrapper.ImportDocumentWithDefaultPipelineAsync(projectDoc);
    Console.WriteLine($"Team project document imported with quality checks: {docId}");
}
```

## Runtime Flexibility Examples

### **Different Pipelines for Different Document Types**
```csharp
// Build memory with multiple registered handlers
var result = new AgentMemoryBuilder("flexible-agent")
    .WithCustomHandler("public_validation", new PublicContentValidator())
    .WithCustomHandler("sensitive_validation", new SensitiveContentValidator())
    .WithCustomHandler("audit_log", new AuditLogger())
    .Build();

IKernelMemory memory = result is CustomPipelineMemoryWrapper wrapper ? wrapper.Memory : (IKernelMemory)result;

// Different pipelines for different security levels
await memory.ImportDocumentWithCustomPipelineAsync(
    "public-announcement.pdf",
    new[] { "public_validation", "extract", "partition", "gen_embeddings", "save_records" });

await memory.ImportDocumentWithCustomPipelineAsync(
    "classified-report.pdf", 
    new[] { "audit_log", "sensitive_validation", "extract", "partition", "gen_embeddings", "save_records" });
```

### **Service Integration Pattern**
```csharp
// Set up dependency injection
services.AddSingleton<IDocumentStorage, MyDocumentStorage>();
services.AddSingleton<IAuditService, MyAuditService>();
services.AddLogging();

// Build with service-dependent handlers
var result = new ProjectMemoryBuilder("service-integrated-project")
    // These handlers will get their dependencies from the service provider
    .WithCustomHandler<DocumentAuditHandler>("audit")
    .WithCustomHandler<ComplianceCheckHandler>("compliance")
    .WithCustomPipeline("audit", "compliance", "extract", "partition", "gen_embeddings", "save_records")
    .Build();
```

## Key Benefits of Enhanced Implementation

### **1. Flexibility** 🎯
- **Simple handlers**: Direct instance registration
- **Complex handlers**: Factory pattern with full dependency control
- **Enterprise handlers**: Type registration with DI container

### **2. Discoverability** 🔍
```csharp
// Users can inspect what's configured
if (result is CustomPipelineMemoryWrapper wrapper)
{
    var steps = wrapper.GetRegisteredPipelineSteps();
    Console.WriteLine($"Pipeline: {string.Join(" → ", steps)}");
}
```

### **3. Runtime Adaptability** ⚡
```csharp
// Same memory instance, different pipelines for different needs
await memory.ImportDocumentWithCustomPipelineAsync(doc1, securityPipeline);
await memory.ImportDocumentWithCustomPipelineAsync(doc2, standardPipeline);
await memory.ImportDocumentWithCustomPipelineAsync(doc3, fastPipeline);
```

### **4. Service Integration** 🔌
```csharp
// Seamless integration with existing enterprise services
.WithCustomHandler<EnterpriseHandler>("enterprise", provider => 
    new EnterpriseHandler(
        provider.GetService<IEnterpriseSecurityService>(),
        provider.GetService<IComplianceService>(),
        provider.GetService<IAuditService>()))
```

## Summary

The enhanced custom pipeline implementation provides:

- **Three registration patterns** for different complexity levels
- **Type-safe handler factories** with full dependency injection support
- **Runtime pipeline flexibility** via extension methods and wrapper
- **Enterprise-ready** service integration capabilities
- **Backward compatibility** with existing code

Users can now build everything from simple logging pipelines to complex enterprise-grade content processing workflows, all while maintaining the clean, discoverable API that your framework is known for.
