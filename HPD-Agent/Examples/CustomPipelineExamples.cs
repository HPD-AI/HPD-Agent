using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Pipeline;
using HPD_Agent.MemoryRAG;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HPD_Agent.Examples
{
    /// <summary>
    /// Example custom handler for logging pipeline steps
    /// </summary>
    public class LoggingPipelineHandler : IPipelineStepHandler
    {
        private readonly string _stepName;

        public LoggingPipelineHandler(string stepName)
        {
            _stepName = stepName;
        }

        public string StepName => _stepName;

        public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(
            DataPipeline pipeline, 
            CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Executing custom step: {_stepName}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Processing {pipeline.Files.Count} files");
            
            // Your custom logic here
            await Task.Delay(100, cancellationToken); // Simulate processing
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Completed step: {_stepName}");
            
            return (ReturnType.Success, pipeline);
        }
    }

    /// <summary>
    /// Example custom handler for content validation
    /// </summary>
    public class ContentValidationHandler : IPipelineStepHandler
    {
        public string StepName => "validate_content";

        public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(
            DataPipeline pipeline, 
            CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Validating content...");
            
            // Add validation logic here
            // For example, check file sizes, content types, etc.
            foreach (var file in pipeline.Files)
            {
                if (file.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Rejecting executable file: {file.Name}");
                    return (ReturnType.FatalError, pipeline);
                }
            }
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Content validation passed");
            await Task.CompletedTask; // Remove warning about async method without await
            return (ReturnType.Success, pipeline);
        }
    }

    /// <summary>
    /// Examples demonstrating how to use the custom pipeline functionality
    /// </summary>
    public static class CustomPipelineExamples
    {
        /// <summary>
        /// Example 1: Basic usage with simple custom pipeline
        /// </summary>
        public static async Task BasicCustomPipelineExample()
        {
            Console.WriteLine("=== Basic Custom Pipeline Example ===");
            
            // Build memory with custom handlers and pipeline
            var result = new AgentMemoryBuilder("documentProcessor")
                .WithCustomHandler<LoggingPipelineHandler>("log_start")
                .WithCustomHandler<ContentValidationHandler>("validate_content")
                .WithCustomHandler<LoggingPipelineHandler>("log_end")
                .WithCustomPipeline("log_start", "validate_content", "extract", "partition", "gen_embeddings", "save_records", "log_end")
                .Build();

            // Handle different return types
            if (result is CustomPipelineMemoryWrapper wrapper)
            {
                Console.WriteLine($"Custom pipeline configured with steps: {string.Join(" → ", wrapper.GetRegisteredPipelineSteps())}");
                
                // Use the default custom pipeline for runtime ingestion
                var docId = await wrapper.ImportDocumentWithDefaultPipelineAsync("example.pdf");
                Console.WriteLine($"Document imported with ID: {docId}");
                
                // Access the underlying memory for querying
                var answer = await wrapper.Memory.AskAsync("What is this document about?");
                Console.WriteLine($"Answer: {answer.Result}");
            }
            else if (result is IKernelMemory memory)
            {
                Console.WriteLine("Standard memory instance (no custom pipeline)");
                var answer = await memory.AskAsync("What is this document about?");
                Console.WriteLine($"Answer: {answer.Result}");
            }
        }

        /// <summary>
        /// Example 2: Using extension methods for runtime custom pipelines
        /// </summary>
        public static async Task RuntimeCustomPipelineExample()
        {
            Console.WriteLine("=== Runtime Custom Pipeline Example ===");
            
            // Build memory with registered handlers but no default custom pipeline
            var result = new AgentMemoryBuilder("flexibleProcessor")
                .WithCustomHandler<LoggingPipelineHandler>("log_processing")
                .WithCustomHandler<ContentValidationHandler>("validate_content")
                .Build();

            IKernelMemory memory;
            if (result is CustomPipelineMemoryWrapper wrapper)
            {
                memory = wrapper.Memory;
            }
            else
            {
                memory = (IKernelMemory)result;
            }

            // Use different pipelines for different types of documents
            
            // For sensitive documents - include validation
            var sensitiveDocId = await memory.ImportDocumentWithCustomPipelineAsync(
                "sensitive-report.pdf",
                new[] { "validate_content", "extract", "partition", "gen_embeddings", "save_records" });
            
            // For regular documents - include logging
            var regularDocId = await memory.ImportDocumentWithCustomPipelineAsync(
                "regular-doc.docx", 
                new[] { "log_processing", "extract", "partition", "gen_embeddings", "save_records" });
            
            Console.WriteLine($"Sensitive document imported: {sensitiveDocId}");
            Console.WriteLine($"Regular document imported: {regularDocId}");
        }

        /// <summary>
        /// Example 3: Advanced usage with handler factories for dependency injection
        /// </summary>
        public static async Task AdvancedCustomPipelineExample()
        {
            Console.WriteLine("=== Advanced Custom Pipeline Example ===");
            
            // Use factory methods for handlers that need dependencies
            var result = new ConversationMemoryBuilder("conversation123")
                .WithCustomHandler<LoggingPipelineHandler>("audit_log", provider => 
                    new LoggingPipelineHandler("audit_log"))
                .WithCustomHandler<ContentValidationHandler>("security_check")
                .WithCustomPipeline("audit_log", "security_check", "extract", "partition", "gen_embeddings", "save_records")
                .Build();

            if (result is CustomPipelineMemoryWrapper conversationWrapper)
            {
                Console.WriteLine("Conversation memory with custom security pipeline");
                
                // Import conversation documents with security checks
                var chatDoc = new Document("chat-001")
                    .AddFile("meeting-transcript.txt")
                    .AddTag("conversation", "conversation123")
                    .AddTag("security-level", "confidential");
                
                var docId = await conversationWrapper.ImportDocumentWithDefaultPipelineAsync(chatDoc);
                Console.WriteLine($"Secure conversation document imported: {docId}");
            }
        }

        /// <summary>
        /// Example 4: Project memory with custom pipeline for team collaboration
        /// </summary>
        public static async Task ProjectMemoryCustomPipelineExample()
        {
            Console.WriteLine("=== Project Memory Custom Pipeline Example ===");
            
            var result = new ProjectMemoryBuilder("project-alpha")
                .WithCustomHandler<LoggingPipelineHandler>("team_audit")
                .WithCustomHandler<ContentValidationHandler>("quality_check")
                .WithCustomPipeline("team_audit", "quality_check", "extract", "partition", "gen_embeddings", "save_records")
                .WithMultiUserAccess()
                .WithRuntimeManagement()
                .Build();

            if (result is CustomPipelineMemoryWrapper projectWrapper)
            {
                Console.WriteLine("Project memory with team collaboration pipeline");
                
                // Import project documents with quality checks
                var projectDoc = new Document("design-spec-v2")
                    .AddFile("architecture-spec.pdf")
                    .AddFile("api-documentation.md")
                    .AddTag("project", "project-alpha")
                    .AddTag("version", "2.0")
                    .AddTag("team", "backend");
                
                var docId = await projectWrapper.ImportDocumentWithDefaultPipelineAsync(projectDoc);
                Console.WriteLine($"Project document imported with quality pipeline: {docId}");
                
                // Query the project knowledge
                var answer = await projectWrapper.Memory.AskAsync(
                    "What are the main architectural components?",
                    filter: MemoryFilters.ByTag("project", "project-alpha"));
                
                Console.WriteLine($"Project Answer: {answer.Result}");
            }
        }

        /// <summary>
        /// Example showing all custom pipeline usage patterns
        /// </summary>
        public static async Task RunAllExamples()
        {
            await BasicCustomPipelineExample();
            Console.WriteLine();
            
            await RuntimeCustomPipelineExample();
            Console.WriteLine();
            
            await AdvancedCustomPipelineExample();
            Console.WriteLine();
            
            await ProjectMemoryCustomPipelineExample();
        }
    }
}
