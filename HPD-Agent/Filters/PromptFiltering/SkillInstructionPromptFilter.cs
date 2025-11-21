using System.Collections.Immutable;
using System.Text;
using Microsoft.Extensions.AI;

namespace HPD.Agent.Internal.Filters;

/// <summary>
/// Injects active skill instructions into ChatOptions.Instructions (ephemeral during skill expansion).
/// Mirrors container result filtering pattern: instructions visible during skill turns only.
/// MUST be FIRST in filter chain to ensure highest priority.
/// ALWAYS injects to system prompt. Whether instructions also appear in function result is controlled
/// by ScopingConfig.SkillInstructionMode (configured at build time, checked by source-generated code).
/// </summary>
internal class SkillInstructionPromptFilter : IPromptFilter
{
    public async Task<IEnumerable<ChatMessage>> InvokeAsync(
        PromptFilterContext context,
        Func<PromptFilterContext, Task<IEnumerable<ChatMessage>>> next)
    {
        // Extract expanded skills and their instructions from context
        var expandedSkills = context.GetExpandedSkills();
        var skillInstructions = context.GetSkillInstructions();

        if (expandedSkills != null && expandedSkills.Count > 0 &&
            skillInstructions != null && skillInstructions.Count > 0 &&
            context.Options != null)
        {
            // Build skill protocols section from metadata
            // This includes function lists, documents, and instructions
            var protocolsSection = BuildSkillProtocolsSection(
                expandedSkills,
                skillInstructions,
                context.Options);

            if (!string.IsNullOrEmpty(protocolsSection))
            {
                // Inject BEFORE existing instructions (highest priority)
                var currentInstructions = context.Options.Instructions ?? string.Empty;

                // Avoid duplicate injection - check if protocols already present
                if (!currentInstructions.Contains("🔧 ACTIVE SKILL PROTOCOLS"))
                {
                    context.Options.Instructions = string.IsNullOrEmpty(currentInstructions)
                        ? protocolsSection
                        : $"{protocolsSection}\n\n{currentInstructions}";
                }
            }
        }

        // Continue pipeline
        return await next(context);
    }

    private static string BuildSkillProtocolsSection(
        ImmutableHashSet<string> expandedSkills,
        ImmutableDictionary<string, string> skillInstructions,
        ChatOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine("🔧 ACTIVE SKILL PROTOCOLS (Execute ALL steps completely)");
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine();

        // Order alphabetically for consistency
        foreach (var skillName in expandedSkills.OrderBy(s => s))
        {
            if (skillInstructions.TryGetValue(skillName, out var instructions))
            {
                sb.AppendLine($"## {skillName}:");
                sb.AppendLine();

                // Find the skill's AIFunction to extract metadata
                var skillFunction = options.Tools?.OfType<AIFunction>()
                    .FirstOrDefault(f => f.Name == skillName);

                if (skillFunction != null)
                {
                    // Add function list from metadata
                    if (skillFunction.AdditionalProperties?.TryGetValue("ReferencedFunctions", out var functionsObj) == true
                        && functionsObj is string[] functions && functions.Length > 0)
                    {
                        sb.AppendLine($"**Available functions:** {string.Join(", ", functions)}");
                        sb.AppendLine();
                    }

                    // Add document information from metadata
                    var hasDocuments = BuildDocumentSection(skillFunction, sb);
                    if (hasDocuments)
                    {
                        sb.AppendLine();
                    }
                }

                // Add the skill instructions
                sb.AppendLine(instructions);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static bool BuildDocumentSection(AIFunction skillFunction, StringBuilder sb)
    {
        var hasAnyDocuments = false;

        // Check for DocumentUploads
        if (skillFunction.AdditionalProperties?.TryGetValue("DocumentUploads", out var uploadsObj) == true
            && uploadsObj is Array uploadsArray && uploadsArray.Length > 0)
        {
            if (!hasAnyDocuments)
            {
                sb.AppendLine("📚 **Available Documents:**");
                hasAnyDocuments = true;
            }

            foreach (var upload in uploadsArray)
            {
                if (upload is Dictionary<string, string> uploadDict)
                {
                    var docId = uploadDict.GetValueOrDefault("DocumentId", "");
                    var description = uploadDict.GetValueOrDefault("Description", "");
                    sb.AppendLine($"- {docId}: {description}");
                }
            }
        }

        // Check for DocumentReferences
        if (skillFunction.AdditionalProperties?.TryGetValue("DocumentReferences", out var refsObj) == true
            && refsObj is Array refsArray && refsArray.Length > 0)
        {
            if (!hasAnyDocuments)
            {
                sb.AppendLine("📚 **Available Documents:**");
                hasAnyDocuments = true;
            }

            foreach (var reference in refsArray)
            {
                if (reference is Dictionary<string, string?> refDict)
                {
                    var docId = refDict.GetValueOrDefault("DocumentId", "");
                    var description = refDict.GetValueOrDefault("DescriptionOverride")
                        ?? "[Use read_skill_document to view]";
                    sb.AppendLine($"- {docId}: {description}");
                }
            }
        }

        if (hasAnyDocuments)
        {
            sb.AppendLine();
            sb.AppendLine("Use `read_skill_document(documentId)` to retrieve document content.");
        }

        return hasAnyDocuments;
    }

    public Task PostInvokeAsync(PostInvokeContext context, CancellationToken cancellationToken)
    {
        // No post-processing needed
        return Task.CompletedTask;
    }
}
