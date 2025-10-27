using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Generates code for skill registration
/// </summary>
internal static class SkillCodeGenerator
{
    /// <summary>
    /// Generates the GetReferencedPlugins() method for auto-registration
    /// </summary>
    public static string GenerateGetReferencedPluginsMethod(PluginInfo plugin)
    {
        if (!plugin.Skills.Any())
            return string.Empty;

        var allReferencedPlugins = plugin.Skills
            .SelectMany(s => s.ResolvedPluginTypes)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        if (!allReferencedPlugins.Any())
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets the list of plugins referenced by skills in this class");
        sb.AppendLine("        /// Used by AgentBuilder for auto-registration");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static string[] GetReferencedPlugins()");
        sb.AppendLine("        {");
        sb.AppendLine("            return new[]");
        sb.AppendLine("            {");

        for (int i = 0; i < allReferencedPlugins.Count; i++)
        {
            var comma = i < allReferencedPlugins.Count - 1 ? "," : "";
            sb.AppendLine($"                \"{allReferencedPlugins[i]}\"{comma}");
        }

        sb.AppendLine("            };");
        sb.AppendLine("        }");

        return sb.ToString();
    }

    /// <summary>
    /// Generates skill registration code to be added to CreatePlugin() method
    /// </summary>
    public static string GenerateSkillRegistrations(PluginInfo plugin)
    {
        if (!plugin.Skills.Any())
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("        // Add skill functions");

        foreach (var skill in plugin.Skills)
        {
            sb.AppendLine($"        functions.Add(Create{skill.MethodName}Skill(context));");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates skill container function (for SkillScopingMode.Scoped skills)
    /// </summary>
    public static string GenerateSkillContainerFunction(SkillInfo skill)
    {
        if (skill.Options.ScopingMode != "Scoped")
            return string.Empty;

        var sb = new StringBuilder();

        var functionList = string.Join(", ", skill.ResolvedFunctionReferences);
        var returnMessage = $"{skill.Name} skill expanded. Available functions: {functionList}";

        if (!string.IsNullOrEmpty(skill.Instructions))
        {
            returnMessage += $"\n\n{skill.Instructions}";
        }

        var escapedReturnMessage = returnMessage.Replace("\"", "\"\"");

        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Container function for {skill.Name} skill (Scoped mode).");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        private static AIFunction Create{skill.MethodName}SkillContainer()");
        sb.AppendLine("        {");
        sb.AppendLine("            return HPDAIFunctionFactory.Create(");
        sb.AppendLine("                async (arguments, cancellationToken) =>");
        sb.AppendLine("                {");
        sb.AppendLine($"                    return @\"{escapedReturnMessage}\";");
        sb.AppendLine("                },");
        sb.AppendLine("                new HPDAIFunctionFactoryOptions");
        sb.AppendLine("                {");
        sb.AppendLine($"                    Name = \"{skill.Name}\",");
        sb.AppendLine($"                    Description = \"{skill.Description}\",");
        sb.AppendLine("                    SchemaProvider = () => CreateEmptyContainerSchema(),");
        sb.AppendLine("                    AdditionalProperties = new Dictionary<string, object>");
        sb.AppendLine("                    {");
        sb.AppendLine("                        [\"IsContainer\"] = true,");
        sb.AppendLine("                        [\"IsSkill\"] = true,");
        sb.AppendLine($"                        [\"ParentSkillContainer\"] = \"{skill.ContainingClass.Identifier.ValueText}\",");
        sb.AppendLine($"                        [\"ReferencedFunctions\"] = new[] {{ {string.Join(", ", skill.ResolvedFunctionReferences.Select(f => $"\"{f}\""))} }},");
        sb.AppendLine($"                        [\"ReferencedPlugins\"] = new[] {{ {string.Join(", ", skill.ResolvedPluginTypes.Select(p => $"\"{p}\""))} }},");
        sb.AppendLine($"                        [\"ScopingMode\"] = \"Scoped\",");
        sb.AppendLine($"                        [\"AutoExpand\"] = {skill.Options.AutoExpand.ToString().ToLower()}");
        sb.AppendLine("                    }");
        sb.AppendLine("                });");
        sb.AppendLine("        }");

        return sb.ToString();
    }

    /// <summary>
    /// Generates skill activation function
    /// </summary>
    public static string GenerateSkillFunction(SkillInfo skill)
    {
        var sb = new StringBuilder();

        var instructions = skill.Instructions ?? "Skill activated.";
        var escapedInstructions = instructions.Replace("\"", "\"\"");

        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates AIFunction for {skill.Name} skill.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        private static AIFunction Create{skill.MethodName}Skill(IPluginMetadataContext? context)");
        sb.AppendLine("        {");
        sb.AppendLine("            return HPDAIFunctionFactory.Create(");
        sb.AppendLine("                async (arguments, cancellationToken) =>");
        sb.AppendLine("                {");
        sb.AppendLine($"                    return @\"{escapedInstructions}\";");
        sb.AppendLine("                },");
        sb.AppendLine("                new HPDAIFunctionFactoryOptions");
        sb.AppendLine("                {");
        sb.AppendLine($"                    Name = \"{skill.Name}\",");
        sb.AppendLine($"                    Description = \"{skill.Description}\",");
        sb.AppendLine("                    SchemaProvider = () => CreateEmptyContainerSchema(),");
        sb.AppendLine("                    AdditionalProperties = new Dictionary<string, object>");
        sb.AppendLine("                    {");
        sb.AppendLine("                        [\"IsSkill\"] = true,");
        sb.AppendLine($"                        [\"ParentSkillContainer\"] = \"{skill.ContainingClass.Identifier.ValueText}\",");
        sb.AppendLine($"                        [\"ReferencedFunctions\"] = new[] {{ {string.Join(", ", skill.ResolvedFunctionReferences.Select(f => $"\"{f}\""))} }},");
        sb.AppendLine($"                        [\"ReferencedPlugins\"] = new[] {{ {string.Join(", ", skill.ResolvedPluginTypes.Select(p => $"\"{p}\""))} }},");
        sb.AppendLine($"                        [\"ScopingMode\"] = \"{skill.Options.ScopingMode}\",");
        sb.AppendLine($"                        [\"AutoExpand\"] = {skill.Options.AutoExpand.ToString().ToLower()}");
        sb.AppendLine("                    }");
        sb.AppendLine("                });");
        sb.AppendLine("        }");

        return sb.ToString();
    }

    /// <summary>
    /// Generates all skill-related code for a plugin
    /// </summary>
    public static string GenerateAllSkillCode(PluginInfo plugin)
    {
        if (!plugin.Skills.Any())
            return string.Empty;

        var sb = new StringBuilder();

        // Generate skill functions
        foreach (var skill in plugin.Skills)
        {
            sb.AppendLine();
            sb.AppendLine(GenerateSkillFunction(skill));

            // Generate container if Scoped
            if (skill.Options.ScopingMode == "Scoped")
            {
                sb.AppendLine();
                sb.AppendLine(GenerateSkillContainerFunction(skill));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Updates the plugin metadata to include skills
    /// </summary>
    public static string UpdatePluginMetadataWithSkills(PluginInfo plugin, string originalMetadataCode)
    {
        if (!plugin.Skills.Any())
            return originalMetadataCode;

        // Add skill information to metadata
        var sb = new StringBuilder();
        sb.AppendLine("        private static PluginMetadata? _cachedMetadata;");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Gets metadata for the {plugin.Name} plugin (used for scoping).");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static PluginMetadata GetPluginMetadata()");
        sb.AppendLine("        {");
        sb.AppendLine("            return _cachedMetadata ??= new PluginMetadata");
        sb.AppendLine("            {");
        sb.AppendLine($"                Name = \"{plugin.Name}\",");

        var description = plugin.HasScopeAttribute && !string.IsNullOrEmpty(plugin.ScopeDescription)
            ? plugin.ScopeDescription
            : plugin.Description;
        sb.AppendLine($"                Description = \"{description}\",");

        // Include both functions and skills
        var allFunctionNames = plugin.Functions.Select(f => f.FunctionName)
            .Concat(plugin.Skills.Select(s => s.Name))
            .ToList();
        var functionNamesArray = string.Join(", ", allFunctionNames.Select(n => $"\"{n}\""));

        sb.AppendLine($"                FunctionNames = new[] {{ {functionNamesArray} }},");
        sb.AppendLine($"                FunctionCount = {allFunctionNames.Count},");
        sb.AppendLine($"                HasScopeAttribute = {plugin.HasScopeAttribute.ToString().ToLower()},");
        sb.AppendLine($"                IsSkillContainer = {plugin.IsSkillOnly.ToString().ToLower()}");
        sb.AppendLine("            };");
        sb.AppendLine("        }");

        return sb.ToString();
    }
}
