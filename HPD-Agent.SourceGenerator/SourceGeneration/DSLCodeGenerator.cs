// Updated DSL Code Generator - now works with any context type
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Linq.Expressions;
internal static class DSLCodeGenerator
{
    private static readonly Regex PropertyExtractor = new(@"context\.([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex TernaryExtractor = new(@"context\.([a-zA-Z_][a-zA-Z0-9_]*)\s*\?\s*['""]([^'""]*)['""]??\s*:\s*['""]([^'""]*)['""]", RegexOptions.Compiled);
    private static readonly Regex HelperExtractor = new(@"([a-zA-Z_][a-zA-Z0-9_]*)\.([a-zA-Z_][a-zA-Z0-9_]*)\(context\)", RegexOptions.Compiled);
    
    /// <summary>
    /// Generates code to resolve a description template with context expressions.
    /// Now works with any IPluginMetadataContext implementation.
    /// </summary>
    public static string GenerateDescriptionResolver(string name, string template, Type? contextType = null)
    {
        var expressions = ExtractDSLExpressions(template);
        var replacements = new List<string>();
        
        foreach (var expression in expressions)
        {
            var cleaned = expression.Trim().TrimStart('{').TrimEnd('}');
            if (PropertyExtractor.IsMatch(cleaned.ToString()))
            {
                replacements.Add(GeneratePropertyReplacement(expression.ToString(), cleaned.ToString(), contextType));
            }
            else if (TernaryExtractor.IsMatch(cleaned.ToString()))
            {
                replacements.Add(GenerateTernaryReplacement(expression.ToString(), cleaned.ToString(), contextType));
            }
            else if (HelperExtractor.IsMatch(cleaned.ToString()))
            {
                replacements.Add(GenerateHelperMethodReplacement(expression.ToString(), cleaned.ToString()));
            }
        }
        return $$"""
        private static string Resolve{{name}}Description(IPluginMetadataContext? context)
        {
            var template = @"{{template}}";
            {{string.Join("\n            ", replacements)}}
            return template;
        }
        """;
    }

    // Add missing ExtractDSLExpressions implementation
    private static List<string> ExtractDSLExpressions(string template)
    {
        // Simple implementation: find all { ... } blocks in the template
        var expressions = new List<string>();
        int start = 0;
        while ((start = template.IndexOf('{', start)) != -1)
        {
            int end = template.IndexOf('}', start + 1);
            if (end == -1) break;
            expressions.Add(template.Substring(start, end - start + 1));
            start = end + 1;
        }
        return expressions;
    }
    
    private static string GeneratePropertyReplacement(string expression, string cleaned, Type? contextType)
    {
        var match = PropertyExtractor.Match(cleaned);
        if (!match.Success) return "";
        
        var propertyName = match.Groups[1].Value;
        var defaultValue = GetGenericDefaultValue(propertyName);
        
        return $"""template = template.Replace("{expression}", context?.GetProperty<object>("{propertyName}")?.ToString() ?? "{defaultValue}");""";
    }
    
    private static string GenerateTernaryReplacement(string expression, string cleaned, Type? contextType)
    {
        var match = TernaryExtractor.Match(cleaned);
        if (!match.Success) return "";
        
        var propertyName = match.Groups[1].Value;
        var trueValue = match.Groups[2].Value;
        var falseValue = match.Groups[3].Value;
        
        return $"""template = template.Replace("{expression}", context?.GetProperty<bool>("{propertyName}") == true ? "{trueValue}" : "{falseValue}");""";
    }
    
    private static string GenerateHelperMethodReplacement(string expression, string cleaned)
    {
        var match = HelperExtractor.Match(cleaned);
        if (!match.Success) return "";
        
        var className = match.Groups[1].Value;
        var methodName = match.Groups[2].Value;
        
        return $"""template = template.Replace("{expression}", {className}.{methodName}(context ?? new DefaultExecutionContext()));""";
    }
    
    /// <summary>
    /// Gets a generic default value for unknown properties
    /// </summary>
    private static string GetGenericDefaultValue(string propertyName)
    {
        // Use naming conventions to guess appropriate defaults
        return propertyName.ToLowerInvariant() switch
        {
            var name when name.Contains("id") => "default-id",
            var name when name.Contains("name") => "default-name",
            var name when name.Contains("type") => "standard",
            var name when name.Contains("tier") || name.Contains("level") => "basic",
            var name when name.StartsWith("is") || name.StartsWith("has") => "false",
            var name when name.Contains("environment") => "production",
            var name when name.Contains("region") => "global",
            var name when name.Contains("language") => "en",
            _ => "unknown"
        };
    }
    
    /// <summary>
    /// Generates a conditional evaluation method for a function from a type-safe expression tree (V2).
    /// This is far simpler and more robust than V1's string parsing.
    /// </summary>
    public static string GenerateConditionalEvaluator(string functionName, Expression conditionExpression, string contextTypeName)
    {
        // Cast to LambdaExpression to access Body and Parameters
        if (conditionExpression is not LambdaExpression lambdaExpression)
        {
            throw new ArgumentException("Condition expression must be a lambda expression", nameof(conditionExpression));
        }

        // The Body of the expression is converted directly to a C# string.
        // This is far simpler and more robust than V1's string parsing.
        var conditionCode = lambdaExpression.Body.ToString();

        // The parameter name in the lambda (e.g., "ctx") needs to be replaced
        // with the variable name used in the generated method ("context").
        var parameterName = lambdaExpression.Parameters[0].Name;
        var finalConditionCode = conditionCode.Replace(parameterName + ".", "context.");

        return $$"""
        /// <summary>
        /// Evaluates the conditional expression for {{functionName}} using a pre-compiled expression.
        /// </summary>
        private static bool Evaluate{{functionName}}Condition({{contextTypeName}}? context)
        {
            // If no context is provided, default to including the function (no filtering).
            if (context == null) return true;
            
            try
            {
                return {{finalConditionCode}};
            }
            catch
            {
                // In case of any unexpected error during evaluation, default to false.
                return false; 
            }
        }
        """;
    }
    
    /// <summary>
    /// Generates a conditional evaluation method for a function from a type-safe property expression (V2).
    /// </summary>
    public static string GenerateConditionalEvaluatorV2(string functionName, string propertyExpression, string contextTypeName)
    {
        // Convert property-based expression to context property access
        // For example: "HasTavilyProvider" becomes "context.HasTavilyProvider"
        // For complex expressions: "HasBraveProvider && HasBingProvider" becomes "context.HasBraveProvider && context.HasBingProvider"
        var conditionCode = ConvertPropertyExpressionToCode(propertyExpression);

        return $$"""
        /// <summary>
        /// Evaluates the conditional expression for {{functionName}} using V2 property-based logic.
        /// </summary>
        private static bool Evaluate{{functionName}}Condition(IPluginMetadataContext? context)
        {
            // If no context is provided, default to including the function (no filtering).
            if (context == null) return true;
            
            // Safely cast to the expected context type
            if (context is not {{contextTypeName}} typedContext) return false;
            
            try
            {
                return {{conditionCode.Replace("context.", "typedContext.")}};
            }
            catch
            {
                // In case of any unexpected error during evaluation, default to false.
                return false; 
            }
        }
        """;
    }

    /// <summary>
    /// Converts a property expression to context-based C# code using a robust regex approach.
    /// This method correctly handles whitespace and complex expressions.
    /// </summary>
    private static string ConvertPropertyExpressionToCode(string propertyExpression)
    {
        // This regex reliably finds all potential C# identifiers.
        var identifierRegex = new System.Text.RegularExpressions.Regex(@"\b[A-Za-z_][A-Za-z0-9_]*\b");

        // We must filter out language keywords and literals to isolate property names.
        var keywords = new HashSet<string> { "true", "false", "null" };

        var result = identifierRegex.Replace(propertyExpression, match =>
        {
            var identifier = match.Value;

            // If the found identifier is a property (i.e., not a keyword),
            // then prepend it with "context." to form a valid member access expression.
            if (!keywords.Contains(identifier))
            {
                return $"context.{identifier}";
            }

            // Otherwise, it's a keyword like "true" or "false", so leave it unchanged.
            return identifier;
        });

        return result;
    }

    /// <summary>
    /// Generates a V2 description resolver that uses direct property access on a typed context.
    /// </summary>
    public static string GenerateDescriptionResolverV2(string name, string template, string contextTypeName)
    {
        var expressions = ExtractDSLExpressions(template);
        var replacements = new List<string>();

        foreach (var expression in expressions)
        {
            var cleaned = expression.Trim().TrimStart('{').TrimEnd('}');
            var match = PropertyExtractor.Match(cleaned);
            if (match.Success)
            {
                var propertyName = match.Groups[1].Value;
                // Generate replacement code using direct property access on the typed context.
                replacements.Add($"template = template.Replace(\"{expression}\", typedContext.{propertyName}?.ToString() ?? \"\");");
            }
        }
        
        return $$"""
        private static string Resolve{{name}}Description(IPluginMetadataContext? context)
        {
            var template = @"{{template}}";

            // If the context is null or not the expected type, return the unresolved template.
            if (context is not {{contextTypeName}} typedContext)
            {
                return template; 
            }

            {{string.Join("\n            ", replacements)}}
            return template;
        }
        """;
    }
}