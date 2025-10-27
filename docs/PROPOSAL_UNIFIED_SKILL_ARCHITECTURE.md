# HPD-Agent Unified Skill Architecture Proposal

**Date:** 2025-10-26
**Status:** Draft
**Author:** Architecture Discussion

---

## Executive Summary

This proposal outlines a fundamental architectural change to HPD-Agent that unifies plugins and skills into a single, type-safe, first-class citizen system. The new architecture enables:

- **Type-safe skill definitions** using compile-time method references instead of runtime strings
- **Skills as first-class citizens** with `Skill` type and `SkillFactory.Create()` pattern
- **Skills within plugin classes** - plugins can contain both `[AIFunction]` methods and `Skill` methods
- **Skill classes as plugin scopes** - skill classes can have `[PluginScope]` attribute for hierarchical organization
- **Auto-registration** - referenced plugins automatically registered without manual configuration
- **Nested skill references** - skills can reference other skills (like filesystem shortcuts)
- **Circular dependency handling** - graceful resolution using "shortcut" pattern with deduplication

---

## Table of Contents

1. [Problem Statement](#problem-statement)
2. [Current Architecture](#current-architecture)
3. [Proposed Architecture](#proposed-architecture)
4. [Design Principles](#design-principles)
5. [Technical Specification](#technical-specification)
6. [Implementation Examples](#implementation-examples)
7. [Migration Strategy](#migration-strategy)
8. [Benefits](#benefits)
9. [Open Items](#open-items)
10. [Timeline](#timeline)

---

## Problem Statement

### Current Limitations

**1. String-Based Function References**
```csharp
// Current approach - no compile-time safety
var skill = new SkillDefinition
{
    Name = "Debugging",
    FunctionReferences = new[] { "FileSystemPlugin.ReadFile", "DebugPlugin.GetStackTrace" }
    //                           ^^^^^^^^^^^^^^^^^^^^^^^^  ^^^^^^^^^^^^^^^^^^^^^
    //                           Strings - typos not caught until runtime!
};
```

**Problems:**
- No compile-time validation
- No IntelliSense/autocomplete
- Breaking changes in function names require manual skill updates
- Typos discovered only at Build() time
- No refactoring support

**2. Skills Separate from Plugins**
```csharp
// Plugins defined in code
public class FileSystemPlugin
{
    [AIFunction]
    public Task<string> ReadFile(string path) { }
}

// Skills defined separately in configuration
builder.WithSkills(skills => {
    skills.DefineSkill("FileOperations", "...",
        functionRefs: new[] { "FileSystemPlugin.ReadFile" });
});
```

**Problems:**
- Cognitive overhead - two separate systems
- Skills can't leverage plugin structure
- No natural grouping mechanism
- Configuration-heavy instead of code-heavy

**3. Manual Plugin Registration Required**
```csharp
// Must register plugins BEFORE defining skills
builder.WithPlugin<FileSystemPlugin>();
builder.WithPlugin<DebugPlugin>();
// Now skills can reference them
builder.WithSkill(skillDefinition);
```

**Problems:**
- Order dependency - plugins must be registered first
- Verbose - must explicitly register all dependencies
- Error-prone - forgetting a plugin causes runtime errors

**4. No Natural Hierarchy**
```csharp
// Current: Flat structure
SkillDefinition {
    Name = "Debugging",
    FunctionReferences = [ /* list of strings */ ]
}
```

**Problems:**
- Can't group related skills
- Can't create skill "containers" like plugin containers
- No discoverability hierarchy for LLMs

---

## Current Architecture

### Plugin System

**Definition:**
```csharp
[PluginScope("File system operations")]
public class FileSystemPlugin
{
    [AIFunction]
    [AIDescription("Read file contents")]
    public async Task<string> ReadFile(string path) { ... }

    [AIFunction]
    [AIDescription("Write file contents")]
    public async Task<string> WriteFile(string path, string content) { ... }
}
```

**Source Generator Output:**
```csharp
// Generated: FileSystemPluginRegistration.g.cs
public static partial class FileSystemPluginRegistration
{
    public static List<AIFunction> CreatePlugin(FileSystemPlugin instance, IPluginMetadataContext? context)
    {
        var functions = new List<AIFunction>();

        // Container if [PluginScope]
        functions.Add(CreateFileSystemPluginContainer());

        // Individual functions
        functions.Add(CreateReadFileFunction(instance, context));
        functions.Add(CreateWriteFileFunction(instance, context));

        return functions;
    }
}
```

**Registration:**
```csharp
builder.WithPlugin<FileSystemPlugin>();
```

### Skill System

**Definition:**
```csharp
var skill = new SkillDefinition
{
    Name = "Debugging",
    Description = "Debug application issues",
    PluginReferences = new[] { "FileSystemPlugin" },
    FunctionReferences = new[] { "DebugPlugin.GetStackTrace" },
    ScopingMode = SkillScopingMode.Scoped,
    PostExpansionInstructions = "Always check logs first..."
};
```

**Registration:**
```csharp
builder.WithSkill(skill);
```

**Runtime Validation:**
- String references validated at `SkillManager.Build()` time
- Throws exception if function not found
- No compile-time safety

### Key Managers

**PluginManager:**
- Stores `PluginRegistration` objects
- Calls generated `CreatePlugin()` via reflection
- Returns `List<AIFunction>`

**SkillManager:**
- Stores `SkillDefinition` objects
- Validates string references against plugin functions
- Creates skill containers

**PluginScopingManager:**
- Filters plugin visibility per turn
- Tracks `expandedPlugins`
- Orders tools: Containers → Non-Plugin → Expanded

**SkillScopingManager:**
- Filters skill visibility per turn
- Tracks `expandedSkills`
- Resolves function references from skills

---

## Proposed Architecture

### Vision: What Developers Will Write

#### Example 1: Standalone Skill Class with Plugin Scope

```csharp
[PluginScope("Debugging workflows and troubleshooting")]
public static class DebuggingSkills
{
    // Skills are static methods returning Skill type
    public static Skill FileDebugging(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            name: "FileDebugging",
            description: "Debug issues by analyzing log files",
            instructions: @"
                1. Use ReadFile to examine error logs
                2. Use GetStackTrace to identify error locations
                3. Document findings with WriteFile
            ",
            // Type-safe method references!
            FileSystemPlugin.ReadFile,
            FileSystemPlugin.WriteFile,
            DebugPlugin.GetStackTrace,
            options: options
        );
    }

    public static Skill DatabaseDebugging(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            name: "DatabaseDebugging",
            description: "Debug database performance issues",
            instructions: "Check slow query log first, then analyze execution plans",
            DatabasePlugin.ExecuteSQL,
            DatabasePlugin.GetQueryPlan,
            FileSystemPlugin.ReadFile,
            options: options
        );
    }
}
```

**Usage:**
```csharp
// Just register the skill class!
builder.WithPlugin<DebuggingSkills>();

// Source generator handles:
// - Detecting FileDebugging and DatabaseDebugging skills
// - Extracting type-safe method references
// - Auto-registering FileSystemPlugin, DebugPlugin, DatabasePlugin
// - Creating container for DebuggingSkills
// - Generating skill AIFunctions
```

#### Example 2: Plugin with Both Functions AND Skills

```csharp
[PluginScope("File system operations")]
public partial class FileSystemPlugin
{
    // Regular AIFunctions
    [AIFunction]
    [AIDescription("Read file contents")]
    public async Task<string> ReadFile(string path) { ... }

    [AIFunction]
    [AIDescription("Write file contents")]
    public async Task<string> WriteFile(string path, string content) { ... }

    [AIFunction]
    [AIDescription("Delete a file")]
    [RequiresPermission]
    public async Task<string> DeleteFile(string path) { ... }

    // Skills within the same plugin!
    public static Skill SafeFileOperations(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            name: "SafeFileOperations",
            description: "Safe file operations with validation",
            instructions: "Always validate paths before operations",
            // Reference own functions
            FileSystemPlugin.ReadFile,
            FileSystemPlugin.WriteFile,
            // Reference other plugins
            ValidationPlugin.ValidatePath,
            options: options
        );
    }

    public static Skill AdminFileOperations(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            name: "AdminFileOperations",
            description: "Administrative file operations including destructive actions",
            instructions: "DANGEROUS: These operations cannot be undone",
            FileSystemPlugin.ReadFile,
            FileSystemPlugin.WriteFile,
            FileSystemPlugin.DeleteFile,
            options: new SkillOptions { ScopingMode = SkillScopingMode.Scoped }
        );
    }
}
```

#### Example 3: Nested Skill References (Skills Referencing Skills)

```csharp
public static class DevelopmentSkills
{
    // Base skill
    public static Skill FileOperations(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            "FileOperations",
            "Basic file operations",
            "Read and write files safely",
            FileSystemPlugin.ReadFile,
            FileSystemPlugin.WriteFile,
            options: options
        );
    }

    // Skill that references another skill + additional functions
    public static Skill CodeReview(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            "CodeReview",
            "Complete code review workflow",
            "Review code changes with context",
            // Reference another skill! (expands to all its functions)
            DevelopmentSkills.FileOperations,
            GitPlugin.GetDiff,
            GitPlugin.GetBlame,
            LintPlugin.CheckStyle,
            options: options
        );
    }

    // Skill referencing skills from multiple classes
    public static Skill FullDebugging(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            "FullDebugging",
            "Comprehensive debugging with all tools",
            "Complete debugging workflow",
            DebuggingSkills.FileDebugging,      // External skill reference
            DebuggingSkills.DatabaseDebugging,  // External skill reference
            ProfilerPlugin.GetMemorySnapshot,
            options: options
        );
    }
}
```

**Resolution (Shortcut Pattern):**
```
FullDebugging references:
  → DebuggingSkills.FileDebugging
      → FileSystemPlugin.ReadFile
      → FileSystemPlugin.WriteFile
      → DebugPlugin.GetStackTrace
  → DebuggingSkills.DatabaseDebugging
      → DatabasePlugin.ExecuteSQL
      → DatabasePlugin.GetQueryPlan
      → FileSystemPlugin.ReadFile (duplicate)
  → ProfilerPlugin.GetMemorySnapshot

Resolved (deduplicated):
  [FileSystemPlugin.ReadFile, FileSystemPlugin.WriteFile,
   DebugPlugin.GetStackTrace, DatabasePlugin.ExecuteSQL,
   DatabasePlugin.GetQueryPlan, ProfilerPlugin.GetMemorySnapshot]
```

---

## Design Principles

### 1. **Skills as First-Class Citizens**

Skills are not configuration objects - they're typed values created by factory methods, just like `AIFunction`.

**Pattern Inspiration:** Microsoft's `AIFunctionFactory.Create()` pattern
```csharp
// Microsoft's pattern for functions
public static AIFunction Create(Delegate method, AIFunctionFactoryOptions? options)

// Our pattern for skills
public static Skill Create(string name, string description, string instructions, params Delegate[] references)
```

### 2. **Type Safety Over Strings**

Compile-time validation instead of runtime string validation.

```csharp
// ❌ Old: String-based (runtime errors)
FunctionReferences = new[] { "FileSystemPlugin.ReadFile" }

// ✅ New: Type-safe (compile errors)
FileSystemPlugin.ReadFile
```

### 3. **Zero Configuration Overhead**

Source generator handles all the heavy lifting.

```csharp
// No manual SkillDefinition objects
// No string array configuration
// Just write type-safe code and register the class
builder.WithPlugin<DebuggingSkills>();
```

### 4. **Filesystem Shortcut Pattern**

Skills work like filesystem shortcuts:
- Skills = Folders containing shortcuts to functions
- Nested skill references = Shortcuts to other folders
- Circular references = OK (like circular symlinks)
- Resolution = Follow all shortcuts, deduplicate files

### 5. **Hierarchical Organization**

Three-level hierarchy for tool organization:

```
[PluginScope] Skill Class (DebuggingSkills)
    ├─ Skill (FileDebugging)
    │   ├─ Function (ReadFile)
    │   ├─ Function (WriteFile)
    │   └─ Function (GetStackTrace)
    └─ Skill (DatabaseDebugging)
        ├─ Function (ExecuteSQL)
        └─ Function (GetQueryPlan)
```

### 6. **Progressive Disclosure**

Tools revealed progressively through expansion:

```
Turn 1: Container (DebuggingSkills)
Turn 2: Skills (FileDebugging, DatabaseDebugging)
Turn 3: Functions (ReadFile, WriteFile, GetStackTrace, etc.)
```

### 7. **Auto-Registration**

No need to manually register dependencies:

```csharp
// Just register the skill class
builder.WithPlugin<DebuggingSkills>();

// Source generator knows it references:
// - FileSystemPlugin
// - DebugPlugin
// These are auto-registered at Build() time
```

---

## Technical Specification

### Core Types

#### Skill Class

```csharp
namespace HPD_Agent.Skills;

/// <summary>
/// Represents a skill - a semantic grouping of functions with instructions.
/// Skills are created via SkillFactory.Create() and processed by source generator.
/// </summary>
public class Skill
{
    /// <summary>
    /// Skill name (used as AIFunction name)
    /// </summary>
    public string Name { get; internal set; } = string.Empty;

    /// <summary>
    /// Description shown in tool list before activation
    /// </summary>
    public string Description { get; internal set; } = string.Empty;

    /// <summary>
    /// Instructions shown after skill activation
    /// </summary>
    public string? Instructions { get; internal set; }

    /// <summary>
    /// References to functions or skills (delegates)
    /// Can be: Func/Action delegates (functions) OR Func&lt;SkillOptions?, Skill&gt; (skills)
    /// </summary>
    public Delegate[] References { get; internal set; } = Array.Empty<Delegate>();

    /// <summary>
    /// Skill configuration options
    /// </summary>
    public SkillOptions Options { get; internal set; } = new();

    // Internal - resolved by source generator during code generation

    /// <summary>
    /// Resolved function references in "PluginName.FunctionName" format
    /// Set by source generator after flattening skill references
    /// </summary>
    internal string[] ResolvedFunctionReferences { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Resolved plugin types that need to be registered
    /// Set by source generator after analyzing all references
    /// </summary>
    internal string[] ResolvedPluginTypes { get; set; } = Array.Empty<string>();
}
```

#### SkillOptions Class

```csharp
namespace HPD_Agent.Skills;

/// <summary>
/// Configuration options for skills
/// </summary>
public class SkillOptions
{
    /// <summary>
    /// Scoping mode for skill function visibility
    /// - InstructionOnly: Functions always visible, skill provides instructions
    /// - Scoped: Functions hidden until skill activated
    /// </summary>
    public SkillScopingMode ScopingMode { get; set; } = SkillScopingMode.InstructionOnly;

    /// <summary>
    /// If true, skill auto-expands at conversation start
    /// </summary>
    public bool AutoExpand { get; set; } = false;

    /// <summary>
    /// Optional paths to instruction document files
    /// Loaded at build time and merged with inline instructions
    /// </summary>
    public string[]? InstructionDocuments { get; set; }

    /// <summary>
    /// Base directory for instruction documents
    /// Default: "skills/documents/"
    /// </summary>
    public string InstructionDocumentBaseDirectory { get; set; } = "skills/documents/";
}
```

#### SkillFactory Class

```csharp
namespace HPD_Agent.Skills;

/// <summary>
/// Factory for creating Skill objects with type-safe function references
/// </summary>
public static class SkillFactory
{
    /// <summary>
    /// Creates a skill with type-safe function/skill references
    /// </summary>
    /// <param name="name">Skill name</param>
    /// <param name="description">Description shown before activation</param>
    /// <param name="instructions">Instructions shown after activation</param>
    /// <param name="references">Function or skill references (delegates)</param>
    /// <returns>Skill object processed by source generator</returns>
    public static Skill Create(
        string name,
        string description,
        string instructions,
        params Delegate[] references)
    {
        return Create(name, description, instructions, null, references);
    }

    /// <summary>
    /// Creates a skill with type-safe function/skill references and options
    /// </summary>
    /// <param name="name">Skill name</param>
    /// <param name="description">Description shown before activation</param>
    /// <param name="instructions">Instructions shown after activation</param>
    /// <param name="options">Skill configuration options</param>
    /// <param name="references">Function or skill references (delegates)</param>
    /// <returns>Skill object processed by source generator</returns>
    public static Skill Create(
        string name,
        string description,
        string instructions,
        SkillOptions? options,
        params Delegate[] references)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Skill name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Skill description cannot be empty", nameof(description));

        return new Skill
        {
            Name = name,
            Description = description,
            Instructions = instructions,
            References = references ?? Array.Empty<Delegate>(),
            Options = options ?? new SkillOptions()
        };
    }
}
```

### Source Generator Enhancements

#### Phase 1: Detection

Detect skill methods in plugin classes:

```csharp
private static bool IsSkillMethod(MethodDeclarationSyntax method, SemanticModel semanticModel)
{
    // Must be: public static Skill MethodName(SkillOptions? options = null)

    if (!method.Modifiers.Any(SyntaxKind.PublicKeyword))
        return false;

    if (!method.Modifiers.Any(SyntaxKind.StaticKeyword))
        return false;

    var returnTypeSymbol = semanticModel.GetTypeInfo(method.ReturnType).Type;
    if (returnTypeSymbol?.Name != "Skill")
        return false;

    // Skill methods should NOT have attributes (distinguish from [AIFunction])
    if (method.AttributeLists.Any())
        return false;

    return true;
}
```

#### Phase 2: Analysis

Extract skill metadata from `SkillFactory.Create()` calls:

```csharp
private static SkillInfo? AnalyzeSkill(
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
        context.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor("HPD002", "Invalid Skill Method",
                $"Skill method '{method.Identifier}' must return SkillFactory.Create()",
                "HPD.Skills", DiagnosticSeverity.Error, true),
            method.GetLocation()));
        return null;
    }

    var arguments = invocation.ArgumentList.Arguments;

    if (arguments.Count < 3)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor("HPD003", "Invalid SkillFactory.Create",
                "SkillFactory.Create() requires at least 3 arguments: name, description, instructions",
                "HPD.Skills", DiagnosticSeverity.Error, true),
            invocation.GetLocation()));
        return null;
    }

    // Extract arguments
    var name = ExtractStringLiteral(arguments[0].Expression, semanticModel);
    var description = ExtractStringLiteral(arguments[1].Expression, semanticModel);
    var instructions = ExtractStringLiteral(arguments[2].Expression, semanticModel);

    // Find SkillOptions argument (optional, can be at position 3 or in named argument)
    SkillOptionsInfo? options = null;
    int referencesStartIndex = 3;

    if (arguments.Count > 3)
    {
        var thirdArg = arguments[3];
        var thirdArgType = semanticModel.GetTypeInfo(thirdArg.Expression).Type;

        if (thirdArgType?.Name == "SkillOptions")
        {
            options = ExtractSkillOptions(thirdArg.Expression, semanticModel);
            referencesStartIndex = 4;
        }
    }

    // Extract function/skill references
    var references = new List<ReferenceInfo>();
    for (int i = referencesStartIndex; i < arguments.Count; i++)
    {
        var reference = AnalyzeReference(arguments[i].Expression, semanticModel, context);
        if (reference != null)
            references.Add(reference);
    }

    return new SkillInfo
    {
        MethodName = method.Identifier.ValueText,
        Name = name,
        Description = description,
        Instructions = instructions,
        Options = options ?? new SkillOptionsInfo(),
        References = references,
        ContainingClass = method.Parent as ClassDeclarationSyntax
    };
}

private static ReferenceInfo? AnalyzeReference(
    ExpressionSyntax expression,
    SemanticModel semanticModel,
    GeneratorSyntaxContext context)
{
    var symbolInfo = semanticModel.GetSymbolInfo(expression);
    var symbol = symbolInfo.Symbol;

    if (symbol is IMethodSymbol methodSymbol)
    {
        var containingType = methodSymbol.ContainingType;
        var returnType = methodSymbol.ReturnType;

        // Check if this is a skill reference
        if (returnType.Name == "Skill")
        {
            return new ReferenceInfo
            {
                ReferenceType = ReferenceType.Skill,
                PluginType = containingType.Name,
                PluginNamespace = containingType.ContainingNamespace.ToDisplayString(),
                MethodName = methodSymbol.Name,
                FullName = $"{containingType.Name}.{methodSymbol.Name}"
            };
        }

        // Check if this is a function reference
        var hasAIFunction = methodSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name.Contains("AIFunction") == true);

        if (!hasAIFunction)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("HPD004", "Invalid Function Reference",
                    $"Method '{containingType.Name}.{methodSymbol.Name}' must have [AIFunction] attribute",
                    "HPD.Skills", DiagnosticSeverity.Error, true),
                expression.GetLocation()));
            return null;
        }

        return new ReferenceInfo
        {
            ReferenceType = ReferenceType.Function,
            PluginType = containingType.Name,
            PluginNamespace = containingType.ContainingNamespace.ToDisplayString(),
            MethodName = methodSymbol.Name,
            FullName = $"{containingType.Name}.{methodSymbol.Name}"
        };
    }

    return null;
}
```

#### Phase 3: Recursive Resolution (Flatten Nested Skills)

Handle skill-to-skill references with circular dependency protection:

```csharp
private class SkillResolver
{
    private readonly Dictionary<string, SkillInfo> _allSkills = new();
    private readonly HashSet<string> _visitedSkills = new();
    private readonly Stack<string> _resolutionStack = new();

    public ResolvedSkillInfo ResolveSkill(SkillInfo skill)
    {
        if (_visitedSkills.Contains(skill.FullName))
        {
            // Already resolved - return cached result
            return _resolvedSkills[skill.FullName];
        }

        if (_resolutionStack.Contains(skill.FullName))
        {
            // Circular reference detected - this is OK!
            // Just return empty to prevent infinite loop
            // The circular reference will be resolved on the other branch
            return new ResolvedSkillInfo
            {
                FunctionReferences = new List<string>(),
                PluginTypes = new List<string>()
            };
        }

        _resolutionStack.Push(skill.FullName);
        _visitedSkills.Add(skill.FullName);

        var functionRefs = new List<string>();
        var pluginTypes = new HashSet<string>();

        foreach (var reference in skill.References)
        {
            if (reference.ReferenceType == ReferenceType.Skill)
            {
                // Recursive: resolve nested skill
                var nestedSkill = _allSkills[reference.FullName];
                var resolved = ResolveSkill(nestedSkill);

                functionRefs.AddRange(resolved.FunctionReferences);
                pluginTypes.UnionWith(resolved.PluginTypes);
            }
            else
            {
                // Direct function reference
                functionRefs.Add(reference.FullName);
                pluginTypes.Add(reference.PluginType);
            }
        }

        _resolutionStack.Pop();

        var result = new ResolvedSkillInfo
        {
            FunctionReferences = functionRefs.Distinct().ToList(),
            PluginTypes = pluginTypes.ToList()
        };

        _resolvedSkills[skill.FullName] = result;
        return result;
    }
}
```

#### Phase 4: Code Generation

Generate registration code with resolved references:

```csharp
// Generated: DebuggingSkillsRegistration.g.cs

namespace HPD_Agent.Skills;

[System.CodeDom.Compiler.GeneratedCodeAttribute("HPDPluginSourceGenerator", "1.0.0.0")]
public static partial class DebuggingSkillsRegistration
{
    // Referenced plugins (for auto-registration)
    private static readonly string[] _referencedPlugins = new[]
    {
        "FileSystemPlugin",
        "DebugPlugin"
    };

    /// <summary>
    /// Gets the list of plugins referenced by skills in this class
    /// Used by AgentBuilder for auto-registration
    /// </summary>
    public static string[] GetReferencedPlugins() => _referencedPlugins;

    /// <summary>
    /// Gets plugin metadata for this skill container
    /// </summary>
    public static PluginMetadata GetPluginMetadata()
    {
        return new PluginMetadata
        {
            Name = "DebuggingSkills",
            Description = "Debugging workflows and troubleshooting",
            FunctionNames = new[] { "FileDebugging", "DatabaseDebugging" },
            FunctionCount = 2,
            HasScopeAttribute = true,
            IsSkillContainer = true
        };
    }

    /// <summary>
    /// Creates all skills as AIFunctions
    /// Called by PluginRegistration.ToAIFunctions()
    /// </summary>
    public static List<AIFunction> CreatePlugin(IPluginMetadataContext? context = null)
    {
        var functions = new List<AIFunction>();

        // Add skill class container (if [PluginScope] present)
        functions.Add(CreateDebuggingSkillsContainer());

        // Add individual skill functions
        functions.Add(CreateFileDebuggingSkill(context));
        functions.Add(CreateDatabaseDebuggingSkill(context));

        return functions;
    }

    /// <summary>
    /// Creates container function for DebuggingSkills class
    /// </summary>
    private static AIFunction CreateDebuggingSkillsContainer()
    {
        return HPDAIFunctionFactory.Create(
            async (arguments, cancellationToken) =>
            {
                return "DebuggingSkills expanded. Available skills: FileDebugging, DatabaseDebugging";
            },
            new HPDAIFunctionFactoryOptions
            {
                Name = "DebuggingSkills",
                Description = "Debugging workflows and troubleshooting",
                SchemaProvider = () => CreateEmptyContainerSchema(),
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["IsContainer"] = true,
                    ["IsSkillContainer"] = true,
                    ["PluginName"] = "DebuggingSkills",
                    ["SkillNames"] = new[] { "FileDebugging", "DatabaseDebugging" },
                    ["FunctionCount"] = 2
                }
            });
    }

    /// <summary>
    /// Creates FileDebugging skill as AIFunction
    /// </summary>
    private static AIFunction CreateFileDebuggingSkill(IPluginMetadataContext? context)
    {
        var instructions = @"
            1. Use ReadFile to examine error logs
            2. Use GetStackTrace to identify error locations
            3. Document findings with WriteFile
        ";

        return HPDAIFunctionFactory.Create(
            async (arguments, cancellationToken) =>
            {
                return $"FileDebugging skill activated.\n\n{instructions}";
            },
            new HPDAIFunctionFactoryOptions
            {
                Name = "FileDebugging",
                Description = "Debug issues by analyzing log files",
                SchemaProvider = () => CreateEmptySchema(),
                Validator = CreateEmptyValidator(),
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["IsSkill"] = true,
                    ["ParentSkillContainer"] = "DebuggingSkills",
                    ["ReferencedFunctions"] = new[]
                    {
                        "FileSystemPlugin.ReadFile",
                        "FileSystemPlugin.WriteFile",
                        "DebugPlugin.GetStackTrace"
                    },
                    ["ReferencedPlugins"] = new[] { "FileSystemPlugin", "DebugPlugin" },
                    ["ScopingMode"] = SkillScopingMode.InstructionOnly,
                    ["AutoExpand"] = false
                }
            });
    }

    /// <summary>
    /// Creates DatabaseDebugging skill as AIFunction
    /// </summary>
    private static AIFunction CreateDatabaseDebuggingSkill(IPluginMetadataContext? context)
    {
        var instructions = "Check slow query log first, then analyze execution plans";

        return HPDAIFunctionFactory.Create(
            async (arguments, cancellationToken) =>
            {
                return $"DatabaseDebugging skill activated.\n\n{instructions}";
            },
            new HPDAIFunctionFactoryOptions
            {
                Name = "DatabaseDebugging",
                Description = "Debug database performance issues",
                SchemaProvider = () => CreateEmptySchema(),
                Validator = CreateEmptyValidator(),
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["IsSkill"] = true,
                    ["ParentSkillContainer"] = "DebuggingSkills",
                    ["ReferencedFunctions"] = new[]
                    {
                        "DatabasePlugin.ExecuteSQL",
                        "DatabasePlugin.GetQueryPlan",
                        "FileSystemPlugin.ReadFile"
                    },
                    ["ReferencedPlugins"] = new[] { "DatabasePlugin", "FileSystemPlugin" },
                    ["ScopingMode"] = SkillScopingMode.InstructionOnly,
                    ["AutoExpand"] = false
                }
            });
    }

    private static JsonElement CreateEmptySchema()
    {
        // Skills take no arguments
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(new Dictionary<string, JsonSchema>())
            .Build();
        return JsonSerializer.SerializeToElement(schema, HPDJsonContext.Default.JsonSchema);
    }

    private static Func<JsonElement, List<ValidationError>> CreateEmptyValidator()
    {
        return _ => new List<ValidationError>();
    }

    private static JsonElement CreateEmptyContainerSchema()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(new Dictionary<string, JsonSchema>())
            .Build();
        return JsonSerializer.SerializeToElement(schema, HPDJsonContext.Default.JsonSchema);
    }
}
```

### AgentBuilder Integration

#### Auto-Registration Logic

```csharp
public Agent Build()
{
    // Phase 1: Create functions from explicitly registered plugins
    var pluginFunctions = new List<AIFunction>();
    var registeredPluginNames = new HashSet<string>();

    foreach (var registration in _pluginManager.GetPluginRegistrations())
    {
        var functions = registration.ToAIFunctions(context);
        pluginFunctions.AddRange(functions);
        registeredPluginNames.Add(registration.PluginType.Name);
    }

    // Phase 2: Discover referenced plugins from all registered plugins
    var referencedPlugins = DiscoverReferencedPlugins(_pluginManager.GetPluginRegistrations());

    // Phase 3: Auto-register referenced plugins
    foreach (var pluginName in referencedPlugins)
    {
        if (!registeredPluginNames.Contains(pluginName))
        {
            _logger?.LogInformation(
                "Auto-registering plugin {PluginName} (referenced by skills)",
                pluginName);

            var pluginType = FindPluginTypeByName(pluginName);
            if (pluginType == null)
            {
                throw new InvalidOperationException(
                    $"Plugin '{pluginName}' is referenced by skills but could not be found. " +
                    $"Ensure the plugin assembly is referenced.");
            }

            _pluginManager.RegisterPlugin(pluginType);
            registeredPluginNames.Add(pluginName);

            // Create functions for auto-registered plugin
            var autoRegistration = PluginRegistration.FromType(pluginType);
            var autoFunctions = autoRegistration.ToAIFunctions(context);
            pluginFunctions.AddRange(autoFunctions);
        }
    }

    // Phase 4: Continue with normal build process...
    // (MCP, skills validation, filters, etc.)
}

private HashSet<string> DiscoverReferencedPlugins(
    IEnumerable<PluginRegistration> registrations)
{
    var referencedPlugins = new HashSet<string>();

    foreach (var registration in registrations)
    {
        // Call generated GetReferencedPlugins() method
        var registrationType = registration.PluginType.Assembly.GetType(
            $"{registration.PluginType.Namespace}.{registration.PluginType.Name}Registration");

        if (registrationType == null)
            continue;

        var method = registrationType.GetMethod(
            "GetReferencedPlugins",
            BindingFlags.Public | BindingFlags.Static);

        if (method == null)
            continue;

        var plugins = method.Invoke(null, null) as string[];
        if (plugins != null)
        {
            foreach (var plugin in plugins)
            {
                referencedPlugins.Add(plugin);
            }
        }
    }

    return referencedPlugins;
}

private Type? FindPluginTypeByName(string pluginName)
{
    // Search all referenced assemblies for the plugin type
    var assemblies = AppDomain.CurrentDomain.GetAssemblies();

    foreach (var assembly in assemblies)
    {
        try
        {
            var types = assembly.GetTypes();
            var pluginType = types.FirstOrDefault(t => t.Name == pluginName);

            if (pluginType != null)
                return pluginType;
        }
        catch (ReflectionTypeLoadException)
        {
            // Skip assemblies we can't load
            continue;
        }
    }

    return null;
}
```

### Unified Scoping Manager

Merge `PluginScopingManager` and `SkillScopingManager`:

```csharp
/// <summary>
/// Unified scoping manager for both plugins and skills
/// </summary>
public class ScopingManager
{
    private readonly Dictionary<string, ContainerInfo> _containers = new();
    private readonly Dictionary<string, FunctionInfo> _functions = new();

    public ScopingManager(IEnumerable<AIFunction> allFunctions)
    {
        // Index all containers and functions
        foreach (var function in allFunctions)
        {
            if (IsContainer(function))
            {
                _containers[function.Name] = new ContainerInfo(function);
            }
            else
            {
                _functions[function.Name] = new FunctionInfo(function);
            }
        }
    }

    /// <summary>
    /// Gets tools visible for current turn based on expansion state
    /// </summary>
    public List<AIFunction> GetToolsForAgentTurn(
        List<AIFunction> allTools,
        HashSet<string> expandedPlugins,
        HashSet<string> expandedSkills)
    {
        var containers = new List<AIFunction>();
        var skills = new List<AIFunction>();
        var nonScopedFunctions = new List<AIFunction>();
        var expandedFunctions = new List<AIFunction>();

        foreach (var tool in allTools)
        {
            if (IsPluginContainer(tool))
            {
                var pluginName = GetPluginName(tool);
                if (!expandedPlugins.Contains(pluginName))
                    containers.Add(tool);
            }
            else if (IsSkillContainer(tool))
            {
                var containerName = GetSkillContainerName(tool);
                if (!expandedSkills.Contains(containerName))
                    containers.Add(tool);
            }
            else if (IsSkill(tool))
            {
                var parentContainer = GetParentSkillContainer(tool);
                if (expandedSkills.Contains(parentContainer))
                    skills.Add(tool);
            }
            else if (IsFunction(tool))
            {
                if (IsHiddenByScopedSkill(tool, expandedSkills))
                    continue;

                var parentPlugin = GetParentPlugin(tool);
                if (string.IsNullOrEmpty(parentPlugin))
                {
                    nonScopedFunctions.Add(tool);
                }
                else if (IsPluginScoped(parentPlugin))
                {
                    if (expandedPlugins.Contains(parentPlugin))
                        expandedFunctions.Add(tool);
                }
                else
                {
                    nonScopedFunctions.Add(tool);
                }
            }
        }

        // Get functions from expanded skills
        var skillReferencedFunctions = GetFunctionsFromExpandedSkills(
            expandedSkills, allTools);

        // Merge and deduplicate
        return containers.OrderBy(c => c.Name)
            .Concat(nonScopedFunctions.OrderBy(f => f.Name))
            .Concat(skills.OrderBy(s => s.Name))
            .Concat(expandedFunctions.OrderBy(f => f.Name))
            .Concat(skillReferencedFunctions.OrderBy(f => f.Name))
            .DistinctBy(f => f.Name)
            .ToList();
    }

    private List<AIFunction> GetFunctionsFromExpandedSkills(
        HashSet<string> expandedSkills,
        List<AIFunction> allTools)
    {
        // Collect all function references from expanded skills
        var referencedFunctionNames = new HashSet<string>();

        foreach (var skillName in expandedSkills)
        {
            var skill = allTools.FirstOrDefault(t =>
                IsSkill(t) && t.Name == skillName);

            if (skill?.AdditionalProperties?
                .TryGetValue("ReferencedFunctions", out var refs) == true)
            {
                if (refs is string[] functionRefs)
                {
                    foreach (var funcRef in functionRefs)
                    {
                        referencedFunctionNames.Add(funcRef);
                    }
                }
            }
        }

        // Find actual AIFunction objects
        return allTools
            .Where(f => IsFunction(f) &&
                (referencedFunctionNames.Contains(f.Name) ||
                 referencedFunctionNames.Contains($"{GetParentPlugin(f)}.{f.Name}")))
            .ToList();
    }

    private bool IsHiddenByScopedSkill(AIFunction function, HashSet<string> expandedSkills)
    {
        // Check if any Scoped skill (not yet expanded) references this function
        var scopedSkills = _functions.Values
            .Where(f => f.IsSkill &&
                   f.ScopingMode == SkillScopingMode.Scoped &&
                   !expandedSkills.Contains(f.ParentContainer ?? ""))
            .ToList();

        foreach (var skill in scopedSkills)
        {
            if (skill.ReferencedFunctions.Contains(function.Name) ||
                skill.ReferencedFunctions.Contains($"{GetParentPlugin(function)}.{function.Name}"))
            {
                return true;
            }
        }

        return false;
    }

    // Helper methods
    private bool IsContainer(AIFunction f) =>
        f.AdditionalProperties?.TryGetValue("IsContainer", out var v) == true && v is bool b && b;

    private bool IsPluginContainer(AIFunction f) =>
        IsContainer(f) && !(f.AdditionalProperties?.TryGetValue("IsSkillContainer", out var v) == true && v is bool b && b);

    private bool IsSkillContainer(AIFunction f) =>
        f.AdditionalProperties?.TryGetValue("IsSkillContainer", out var v) == true && v is bool b && b;

    private bool IsSkill(AIFunction f) =>
        f.AdditionalProperties?.TryGetValue("IsSkill", out var v) == true && v is bool b && b;

    private bool IsFunction(AIFunction f) => !IsContainer(f) && !IsSkill(f);

    private string GetPluginName(AIFunction f) =>
        f.AdditionalProperties?.TryGetValue("PluginName", out var v) == true && v is string s ? s : "";

    private string GetSkillContainerName(AIFunction f) =>
        f.AdditionalProperties?.TryGetValue("PluginName", out var v) == true && v is string s ? s : "";

    private string? GetParentPlugin(AIFunction f) =>
        f.AdditionalProperties?.TryGetValue("ParentPlugin", out var v) == true && v is string s ? s : null;

    private string? GetParentSkillContainer(AIFunction f) =>
        f.AdditionalProperties?.TryGetValue("ParentSkillContainer", out var v) == true && v is string s ? s : null;

    private bool IsPluginScoped(string pluginName) =>
        _containers.TryGetValue(pluginName, out var info) && !info.IsSkillContainer;
}
```

---

## Implementation Examples

### Example 1: Simple Skill Class

```csharp
[PluginScope("Utility workflows")]
public static class UtilitySkills
{
    public static Skill TextProcessing(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            "TextProcessing",
            "Process and analyze text files",
            "Use ReadFile to load text, then process with text utilities",
            FileSystemPlugin.ReadFile,
            FileSystemPlugin.WriteFile,
            TextUtilsPlugin.CountWords,
            TextUtilsPlugin.FindPattern,
            options: options
        );
    }
}
```

**Generated:**
- `UtilitySkillsRegistration.g.cs`
- Container function: `UtilitySkills`
- Skill function: `TextProcessing`
- Auto-registers: `FileSystemPlugin`, `TextUtilsPlugin`

### Example 2: Plugin with Mixed Functions and Skills

```csharp
[PluginScope("Git operations")]
public partial class GitPlugin
{
    // Regular functions
    [AIFunction]
    public async Task<string> GetDiff(string? path = null) { ... }

    [AIFunction]
    public async Task<string> GetBlame(string path) { ... }

    [AIFunction]
    public async Task<string> Commit(string message) { ... }

    // Skills using own functions + others
    public static Skill CodeReview(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            "CodeReview",
            "Review code changes",
            "Check diff first, then examine blame for context",
            GitPlugin.GetDiff,
            GitPlugin.GetBlame,
            FileSystemPlugin.ReadFile,
            options: options
        );
    }
}
```

**Generated:**
- `GitPluginRegistration.g.cs`
- Container: `GitPlugin`
- Functions: `GetDiff`, `GetBlame`, `Commit`
- Skill: `CodeReview`
- Auto-registers: `FileSystemPlugin`

### Example 3: Nested Skills

```csharp
public static class WorkflowSkills
{
    // Base skill
    public static Skill BasicFileOps(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            "BasicFileOps",
            "Basic file operations",
            "Read and write files",
            FileSystemPlugin.ReadFile,
            FileSystemPlugin.WriteFile,
            options: options
        );
    }

    // Composite skill
    public static Skill AdvancedFileProcessing(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            "AdvancedFileProcessing",
            "Advanced file processing with validation",
            "Validate paths, then use file operations",
            WorkflowSkills.BasicFileOps,  // Nested skill!
            ValidationPlugin.ValidatePath,
            ValidationPlugin.CheckPermissions,
            options: options
        );
    }
}
```

**Resolution:**
```
AdvancedFileProcessing expands to:
  - FileSystemPlugin.ReadFile (from BasicFileOps)
  - FileSystemPlugin.WriteFile (from BasicFileOps)
  - ValidationPlugin.ValidatePath
  - ValidationPlugin.CheckPermissions
```

### Example 4: Scoped Skill for Dangerous Operations

```csharp
public static class AdminSkills
{
    public static Skill DatabaseMaintenance(SkillOptions? options = null)
    {
        return SkillFactory.Create(
            "DatabaseMaintenance",
            "Database maintenance operations (DANGEROUS)",
            @"WARNING: These operations affect production data.
              Always backup first using BackupDatabase.",
            DatabasePlugin.BackupDatabase,
            DatabasePlugin.ExecuteSQL,
            DatabasePlugin.TruncateTable,
            DatabasePlugin.DropTable,
            new SkillOptions
            {
                ScopingMode = SkillScopingMode.Scoped  // Hide until activated!
            }
        );
    }
}
```

**Behavior:**
- Functions hidden until `DatabaseMaintenance` skill activated
- Agent must explicitly choose to use dangerous operations
- Instructions emphasize safety protocols

---

## Migration Strategy

### Phase 1: Foundation (Non-Breaking)

**Add new types without breaking existing code:**

1. Add `Skill` class
2. Add `SkillFactory` class
3. Add `SkillOptions` class
4. Keep existing `SkillDefinition` working

**Timeline:** Week 1

### Phase 2: Source Generator Enhancement (Non-Breaking)

**Extend generator to detect and process skills:**

1. Add skill method detection
2. Add skill analysis logic
3. Add recursive resolution
4. Generate skill registration code
5. Keep existing plugin-only generation working

**Timeline:** Week 2-3

### Phase 3: Auto-Registration (Non-Breaking)

**Add auto-registration logic:**

1. Add `GetReferencedPlugins()` to generated code
2. Implement `DiscoverReferencedPlugins()` in AgentBuilder
3. Implement auto-registration in `Build()`
4. Add logging for auto-registered plugins
5. Keep explicit registration working

**Timeline:** Week 4

### Phase 4: Unified Scoping Manager (Breaking Change)

**Merge scoping managers:**

1. Create new `ScopingManager` class
2. Merge logic from `PluginScopingManager` and `SkillScopingManager`
3. Update `Agent.cs` to use unified manager
4. Deprecate old managers
5. Update tests

**Timeline:** Week 5-6

### Phase 5: Documentation & Migration Guide (Non-Breaking)

**Help developers migrate:**

1. Write migration guide (old → new patterns)
2. Update all examples in documentation
3. Create before/after comparison examples
4. Document best practices
5. Create video walkthrough

**Timeline:** Week 7

### Phase 6: Deprecation (Breaking Change)

**Remove old skill system:**

1. Mark `SkillDefinition` as `[Obsolete]`
2. Mark `SkillManager` as `[Obsolete]`
3. Mark `SkillScopingManager` as `[Obsolete]`
4. Add deprecation warnings
5. Update examples to use new system

**Timeline:** Week 8

### Phase 7: Cleanup (Breaking Change - Major Version)

**Remove deprecated code:**

1. Remove `SkillDefinition`
2. Remove old `SkillManager`
3. Remove old `SkillScopingManager`
4. Remove string-based skill configuration
5. Bump major version

**Timeline:** Week 9-10

---

## Benefits

### For Developers

✅ **Type Safety**
- Compile-time validation of skill references
- IDE autocomplete and IntelliSense
- Refactoring support (rename, find usages)
- No runtime string resolution errors

✅ **Reduced Boilerplate**
- No manual `SkillDefinition` objects
- No string array configuration
- No explicit plugin dependency registration
- Source generator handles everything

✅ **Better Organization**
- Skills naturally grouped with related plugins
- Clear hierarchical structure
- Skills can be versioned with plugins
- Easy to find and maintain

✅ **Consistency**
- Skills use same patterns as functions
- `SkillFactory.Create()` mirrors `AIFunctionFactory.Create()`
- Unified registration via `WithPlugin<T>()`
- Consistent metadata structure

### For LLMs (Agents)

✅ **Progressive Disclosure**
```
Turn 1: DebuggingSkills (container)
Turn 2: FileDebugging, DatabaseDebugging (skills)
Turn 3: ReadFile, WriteFile, GetStackTrace (functions)
```

✅ **Hierarchical Organization**
- Clear grouping of related capabilities
- Skills provide context and instructions
- Reduces tool list overwhelm

✅ **Semantic Grouping**
- Skills group functions by purpose, not just by plugin
- Cross-plugin workflows are first-class
- Better matches how agents think about tasks

### For System Architecture

✅ **Simplified Codebase**
- One less manager (`SkillScopingManager` merged)
- Less string-based logic
- Fewer runtime validation paths
- More compile-time guarantees

✅ **Better Performance**
- No runtime string resolution
- Fewer lookups and validations
- Generated code is optimized
- Deduplication happens once during resolution

✅ **Maintainability**
- Type-safe references prevent breaking changes
- Circular dependencies handled gracefully
- Clear separation of concerns
- Easier to test and debug

---

## Open Items

### 1. Skill Versioning Strategy (TODO)

**Problem:** What happens when a function signature changes and skills reference it?

**Potential Solutions:**
- Semantic versioning for skills
- Breaking change detection in CI/CD
- Deprecation warnings in source generator
- Migration path documentation

**Action:** Create separate proposal for versioning strategy

### 2. Circular Dependency Detection

**Current Approach:** Graceful handling via visited set and deduplication

**Open Questions:**
- Should we warn about circular references?
- Should we limit recursion depth?
- Should we visualize skill dependency graphs?

**Decision:** Monitor usage patterns and add warnings if needed

### 3. Skill Instruction Document Loading

**Current Behavior:** Load from filesystem at Build() time

**Open Questions:**
- Should we support embedded resources?
- Should we support HTTP URLs?
- Should we cache loaded documents?

**Decision:** Start with filesystem only, add others based on user feedback

### 4. Skills-Only Mode Migration

**Current:** `PluginScopingConfig.SkillsOnlyMode` hides unreferenced functions

**Open Questions:**
- Should this work with new skill system?
- Should it be renamed?
- Should it have different semantics?

**Decision:** Keep existing behavior, evaluate if changes needed after migration

### 5. MCP and Frontend Tool Integration

**Current:** External tools wrapped via `ExternalToolScopingWrapper`

**Open Questions:**
- Can skills reference MCP functions?
- Can MCP tools be organized into skills?
- How to handle runtime-discovered MCP tools?

**Decision:** Keep external tools separate for now, revisit in future

---

## Timeline

### Estimated Timeline: 10 Weeks

**Week 1:** Foundation types
**Week 2-3:** Source generator enhancements
**Week 4:** Auto-registration logic
**Week 5-6:** Unified scoping manager
**Week 7:** Documentation and migration guide
**Week 8:** Deprecation warnings
**Week 9-10:** Cleanup and major version release

### Milestones

**M1 (End of Week 3):** Skills can be defined and detected
**M2 (End of Week 4):** Auto-registration working
**M3 (End of Week 6):** Unified scoping working
**M4 (End of Week 7):** Documentation complete
**M5 (End of Week 10):** Old system removed, v2.0.0 released

---

## Conclusion

This proposal represents a fundamental architectural improvement to HPD-Agent that:

1. **Eliminates string-based references** in favor of type-safe compile-time references
2. **Unifies plugins and skills** into a consistent first-class citizen system
3. **Reduces boilerplate** through intelligent source generation
4. **Improves developer experience** with better IDE support and fewer errors
5. **Maintains flexibility** with nested skills and circular reference support
6. **Simplifies the codebase** by merging managers and reducing runtime logic

The migration path is carefully designed to be **non-breaking** until the final cleanup phase, allowing gradual adoption and testing.

The result is a more elegant, type-safe, and maintainable system that better serves both developers and LLMs using HPD-Agent.

---

## Appendices

### Appendix A: Comparison Matrix

| Feature | Current (v1) | Proposed (v2) |
|---------|-------------|---------------|
| Function references | Strings | Type-safe delegates |
| Compile-time validation | ❌ No | ✅ Yes |
| IDE autocomplete | ❌ Limited | ✅ Full |
| Skill definition | Configuration objects | Factory methods |
| Plugin registration | Manual, order-dependent | Auto-registration |
| Nested skills | ❌ Not supported | ✅ Supported |
| Circular references | ❌ Not supported | ✅ Gracefully handled |
| Skills in plugins | ❌ Separate | ✅ Integrated |
| Scoping managers | 2 separate | 1 unified |
| Refactoring support | ❌ Manual updates | ✅ Automatic |

### Appendix B: File Structure

```
HPD-Agent/
├── Agent/
│   ├── Agent.cs (updated: uses ScopingManager)
│   ├── AgentBuilder.cs (updated: auto-registration)
│   └── AgentConfig.cs (unchanged)
├── Plugins/
│   ├── Attributes/ (unchanged)
│   ├── PluginManager.cs (unchanged)
│   ├── PluginRegistration.cs (unchanged)
│   └── HPDAIFunctionFactory.cs (unchanged)
├── Skills/
│   ├── Skill.cs (NEW)
│   ├── SkillFactory.cs (NEW)
│   ├── SkillOptions.cs (NEW)
│   ├── ScopingManager.cs (NEW - merged)
│   ├── SkillDefinition.cs (deprecated)
│   ├── SkillManager.cs (deprecated)
│   └── SkillScopingManager.cs (deprecated)
└── SourceGenerator/
    ├── HPDPluginSourceGenerator.cs (updated: skill detection)
    ├── SkillAnalyzer.cs (NEW)
    ├── SkillResolver.cs (NEW)
    └── DSLCodeGenerator.cs (updated: skill code gen)
```

### Appendix C: Glossary

**Skill:** A semantic grouping of functions from one or more plugins with instructions

**Skill Container:** A plugin class with `[PluginScope]` that contains skill methods

**Skill Method:** A `public static Skill MethodName(SkillOptions?)` method

**Skill Reference:** A skill method used as a parameter in another skill's `SkillFactory.Create()`

**Function Reference:** A plugin method with `[AIFunction]` used in `SkillFactory.Create()`

**Nested Skill:** A skill that references other skills

**Circular Reference:** Two or more skills that reference each other (supported)

**Auto-Registration:** Automatic plugin registration based on skill references

**Shortcut Pattern:** Resolution strategy where skills act as shortcuts to functions

**Progressive Disclosure:** Hierarchical tool revelation through container expansion

---

**End of Proposal**
