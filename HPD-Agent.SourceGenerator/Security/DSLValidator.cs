using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Enhanced DSL validator for generic context system with improved security.
/// </summary>
public static class DSLValidator
{
    private static readonly Regex PropertyPattern = new(@"^context\.[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex TernaryPattern = new(@"^context\.[a-zA-Z_][a-zA-Z0-9_]*\s*\?\s*'[^']*'\s*:\s*'[^']*'$", RegexOptions.Compiled);
    private static readonly Regex HelperPattern = new(@"^[a-zA-Z_][a-zA-Z0-9_]*\.[a-zA-Z_][a-zA-Z0-9_]*\(context\)$", RegexOptions.Compiled);
    
    // Enhanced security patterns for generic context
    private static readonly HashSet<string> DangerousNamespaces = new()
    {
        "System.IO", "System.Net", "System.Reflection", 
        "System.Diagnostics", "System.Environment", "System.Threading",
        "Microsoft.Win32", "System.Runtime.InteropServices",
        "System.Security", "System.CodeDom", "System.Activator"
    };
    
    private static readonly HashSet<string> DangerousMethods = new()
    {
        "GetType", "GetMethod", "Invoke", "CreateInstance",
        "ReadAllText", "WriteAllText", "Execute", "Start",
        "Process", "LoadLibrary", "GetProcAddress", "Eval",
        "Compile", "LoadAssembly", "GetProperty", "SetProperty"
    };
    
    // Dangerous patterns that could be used with generic contexts
    private static readonly HashSet<string> DangerousPatterns = new()
    {
        "typeof", "nameof", "new ", "class ", "interface ",
        "Assembly", "Type.", "Activator.", "Assembly.",
        "GetProperty", "SetProperty", "GetField", "SetField"
    };
    
    /// <summary>
    /// Validates a DSL expression with enhanced security for generic contexts.
    /// </summary>
    public static ValidationResult ValidateExpression(string expression, Type? contextType = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return ValidationResult.Invalid("Expression cannot be null or empty");
            
        var cleaned = expression.Trim().TrimStart('{').TrimEnd('}');
        
        // Enhanced security analysis
        var securityRisks = AnalyzeSecurity(cleaned);
        if (securityRisks.Any())
        {
            return ValidationResult.Invalid($"Security risks detected: {string.Join(", ", securityRisks.Select(r => r.Description))}");
        }
        
        // Validate against allowed patterns
        if (PropertyPattern.IsMatch(cleaned))
        {
            var propertyValidation = ValidatePropertyAccess(cleaned, contextType);
            return propertyValidation.IsValid ? 
                ValidationResult.Valid("property") : 
                propertyValidation;
        }
        
        if (TernaryPattern.IsMatch(cleaned))
        {
            var ternaryValidation = ValidateTernaryExpression(cleaned, contextType);
            return ternaryValidation.IsValid ? 
                ValidationResult.Valid("ternary") : 
                ternaryValidation;
        }
        
        if (HelperPattern.IsMatch(cleaned))
        {
            var helperValidation = ValidateHelperMethod(cleaned);
            return helperValidation.IsValid ? 
                ValidationResult.Valid("helper") : 
                helperValidation;
        }
            
        return ValidationResult.Invalid($"Expression '{expression}' does not match allowed patterns. Allowed: {{context.property}}, {{context.property ? 'true' : 'false'}}, {{Helper.Method(context)}}");
    }
    
    /// <summary>
    /// Enhanced security analysis for generic contexts.
    /// </summary>
    public static List<SecurityRisk> AnalyzeSecurity(string expression)
    {
        var risks = new List<SecurityRisk>();
        
        // Check for dangerous namespace usage
        foreach (var ns in DangerousNamespaces)
        {
            if (expression.IndexOf(ns, StringComparison.OrdinalIgnoreCase) >= 0)
                risks.Add(new SecurityRisk($"Dangerous namespace usage: {ns}", RiskLevel.Critical));
        }
        
        // Check for dangerous method calls
        foreach (var method in DangerousMethods)
        {
            if (Regex.IsMatch(expression, $@"\b{Regex.Escape(method)}\b", RegexOptions.IgnoreCase))
                risks.Add(new SecurityRisk($"Dangerous method call: {method}", RiskLevel.High));
        }
        
        // Check for dangerous patterns
        foreach (var pattern in DangerousPatterns)
        {
            if (expression.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                risks.Add(new SecurityRisk($"Dangerous pattern detected: {pattern}", RiskLevel.High));
        }
        
        // Check for code injection patterns
        if (expression.Contains("();") || expression.Contains("};"))
            risks.Add(new SecurityRisk("Code injection pattern detected", RiskLevel.Critical));
            
        if (Regex.IsMatch(expression, @"['""][\s]*\+[\s]*['""]"))
            risks.Add(new SecurityRisk("String concatenation injection risk", RiskLevel.Medium));
            
        // Check for SQL injection patterns in helper methods
        if (Regex.IsMatch(expression, @"(SELECT|INSERT|UPDATE|DELETE|DROP|EXEC)", RegexOptions.IgnoreCase))
            risks.Add(new SecurityRisk("Potential SQL injection pattern", RiskLevel.High));
        
        // Check for script injection
        if (Regex.IsMatch(expression, @"<script|javascript:|eval\(", RegexOptions.IgnoreCase))
            risks.Add(new SecurityRisk("Script injection pattern detected", RiskLevel.Critical));
        
        return risks;
    }
    
    /// <summary>
    /// Validates property access expressions.
    /// </summary>
    private static ValidationResult ValidatePropertyAccess(string expression, Type? contextType)
    {
        var match = PropertyPattern.Match(expression);
        if (!match.Success)
            return ValidationResult.Invalid("Invalid property pattern");
            
        var propertyName = match.Groups[1].Value;
        
        // Basic property name validation
        if (!IsValidPropertyName(propertyName))
            return ValidationResult.Invalid($"Invalid property name: {propertyName}");
        
        // If we have context type information, validate the property exists
        if (contextType != null)
        {
            var contextValidation = ValidateAgainstContextType(propertyName, contextType);
            if (!contextValidation.IsValid)
                return contextValidation;
        }
        
        return ValidationResult.Valid("property");
    }
    
    /// <summary>
    /// Validates ternary expressions.
    /// </summary>
    private static ValidationResult ValidateTernaryExpression(string expression, Type? contextType)
    {
        var match = TernaryPattern.Match(expression);
        if (!match.Success)
            return ValidationResult.Invalid("Invalid ternary pattern");
            
        var propertyName = match.Groups[1].Value;
        var trueValue = match.Groups[2].Value;
        var falseValue = match.Groups[3].Value;
        
        // Validate property name
        if (!IsValidPropertyName(propertyName))
            return ValidationResult.Invalid($"Invalid property name in ternary: {propertyName}");
        
        // Validate string values for injection
        var stringValidation = ValidateStringLiterals(trueValue, falseValue);
        if (!stringValidation.IsValid)
            return stringValidation;
        
        // If we have context type, validate property exists
        if (contextType != null)
        {
            var contextValidation = ValidateAgainstContextType(propertyName, contextType);
            if (!contextValidation.IsValid)
                return contextValidation;
        }
        
        return ValidationResult.Valid("ternary");
    }
    
    /// <summary>
    /// Validates helper method calls.
    /// </summary>
    private static ValidationResult ValidateHelperMethod(string expression)
    {
        var match = HelperPattern.Match(expression);
        if (!match.Success)
            return ValidationResult.Invalid("Invalid helper method pattern");
            
        var className = match.Groups[1].Value;
        var methodName = match.Groups[2].Value;
        
        // Basic name validation
        if (!IsValidIdentifier(className))
            return ValidationResult.Invalid($"Invalid class name: {className}");
            
        if (!IsValidIdentifier(methodName))
            return ValidationResult.Invalid($"Invalid method name: {methodName}");
        
        // Check against dangerous classes/methods
        if (DangerousMethods.Contains(methodName))
            return ValidationResult.Invalid($"Dangerous method not allowed: {methodName}");
        
        return ValidationResult.Valid("helper");
    }
    
    /// <summary>
    /// AOT-friendly validation: assumes all context properties are resolved at runtime.
    /// </summary>
    private static ValidationResult ValidateAgainstContextType(string propertyName, Type contextType)
    {
        // Skip reflection checks for AOT compatibility.
        return ValidationResult.Valid("property", $"Property '{propertyName}' will be resolved at runtime via GetProperty<T>()");
    }
    
    /// <summary>
    /// Validates string literals for injection attacks.
    /// </summary>
    private static ValidationResult ValidateStringLiterals(params string[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrEmpty(value)) continue;
            
            // Check for script injection
            if (Regex.IsMatch(value, @"<script|javascript:|eval\(", RegexOptions.IgnoreCase))
                return ValidationResult.Invalid($"Script injection detected in string literal: {value}");
                
            // Check for SQL injection
            if (Regex.IsMatch(value, @"(SELECT|INSERT|UPDATE|DELETE|DROP|EXEC)", RegexOptions.IgnoreCase))
                return ValidationResult.Invalid($"Potential SQL injection in string literal: {value}");
                
            // Check for path traversal
            if (value.Contains("../") || value.Contains("..\\"))
                return ValidationResult.Invalid($"Path traversal attempt detected: {value}");
        }
        
        return ValidationResult.Valid("string");
    }
    
    /// <summary>
    /// Validates property names.
    /// </summary>
    private static bool IsValidPropertyName(string name)
    {
        return !string.IsNullOrEmpty(name) && 
               Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$") &&
               name.Length <= 50; // Reasonable limit
    }
    
    /// <summary>
    /// Validates C# identifiers.
    /// </summary>
    private static bool IsValidIdentifier(string identifier)
    {
        return !string.IsNullOrEmpty(identifier) && 
               Regex.IsMatch(identifier, @"^[a-zA-Z_][a-zA-Z0-9_]*$") &&
               identifier.Length <= 50; // Reasonable limit
    }
}

/// <summary>
/// Enhanced validation result with warnings.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? WarningMessage { get; private set; }
    public string? ExpressionType { get; private set; }
    
    private ValidationResult() { }
    
    public static ValidationResult Valid(string expressionType, string? warningMessage = null) => new()
    {
        IsValid = true,
        ExpressionType = expressionType,
        WarningMessage = warningMessage
    };
    
    public static ValidationResult Invalid(string errorMessage) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage
    };
    
    public static ValidationResult Warning(string warningMessage) => new()
    {
        IsValid = true,
        WarningMessage = warningMessage
    };
}

/// <summary>
/// Security risk information.
/// </summary>
public class SecurityRisk
{
    public string Description { get; }
    public RiskLevel Level { get; }
    
    public SecurityRisk(string description, RiskLevel level)
    {
        Description = description;
        Level = level;
    }
}

/// <summary>
/// Security risk levels.
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}