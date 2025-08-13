// Updated DSL Code Generator - now works with any context type
using System.Text.RegularExpressions;
using System.Linq;
internal static class DSLCodeGenerator
{
    private static readonly Regex PropertyExtractor = new(@"context\.([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex TernaryExtractor = new(@"context\.([a-zA-Z_][a-zA-Z0-9_]*)\s*\?\s*'([^']*)'\s*:\s*'([^']*)'", RegexOptions.Compiled);
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
    /// Generates a conditional evaluation method for a function
    /// </summary>
    public static string GenerateConditionalEvaluator(string functionName, string condition)
    {
        var evaluatorCode = GenerateConditionEvaluationCode(condition);
        
        return $$"""
        /// <summary>
        /// Evaluates the conditional expression for {{functionName}}
        /// </summary>
        private static bool Evaluate{{functionName}}Condition(IPluginMetadataContext? context)
        {
            // If no context provided, include function by default (treat as unconditional)
            if (context == null) return true;
            
            try
            {
                {{evaluatorCode}}
            }
            catch (Exception)
            {
                // Log error and default to false for security
                return false;
            }
        }
        """;
    }
    
    private static string GenerateConditionEvaluationCode(string condition)
    {
        // Handle different expression patterns
        if (IsBooleanProperty(condition))
        {
            return GenerateBooleanPropertyCheck(condition);
        }
        
        if (IsComparisonExpression(condition))
        {
            return GenerateComparisonCheck(condition);
        }
        
        if (IsLogicalExpression(condition))
        {
            return GenerateLogicalExpressionCheck(condition);
        }
        
        // Fallback for complex expressions
        return GenerateComplexExpressionCheck(condition);
    }
    
    private static bool IsBooleanProperty(string condition)
    {
        // Matches: context.propertyName or !context.propertyName
        return Regex.IsMatch(condition, @"^!?context\.[a-zA-Z_][a-zA-Z0-9_]*$");
    }
    
    private static string GenerateBooleanPropertyCheck(string condition)
    {
        var isNegated = condition.StartsWith("!");
        var propertyName = isNegated ? condition.Substring(9) : condition.Substring(8); // Remove "context." or "!context."
        
        if (isNegated)
        {
            return "return context.GetProperty<bool>(\"" + propertyName + "\") != true;";
        }
        else
        {
            return "return context.GetProperty<bool>(\"" + propertyName + "\") == true;";
        }
    }
    
    private static bool IsComparisonExpression(string condition)
    {
        // Matches: context.property == 'value' or context.property > 100
        return Regex.IsMatch(condition, @"context\.[a-zA-Z_][a-zA-Z0-9_]*\s*(==|!=|>|<|>=|<=)\s*('.*'|\d+)");
    }
    
    private static string GenerateComparisonCheck(string condition)
    {
        var match = Regex.Match(condition, @"context\.([a-zA-Z_][a-zA-Z0-9_]*)\s*(==|!=|>|<|>=|<=)\s*('(.*)' |(\d+))");
        
        var propertyName = match.Groups[1].Value;
        var operatorSymbol = match.Groups[2].Value;
        var isStringValue = match.Groups[4].Success;
        var value = isStringValue ? match.Groups[4].Value : match.Groups[5].Value;
        
        if (isStringValue)
        {
            return operatorSymbol switch
            {
                "==" => "return context.GetProperty<string>(\"" + propertyName + "\") == \"" + value + "\";",
                "!=" => "return context.GetProperty<string>(\"" + propertyName + "\") != \"" + value + "\";",
                _ => "return false; // Unsupported string comparison operator"
            };
        }
        else
        {
            return operatorSymbol switch
            {
                "==" => "return context.GetProperty<int>(\"" + propertyName + "\") == " + value + ";",
                "!=" => "return context.GetProperty<int>(\"" + propertyName + "\") != " + value + ";",
                ">" => "return context.GetProperty<int>(\"" + propertyName + "\") > " + value + ";",
                "<" => "return context.GetProperty<int>(\"" + propertyName + "\") < " + value + ";",
                ">=" => "return context.GetProperty<int>(\"" + propertyName + "\") >= " + value + ";",
                "<=" => "return context.GetProperty<int>(\"" + propertyName + "\") <= " + value + ";",
                _ => "return false; // Unsupported numeric comparison operator"
            };
        }
    }
    
    private static bool IsLogicalExpression(string condition)
    {
        // Matches expressions with && or ||
        return condition.Contains("&&") || condition.Contains("||");
    }
    
    private static string GenerateLogicalExpressionCheck(string condition)
    {
        // Simple implementation for now - handle && and || at top level
        if (condition.Contains("&&"))
        {
            var parts = condition.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries);
            var checks = parts.Select(part => GenerateSimpleConditionCheck(part.Trim())).ToArray();
            return "return " + string.Join(" && ", checks) + ";";
        }
        else if (condition.Contains("||"))
        {
            var parts = condition.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
            var checks = parts.Select(part => GenerateSimpleConditionCheck(part.Trim())).ToArray();
            return "return " + string.Join(" || ", checks) + ";";
        }
        
        return "return false; // Unsupported logical expression";
    }
    
    private static string GenerateSimpleConditionCheck(string condition)
    {
        if (IsBooleanProperty(condition))
        {
            var isNegated = condition.StartsWith("!");
            var propertyName = isNegated ? condition.Substring(9) : condition.Substring(8);
            
            if (isNegated)
            {
                return "(context.GetProperty<bool>(\"" + propertyName + "\") != true)";
            }
            else
            {
                return "(context.GetProperty<bool>(\"" + propertyName + "\") == true)";
            }
        }
        
        if (IsComparisonExpression(condition))
        {
            var match = Regex.Match(condition, @"context\.([a-zA-Z_][a-zA-Z0-9_]*)\s*(==|!=|>|<|>=|<=)\s*('(.*)' |(\d+))");
            var propertyName = match.Groups[1].Value;
            var operatorSymbol = match.Groups[2].Value;
            var isStringValue = match.Groups[4].Success;
            var value = isStringValue ? match.Groups[4].Value : match.Groups[5].Value;
            
            if (isStringValue)
            {
                return operatorSymbol switch
                {
                    "==" => "(context.GetProperty<string>(\"" + propertyName + "\") == \"" + value + "\")",
                    "!=" => "(context.GetProperty<string>(\"" + propertyName + "\") != \"" + value + "\")",
                    _ => "false"
                };
            }
            else
            {
                return operatorSymbol switch
                {
                    "==" => "(context.GetProperty<int>(\"" + propertyName + "\") == " + value + ")",
                    "!=" => "(context.GetProperty<int>(\"" + propertyName + "\") != " + value + ")",
                    ">" => "(context.GetProperty<int>(\"" + propertyName + "\") > " + value + ")",
                    "<" => "(context.GetProperty<int>(\"" + propertyName + "\") < " + value + ")",
                    ">=" => "(context.GetProperty<int>(\"" + propertyName + "\") >= " + value + ")",
                    "<=" => "(context.GetProperty<int>(\"" + propertyName + "\") <= " + value + ")",
                    _ => "false"
                };
            }
        }
        
        return "false";
    }
    
    private static string GenerateComplexExpressionCheck(string condition)
    {
        // For now, just return false for unsupported expressions
        return "return false; // Complex expression not yet supported: " + condition;
    }
}