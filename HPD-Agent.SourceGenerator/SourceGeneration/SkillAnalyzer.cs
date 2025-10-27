using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Analyzes skill methods and extracts skill information
/// </summary>
internal static class SkillAnalyzer
{
    /// <summary>
    /// Checks if a class contains skill methods
    /// A skill method is: public static Skill MethodName(SkillOptions? options = null)
    /// </summary>
    public static bool HasSkillMethods(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
    {
        return classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(method => IsSkillMethod(method, semanticModel));
    }

    /// <summary>
    /// Checks if a method is a skill method
    /// Must be: public static Skill MethodName(SkillOptions? options = null)
    /// </summary>
    public static bool IsSkillMethod(MethodDeclarationSyntax method, SemanticModel semanticModel)
    {
        // Must be public
        if (!method.Modifiers.Any(SyntaxKind.PublicKeyword))
            return false;

        // Must be static
        if (!method.Modifiers.Any(SyntaxKind.StaticKeyword))
            return false;

        // Must return Skill type
        var returnTypeSymbol = semanticModel.GetTypeInfo(method.ReturnType).Type;
        if (returnTypeSymbol?.Name != "Skill")
            return false;

        // Should NOT have attributes (distinguish from [AIFunction] methods)
        // Skills are detected by return type, not attributes
        if (method.AttributeLists.Any())
            return false;

        return true;
    }

    /// <summary>
    /// Analyzes a skill method and extracts skill information
    /// </summary>
    public static SkillInfo? AnalyzeSkill(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        GeneratorSyntaxContext context)
    {
        // Find SkillFactory.Create() invocation in method body
        var invocation = method.Body?.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => IsSkillFactoryCreate(inv, semanticModel));

        if (invocation == null)
        {
            // Check arrow expression body: => SkillFactory.Create(...)
            invocation = method.ExpressionBody?.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(inv => IsSkillFactoryCreate(inv, semanticModel));
        }

        if (invocation == null)
        {
            // Missing SkillFactory.Create() call - diagnostics will be reported by main generator
            // For now, just return null
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;

        // Minimum 3 arguments: name, description, instructions
        if (arguments.Count < 3)
        {
            return null; // TODO: Report diagnostic in Phase 2.5
        }

        // Extract arguments
        var name = ExtractStringLiteral(arguments[0].Expression, semanticModel);
        var description = ExtractStringLiteral(arguments[1].Expression, semanticModel);
        var instructions = ExtractStringLiteral(arguments[2].Expression, semanticModel);

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
        {
            return null; // TODO: Report diagnostic in Phase 2.5
        }

        var methodName = method.Identifier.ValueText;

        // Find SkillOptions argument (optional, can be at position 3 or named)
        SkillOptionsInfo? options = null;
        int referencesStartIndex = 3;

        // Check if position 3 is SkillOptions or a reference
        if (arguments.Count > 3)
        {
            var thirdArg = arguments[3];

            // Check if named parameter "options"
            if (thirdArg.NameColon?.Name.Identifier.ValueText == "options")
            {
                options = ExtractSkillOptions(thirdArg.Expression, semanticModel);
                referencesStartIndex = 4;
            }
            else
            {
                // Check type to determine if it's SkillOptions or a reference
                var thirdArgType = semanticModel.GetTypeInfo(thirdArg.Expression).Type;
                if (thirdArgType?.Name == "SkillOptions")
                {
                    options = ExtractSkillOptions(thirdArg.Expression, semanticModel);
                    referencesStartIndex = 4;
                }
            }
        }

        // Extract function/skill references (remaining arguments)
        var references = new List<ReferenceInfo>();
        for (int i = referencesStartIndex; i < arguments.Count; i++)
        {
            var argExpr = arguments[i].Expression;

            // Skip if this is a named "options" parameter
            if (arguments[i].NameColon?.Name.Identifier.ValueText == "options")
                continue;

            var reference = AnalyzeReference(argExpr, semanticModel);
            if (reference != null)
                references.Add(reference);
        }

        // TODO: Warn if no references in Phase 2.5

        var containingClass = method.Parent as ClassDeclarationSyntax;
        var namespaceName = GetNamespace(containingClass);

        return new SkillInfo
        {
            MethodName = methodName,
            Name = name,
            Description = description,
            Instructions = instructions,
            Options = options ?? new SkillOptionsInfo(),
            References = references,
            ContainingClass = containingClass!,
            Namespace = namespaceName
        };
    }

    /// <summary>
    /// Checks if an invocation is SkillFactory.Create()
    /// </summary>
    private static bool IsSkillFactoryCreate(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        var symbol = symbolInfo.Symbol as IMethodSymbol;

        return symbol?.ContainingType?.Name == "SkillFactory" &&
               symbol?.Name == "Create";
    }

    /// <summary>
    /// Analyzes a reference (function or skill)
    /// </summary>
    private static ReferenceInfo? AnalyzeReference(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(expression);
        var symbol = symbolInfo.Symbol;

        if (symbol is not IMethodSymbol methodSymbol)
        {
            return null; // TODO: Report diagnostic in Phase 2.5
        }

        var containingType = methodSymbol.ContainingType;
        var returnType = methodSymbol.ReturnType;

        // Check if this is a skill reference (returns Skill type)
        if (returnType.Name == "Skill")
        {
            return new ReferenceInfo
            {
                ReferenceType = ReferenceType.Skill,
                PluginType = containingType.Name,
                PluginNamespace = containingType.ContainingNamespace.ToDisplayString(),
                MethodName = methodSymbol.Name,
                FullName = $"{containingType.Name}.{methodSymbol.Name}",
                Location = expression.GetLocation()
            };
        }

        // Check if this is a function reference (has [AIFunction] attribute)
        var hasAIFunction = methodSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name.Contains("AIFunction") == true);

        if (!hasAIFunction)
        {
            return null; // TODO: Report diagnostic in Phase 2.5
        }

        return new ReferenceInfo
        {
            ReferenceType = ReferenceType.Function,
            PluginType = containingType.Name,
            PluginNamespace = containingType.ContainingNamespace.ToDisplayString(),
            MethodName = methodSymbol.Name,
            FullName = $"{containingType.Name}.{methodSymbol.Name}",
            Location = expression.GetLocation()
        };
    }

    /// <summary>
    /// Extracts string literal from expression
    /// </summary>
    private static string? ExtractStringLiteral(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        // Handle string literals
        if (expression is LiteralExpressionSyntax literal &&
            literal.Token.IsKind(SyntaxKind.StringLiteralToken))
        {
            return literal.Token.ValueText;
        }

        // Handle verbatim string literals (@"...")
        if (expression is LiteralExpressionSyntax verbatim &&
            verbatim.Token.IsKind(SyntaxKind.StringLiteralToken))
        {
            return verbatim.Token.ValueText;
        }

        // Try to evaluate constant
        var constantValue = semanticModel.GetConstantValue(expression);
        if (constantValue.HasValue && constantValue.Value is string str)
        {
            return str;
        }

        return null;
    }

    /// <summary>
    /// Extracts SkillOptions from object creation expression
    /// </summary>
    private static SkillOptionsInfo ExtractSkillOptions(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        var options = new SkillOptionsInfo();

        if (expression is not ObjectCreationExpressionSyntax objectCreation)
            return options;

        if (objectCreation.Initializer == null)
            return options;

        foreach (var assignment in objectCreation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
        {
            var propertyName = (assignment.Left as IdentifierNameSyntax)?.Identifier.ValueText;
            var value = assignment.Right;

            switch (propertyName)
            {
                case "ScopingMode":
                    var scopingValue = (value as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText;
                    if (scopingValue != null)
                        options.ScopingMode = scopingValue;
                    break;

                case "AutoExpand":
                    var autoExpandValue = semanticModel.GetConstantValue(value);
                    if (autoExpandValue.HasValue && autoExpandValue.Value is bool autoExpand)
                        options.AutoExpand = autoExpand;
                    break;

                case "InstructionDocuments":
                    // Extract array of strings
                    if (value is ArrayCreationExpressionSyntax arrayCreation &&
                        arrayCreation.Initializer != null)
                    {
                        options.InstructionDocuments = arrayCreation.Initializer.Expressions
                            .Select(expr => ExtractStringLiteral(expr, semanticModel))
                            .Where(s => s != null)
                            .ToList()!;
                    }
                    break;

                case "InstructionDocumentBaseDirectory":
                    var baseDir = ExtractStringLiteral(value, semanticModel);
                    if (baseDir != null)
                        options.InstructionDocumentBaseDirectory = baseDir;
                    break;
            }
        }

        return options;
    }

    /// <summary>
    /// Gets namespace of a class
    /// </summary>
    private static string GetNamespace(ClassDeclarationSyntax? classDecl)
    {
        if (classDecl == null) return string.Empty;

        var namespaceDecl = classDecl.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDecl != null)
            return namespaceDecl.Name.ToString();

        var fileScopedNamespace = classDecl.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        if (fileScopedNamespace != null)
            return fileScopedNamespace.Name.ToString();

        return string.Empty;
    }

    // TODO: Phase 2.5 - Add diagnostic reporting support
    // For now, diagnostics are skipped to get basic functionality working
}
