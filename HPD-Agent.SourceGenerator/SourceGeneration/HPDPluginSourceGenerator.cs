using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;



/// <summary>
/// Source generator for HPD-Agent AI plugins. Generates AOT-compatible plugin registration code.
/// </summary>
[Generator]
public class HPDPluginSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all classes with [AIPlugin] attribute
        var pluginClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, ct) => IsPluginClass(node, ct),
                transform: static (ctx, ct) => GetPluginDeclaration(ctx, ct))
            .Where(static plugin => plugin is not null)
            .Collect();
        
        // Generate registration code for each plugin
        context.RegisterSourceOutput(pluginClasses, GeneratePluginRegistrations);
    }
    
    /// <summary>
    /// Determines if a syntax node represents a plugin class.
    /// </summary>
    private static bool IsPluginClass(SyntaxNode node, CancellationToken cancellationToken = default)
    {
        return node is ClassDeclarationSyntax classDecl &&
               classDecl.Modifiers.Any(SyntaxKind.PublicKeyword) &&
               classDecl.Members.OfType<MethodDeclarationSyntax>()
                   .Any(method => method.AttributeLists
                       .SelectMany(attrList => attrList.Attributes)
                       .Any(attr => attr.Name.ToString().Contains("AIFunction")));
    }
    
    /// <summary>
    /// Extracts plugin information from a class declaration.
    /// </summary>
    private static PluginInfo? GetPluginDeclaration(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        
        // Find all methods with [AIFunction] attribute
        var functions = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(method => HasAIFunctionAttribute(method, semanticModel))
            .Select(method => AnalyzeFunction(method, semanticModel))
            .Where(func => func != null)
            .ToList();
        
        // Skip classes with no AI functions
        if (!functions.Any()) return null;
        
        // Get namespace
        var namespaceName = GetNamespace(classDecl);
        
        return new PluginInfo
        {
            Name = classDecl.Identifier.ValueText,
            PluginName = classDecl.Identifier.ValueText, // Use class name as plugin name
            Description = $"Plugin containing {functions.Count} AI functions.", // Auto-generated description
            Namespace = namespaceName,
            Functions = functions!
        };
    }
    
    /// <summary>
    /// Generates plugin registration code for all discovered plugins.
    /// </summary>
    private static void GeneratePluginRegistrations(SourceProductionContext context, ImmutableArray<PluginInfo?> plugins)
    {
        // Always generate a test file to confirm the generator is running
        context.AddSource("_SourceGeneratorTest.g.cs", "// Source generator is running!");
        
        foreach (var plugin in plugins)
        {
            if (plugin != null)
            {
                var source = GeneratePluginRegistration(plugin);
                context.AddSource($"{plugin.Name}Registration.g.cs", source);
            }
        }
    }
    
    /// <summary>
    /// Generates the CreatePlugin method with context support.
    /// </summary>
    private static string GenerateCreatePluginMethod(PluginInfo plugin)
    {
        var unconditionalFunctions = plugin.Functions.Where(f => !f.IsConditional).ToList();
        var conditionalFunctions = plugin.Functions.Where(f => f.IsConditional).ToList();
        
        var sb = new StringBuilder();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates an AIFunction list for the {plugin.Name} plugin.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"instance\">The plugin instance</param>");
        sb.AppendLine($"    /// <param name=\"context\">The execution context (optional)</param>");
        sb.AppendLine($"    public static List<AIFunction> CreatePlugin({plugin.Name} instance, IPluginMetadataContext? context = null)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        var functions = new List<AIFunction>();");
        sb.AppendLine();
        
        // Always included functions
        if (unconditionalFunctions.Any())
        {
            sb.AppendLine($"        // Always included functions");
            foreach (var function in unconditionalFunctions)
            {
                var functionCode = GenerateFunctionRegistration(function);
                sb.AppendLine($"        functions.Add({functionCode});");
            }
            sb.AppendLine();
        }
        
        // Conditionally included functions
        if (conditionalFunctions.Any())
        {
            sb.AppendLine($"        // Conditionally included functions");
            foreach (var function in conditionalFunctions)
            {
                sb.AppendLine($"        if (Evaluate{function.Name}Condition(context))");
                sb.AppendLine($"        {{");
                var functionCode = GenerateFunctionRegistration(function);
                sb.AppendLine($"            functions.Add({functionCode});");
                sb.AppendLine($"        }}");
                sb.AppendLine();
            }
        }
        
        sb.AppendLine($"        return functions;");
        sb.AppendLine($"    }}");
        return sb.ToString();
    }

    /// <summary>
    /// Generates the registration code for a single plugin.
    /// </summary>
    private static string GeneratePluginRegistration(PluginInfo plugin)
    {
        var sb = new StringBuilder();
        
        // File header
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// This code was generated by HPDPluginSourceGenerator");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();
        // disable null and type conflict warnings in generated code
        sb.AppendLine("#pragma warning disable CS8601");
        sb.AppendLine("#pragma warning disable CS0436");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Microsoft.Extensions.AI;");
        sb.AppendLine();
        
        
        // Namespace
        if (!string.IsNullOrEmpty(plugin.Namespace))
        {
            sb.AppendLine($"namespace {plugin.Namespace};");
            sb.AppendLine();
        }
        
        // Registration class
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated registration code for {plugin.Name} plugin with generic context support.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public static partial class {plugin.Name}Registration");
        sb.AppendLine("{");
        
        // CreatePlugin method
        sb.AppendLine(GenerateCreatePluginMethod(plugin));
        
        // Context resolution methods (always generate conditional evaluators if needed)
        bool hasContextMethods = plugin.HasContextAwareMetadata || plugin.Functions.Any(f => f.HasContextAwareMetadata);
        bool hasConditionalEvaluators = plugin.Functions.Any(f => f.IsConditional);
        if (hasContextMethods || hasConditionalEvaluators)
        {
            sb.AppendLine();
            sb.AppendLine(GenerateContextResolutionMethods(plugin));
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates the registration code for a single function.
    /// </summary>
    private static string GenerateFunctionRegistration(FunctionInfo function)
    {
        var parameterList = string.Join(", ", function.Parameters.Select(p => $"{p.Type} {p.Name}"));
        var argumentList = string.Join(", ", function.Parameters.Select(p => p.Name));
        var nameCode = $"\"{function.FunctionName}\"";
        var descriptionCode = function.HasContextAwareMetadata 
            ? $"Resolve{function.Name}Description(context)"
            : $"\"{function.Description}\"";

        // Create options with parameter descriptions
        var optionsCode = GenerateOptionsCode(function, nameCode, descriptionCode);
        
        if (function.IsAsync)
        {
            return $$"""
                HPDAIFunctionFactory.Create(
                        async ({{parameterList}}) => await instance.{{function.Name}}({{argumentList}}),
                        {{optionsCode}}
                    )
                """;
        }
        else
        {
            return $$"""
                HPDAIFunctionFactory.Create(
                        ({{parameterList}}) => instance.{{function.Name}}({{argumentList}}),
                        {{optionsCode}}
                    )
                """;
        }
    }

    /// <summary>
    /// Generates the options code for HPDAIFunctionFactory with parameter descriptions.
    /// </summary>
    private static string GenerateOptionsCode(FunctionInfo function, string nameCode, string descriptionCode)
    {
        var hasParameterDescriptions = function.Parameters.Any(p => !string.IsNullOrEmpty(p.Description));
        
        var sb = new StringBuilder();
        sb.AppendLine("new HPDAIFunctionFactoryOptions");
        sb.AppendLine("                        {");
        sb.AppendLine($"                            Name = {nameCode},");
        sb.AppendLine($"                            Description = {descriptionCode}");
        
        // Add parameter descriptions if any exist
        if (hasParameterDescriptions)
        {
            sb.AppendLine(",");
            sb.AppendLine("                            ParameterDescriptions = new Dictionary<string, string>");
            sb.AppendLine("                            {");
            var descriptionsWithValues = function.Parameters.Where(p => !string.IsNullOrEmpty(p.Description)).ToList();
            for (int i = 0; i < descriptionsWithValues.Count; i++)
            {
                var param = descriptionsWithValues[i];
                var comma = i < descriptionsWithValues.Count - 1 ? "," : "";
                sb.AppendLine($"                                {{ \"{param.Name}\", \"{param.Description}\" }}{comma}");
            }
            sb.AppendLine("                            }");
        }
        
        sb.AppendLine("                        }");
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates context resolution methods for dynamic metadata.
    /// </summary>
    private static string GenerateContextResolutionMethods(PluginInfo plugin)
    {
        var sb = new StringBuilder();
        
        // Plugin-level description resolution
        if (plugin.HasContextAwareMetadata)
        {
            sb.AppendLine(DSLCodeGenerator.GenerateDescriptionResolver("Plugin", plugin.Description));
        }
        
        // Function-level description resolution
        foreach (var function in plugin.Functions.Where(f => f.HasContextAwareMetadata))
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine(DSLCodeGenerator.GenerateDescriptionResolver(function.Name, function.Description));
        }
        
        // Generate conditional evaluators
        foreach (var function in plugin.Functions.Where(f => f.IsConditional))
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine(DSLCodeGenerator.GenerateConditionalEvaluator(function.Name, function.ConditionalExpression!));
        }
        
        return sb.ToString();
    }
    
    // Helper methods for analysis...
    
    private static bool HasAIFunctionAttribute(MethodDeclarationSyntax method, SemanticModel semanticModel)
    {
        return method.AttributeLists
            .SelectMany(attrList => attrList.Attributes)
            .Any(attr => attr.Name.ToString().Contains("AIFunction"));
    }
    
    private static FunctionInfo? AnalyzeFunction(MethodDeclarationSyntax method, SemanticModel semanticModel)
    {
        if (!method.Modifiers.Any(SyntaxKind.PublicKeyword))
            return null;
        
        var symbol = semanticModel.GetDeclaredSymbol(method);
        if (symbol == null) return null;
        
        // Get function attributes
        var customName = GetCustomFunctionName(method);
        var description = GetFunctionDescription(method);
        var permissions = GetRequiredPermissions(method);
        var conditionalExpression = GetConditionalExpression(method);
        
        // Validate function description
        if (!string.IsNullOrEmpty(description))
        {
            // Temporarily bypass validation for debugging
            // var validationResult = DSLValidator.ValidateExpression(description);
            // if (!validationResult.IsValid)
            // {
            //     // Skip this function if description is invalid
            //     return null;
            // }
        }
        
        return new FunctionInfo
        {
            Name = method.Identifier.ValueText,
            CustomName = customName,
            Description = description,
            Parameters = AnalyzeParameters(method.ParameterList, semanticModel),
            ReturnType = GetReturnType(method, semanticModel),
            IsAsync = IsAsyncMethod(method),
            RequiredPermissions = permissions,
            ConditionalExpression = conditionalExpression
        };
    }
    
    private static List<ParameterInfo> AnalyzeParameters(ParameterListSyntax parameterList, SemanticModel semanticModel)
    {
        return parameterList.Parameters
            .Select(param => new ParameterInfo
            {
                Name = param.Identifier.ValueText,
                Type = GetParameterType(param, semanticModel),
                Description = GetParameterDescription(param),
                HasDefaultValue = param.Default != null,
                DefaultValue = GetDefaultValue(param)
            })
            .ToList();
    }
    
    // Additional helper methods would go here...
    // (GetCustomFunctionName, GetFunctionDescription, GetRequiredPermissions, etc.)
    
    private static string ExtractStringLiteral(ExpressionSyntax expression)
    {
        if (expression is LiteralExpressionSyntax literal && literal.Token.IsKind(SyntaxKind.StringLiteralToken))
        {
            return literal.Token.ValueText;
        }
        return "";
    }
    
    private static string GetNamespace(SyntaxNode node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            if (parent is NamespaceDeclarationSyntax namespaceDecl)
                return namespaceDecl.Name.ToString();
            if (parent is FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
                return fileScopedNamespace.Name.ToString();
            parent = parent.Parent;
        }
        return "";
    }
    
    private static string? GetCustomFunctionName(MethodDeclarationSyntax method)
    {
        // For Semantic Kernel style, function name is always the method name
        // No custom name override supported
        return null; // Use method name as default
    }
    
    private static string GetFunctionDescription(MethodDeclarationSyntax method)
    {
        // Check for [Description] attribute directly on the method (Semantic Kernel style)
        var descriptionAttributes = method.AttributeLists
            .SelectMany(attrList => attrList.Attributes)
            .Where(attr => attr.Name.ToString().Contains("Description"));
            
        foreach (var attr in descriptionAttributes)
        {
            var arguments = attr.ArgumentList?.Arguments;
            if (arguments.HasValue && arguments.Value.Count >= 1)
            {
                return ExtractStringLiteral(arguments.Value[0].Expression);
            }
        }
        
        return "";
    }

    private static List<string> GetRequiredPermissions(MethodDeclarationSyntax method)
    {
        // Implementation to extract required permissions
        return new List<string>(); // Placeholder
    }
    
    private static string? GetConditionalExpression(MethodDeclarationSyntax method)
    {
        var conditionalAttributes = method.AttributeLists
            .SelectMany(attrList => attrList.Attributes)
            .Where(attr => attr.Name.ToString().Contains("ConditionalFunction"));
            
        foreach (var attr in conditionalAttributes)
        {
            var arguments = attr.ArgumentList?.Arguments;
            if (arguments.HasValue && arguments.Value.Count >= 1)
            {
                return ExtractStringLiteral(arguments.Value[0].Expression);
            }
        }
        
        return null;
    }
    
    private static string GetParameterType(ParameterSyntax param, SemanticModel semanticModel)
    {
        return param.Type?.ToString() ?? "object";
    }
    
    private static string GetParameterDescription(ParameterSyntax param)
    {
        // Check for [Description("...")] attribute on the parameter
        var descriptionAttributes = param.AttributeLists
            .SelectMany(attrList => attrList.Attributes)
            .Where(attr => attr.Name.ToString().Contains("Description"));
            
        foreach (var attr in descriptionAttributes)
        {
            var arguments = attr.ArgumentList?.Arguments;
            if (arguments.HasValue && arguments.Value.Count >= 1)
            {
                return ExtractStringLiteral(arguments.Value[0].Expression);
            }
        }
        
        return "";
    }

    private static string? GetDefaultValue(ParameterSyntax param)
    {
        return param.Default?.Value?.ToString();
    }
    
    private static string GetReturnType(MethodDeclarationSyntax method, SemanticModel semanticModel)
    {
        return method.ReturnType.ToString();
    }
    
    private static bool IsAsyncMethod(MethodDeclarationSyntax method)
    {
        return method.Modifiers.Any(SyntaxKind.AsyncKeyword) ||
               method.ReturnType.ToString().StartsWith("Task");
    }
}
