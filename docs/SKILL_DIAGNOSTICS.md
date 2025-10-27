# HPD-Agent Skill System Diagnostic Codes

This document defines all diagnostic codes, error messages, and their severity levels for the unified skill architecture.

## Diagnostic Code Ranges

- **HPD001-HPD099**: Skill definition errors
- **HPD100-HPD199**: Skill reference errors
- **HPD200-HPD299**: Source generator errors
- **HPD300-HPD399**: Auto-registration errors
- **HPD400-HPD499**: Scoping and visibility errors

---

## Skill Definition Errors (HPD001-HPD099)

### HPD001: Invalid Skill Method Signature

**Severity:** Error
**Category:** HPD.Skills
**Message:** `Skill method '{0}' must be public, static, and return type 'Skill'`

**Description:** A method intended to be a skill doesn't follow the required signature pattern.

**Example:**
```csharp
// ❌ Invalid - not static
public Skill FileDebugging() { }

// ❌ Invalid - not public
private static Skill FileDebugging() { }

// ❌ Invalid - wrong return type
public static void FileDebugging() { }

// ✅ Valid
public static Skill FileDebugging(SkillOptions? options = null) { }
```

**Fix:** Ensure skill methods are `public static Skill MethodName(SkillOptions? options = null)`

---

### HPD002: Missing SkillFactory.Create Call

**Severity:** Error
**Category:** HPD.Skills
**Message:** `Skill method '{0}' must return SkillFactory.Create()`

**Description:** A skill method doesn't contain a `SkillFactory.Create()` call in its body.

**Example:**
```csharp
// ❌ Invalid
public static Skill FileDebugging(SkillOptions? options = null)
{
    return new Skill(); // Direct instantiation not allowed
}

// ✅ Valid
public static Skill FileDebugging(SkillOptions? options = null)
{
    return SkillFactory.Create("FileDebugging", "...", "...",
        FileSystemPlugin.ReadFile);
}
```

**Fix:** Use `SkillFactory.Create()` to create skill instances

---

### HPD003: Insufficient SkillFactory.Create Arguments

**Severity:** Error
**Category:** HPD.Skills
**Message:** `SkillFactory.Create() requires at least 3 arguments: name, description, instructions`

**Description:** `SkillFactory.Create()` called with fewer than required arguments.

**Example:**
```csharp
// ❌ Invalid
return SkillFactory.Create("FileDebugging", "Debug files");

// ✅ Valid
return SkillFactory.Create(
    "FileDebugging",
    "Debug issues by analyzing log files",
    "Use ReadFile to examine logs...");
```

**Fix:** Provide all required arguments (name, description, instructions)

---

### HPD004: Empty Skill Name

**Severity:** Error
**Category:** HPD.Skills
**Message:** `Skill name cannot be null or whitespace`

**Description:** The skill name argument is empty or whitespace.

**Example:**
```csharp
// ❌ Invalid
return SkillFactory.Create("", "Description", "Instructions");

// ✅ Valid
return SkillFactory.Create("FileDebugging", "Description", "Instructions");
```

**Fix:** Provide a non-empty skill name

---

### HPD005: Empty Skill Description

**Severity:** Error
**Category:** HPD.Skills
**Message:** `Skill description cannot be null or whitespace`

**Description:** The skill description argument is empty or whitespace.

**Fix:** Provide a non-empty skill description

---

### HPD006: Skill Name Mismatch

**Severity:** Warning
**Category:** HPD.Skills
**Message:** `Skill name '{0}' doesn't match method name '{1}'. Consider using '{1}' for consistency.`

**Description:** The skill name passed to `SkillFactory.Create()` differs from the method name.

**Example:**
```csharp
// ⚠️ Warning
public static Skill FileDebugging(SkillOptions? options = null)
{
    return SkillFactory.Create("FileDiagnostics", "...", "..."); // Name mismatch
}

// ✅ Better
public static Skill FileDebugging(SkillOptions? options = null)
{
    return SkillFactory.Create("FileDebugging", "...", "..."); // Names match
}
```

**Fix:** Use the same name for the method and skill

---

## Skill Reference Errors (HPD100-HPD199)

### HPD100: Invalid Function Reference

**Severity:** Error
**Category:** HPD.Skills
**Message:** `Method '{0}.{1}' must have [AIFunction] attribute to be referenced by skills`

**Description:** A skill references a method that doesn't have the `[AIFunction]` attribute.

**Example:**
```csharp
public class FileSystemPlugin
{
    // ❌ Missing [AIFunction]
    public async Task<string> ReadFile(string path) { }
}

public static Skill FileDebugging(SkillOptions? options = null)
{
    return SkillFactory.Create("FileDebugging", "...", "...",
        FileSystemPlugin.ReadFile); // ❌ Error: not an AIFunction
}
```

**Fix:** Add `[AIFunction]` attribute to the referenced method

---

### HPD101: Non-Method Reference

**Severity:** Error
**Category:** HPD.Skills
**Message:** `Skill reference must be a method delegate, found '{0}'`

**Description:** A skill references something other than a method (e.g., property, field).

**Example:**
```csharp
// ❌ Invalid - referencing a property
return SkillFactory.Create("Test", "...", "...",
    FileSystemPlugin.SomeProperty);

// ✅ Valid - referencing a method
return SkillFactory.Create("Test", "...", "...",
    FileSystemPlugin.ReadFile);
```

**Fix:** Only reference methods with `[AIFunction]` or other skill methods

---

### HPD102: Ambiguous Reference

**Severity:** Error
**Category:** HPD.Skills
**Message:** `Cannot resolve reference to '{0}' - multiple candidates found`

**Description:** The source generator found multiple methods matching the reference.

**Fix:** Use fully qualified method references

---

### HPD103: Unresolved Reference

**Severity:** Error
**Category:** HPD.Skills
**Message:** `Cannot resolve reference to '{0}' - method not found`

**Description:** The source generator couldn't find the referenced method.

**Fix:** Ensure the referenced method exists and is accessible

---

### HPD104: Circular Skill Reference Depth Exceeded

**Severity:** Error
**Category:** HPD.Skills
**Message:** `Circular skill reference depth exceeded (max: {0}). Check for circular dependencies in: {1}`

**Description:** Nested skill references exceed the maximum depth limit (default: 50).

**Example:**
```csharp
// ❌ Circular reference
public static Skill SkillA(SkillOptions? options = null)
{
    return SkillFactory.Create("SkillA", "...", "...",
        MySkills.SkillB); // References SkillB
}

public static Skill SkillB(SkillOptions? options = null)
{
    return SkillFactory.Create("SkillB", "...", "...",
        MySkills.SkillA); // References SkillA - circular!
}
```

**Note:** Circular references are allowed and handled gracefully, but excessive depth suggests a problem.

**Fix:** Review skill reference chain for unintended circular dependencies

---

### HPD105: No Function References

**Severity:** Warning
**Category:** HPD.Skills
**Message:** `Skill '{0}' has no function references. Skills should reference at least one function or skill.`

**Description:** A skill doesn't reference any functions or other skills.

**Example:**
```csharp
// ⚠️ Warning - no references
return SkillFactory.Create("EmptySkill", "...", "...");
```

**Fix:** Add function or skill references, or remove the skill if not needed

---

## Source Generator Errors (HPD200-HPD299)

### HPD200: Code Generation Failed

**Severity:** Error
**Category:** HPD.SourceGenerator
**Message:** `Failed to generate registration code for skill '{0}': {1}`

**Description:** The source generator encountered an error while generating code.

**Fix:** Check the error details and report if this is a generator bug

---

### HPD201: Template Validation Failed

**Severity:** Error
**Category:** HPD.SourceGenerator
**Message:** `Template property '{0}' not found on context type '{1}'`

**Description:** A description template references a property that doesn't exist on the context type.

**Example:**
```csharp
[AIFunction<MyContext>]
[AIDescription("Use {context.NonExistentProperty}")]
public async Task<string> MyFunction() { }
```

**Fix:** Ensure template properties exist on the context type

---

### HPD202: Partial Class Not Marked Partial

**Severity:** Error
**Category:** HPD.SourceGenerator
**Message:** `Class '{0}' contains skills but is not marked partial. Add 'partial' keyword.`

**Description:** A class containing skill methods must be marked `partial` for source generation.

**Example:**
```csharp
// ❌ Invalid
public class MySkills
{
    public static Skill FileDebugging(SkillOptions? options = null) { }
}

// ✅ Valid
public partial class MySkills
{
    public static Skill FileDebugging(SkillOptions? options = null) { }
}
```

**Fix:** Add `partial` keyword to the class declaration

---

### HPD203: Duplicate Skill Name

**Severity:** Error
**Category:** HPD.SourceGenerator
**Message:** `Duplicate skill name '{0}' found in '{1}' and '{2}'`

**Description:** Two skills have the same name, which would cause conflicts.

**Fix:** Use unique names for all skills

---

## Auto-Registration Errors (HPD300-HPD399)

### HPD300: Referenced Plugin Not Found

**Severity:** Error
**Category:** HPD.AutoRegistration
**Message:** `Plugin '{0}' is referenced by skill '{1}' but could not be found. Ensure the plugin assembly is referenced.`

**Description:** A skill references a plugin that couldn't be found in any loaded assemblies.

**Example:**
```csharp
// Skill references DatabasePlugin.ExecuteSQL
// but DatabasePlugin assembly not referenced
```

**Fix:** Add assembly reference for the missing plugin

---

### HPD301: Plugin Auto-Registration Failed

**Severity:** Error
**Category:** HPD.AutoRegistration
**Message:** `Failed to auto-register plugin '{0}': {1}`

**Description:** Auto-registration of a plugin failed.

**Fix:** Check the error details and ensure the plugin can be instantiated

---

### HPD302: Plugin Registration Type Ambiguity

**Severity:** Error
**Category:** HPD.AutoRegistration
**Message:** `Multiple types named '{0}' found. Use fully qualified name.`

**Description:** Multiple plugin types with the same name exist in different namespaces.

**Fix:** Use fully qualified type names in skill references

---

## Scoping and Visibility Errors (HPD400-HPD499)

### HPD400: Invalid Scoping Mode

**Severity:** Warning
**Category:** HPD.Scoping
**Message:** `Skill '{0}' has ScopingMode.Scoped but references no functions. Consider using InstructionOnly.`

**Description:** A scoped skill doesn't reference any functions, making scoping pointless.

**Fix:** Add function references or change to `InstructionOnly` mode

---

### HPD401: Conflicting Visibility Rules

**Severity:** Warning
**Category:** HPD.Scoping
**Message:** `Function '{0}' is referenced by both Scoped and InstructionOnly skills. This may cause unexpected visibility behavior.`

**Description:** A function is referenced by skills with different scoping modes.

**Fix:** Review skill scoping modes for consistency

---

### HPD402: Auto-Expand with Scoped Mode

**Severity:** Info
**Category:** HPD.Scoping
**Message:** `Skill '{0}' has AutoExpand=true with ScopingMode.Scoped. Functions will be visible immediately.`

**Description:** Informational message about auto-expanding scoped skills.

---

## Diagnostic Usage in Code

### Example: Reporting a Diagnostic

```csharp
context.ReportDiagnostic(Diagnostic.Create(
    new DiagnosticDescriptor(
        id: "HPD002",
        title: "Missing SkillFactory.Create Call",
        messageFormat: "Skill method '{0}' must return SkillFactory.Create()",
        category: "HPD.Skills",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A skill method doesn't contain a SkillFactory.Create() call in its body.",
        helpLinkUri: "https://docs.hpd-agent.com/skills/diagnostics#HPD002"),
    location: method.GetLocation(),
    method.Identifier.ValueText));
```

### Example: Warning for Best Practices

```csharp
if (skillName != methodName)
{
    context.ReportDiagnostic(Diagnostic.Create(
        new DiagnosticDescriptor(
            id: "HPD006",
            title: "Skill Name Mismatch",
            messageFormat: "Skill name '{0}' doesn't match method name '{1}'. Consider using '{1}' for consistency.",
            category: "HPD.Skills",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true),
        location: nameArgument.GetLocation(),
        skillName,
        methodName));
}
```

---

## Suppressing Diagnostics

To suppress specific diagnostics, use `#pragma` directives or `.editorconfig`:

### Using #pragma

```csharp
#pragma warning disable HPD006 // Suppress name mismatch warning
public static Skill FileDebugging(SkillOptions? options = null)
{
    return SkillFactory.Create("FileDiagnostics", "...", "...");
}
#pragma warning restore HPD006
```

### Using .editorconfig

```ini
[*.cs]
dotnet_diagnostic.HPD006.severity = none
```

---

## Future Diagnostic Codes

Reserved for future use:

- **HPD500-HPD599**: Performance and optimization warnings
- **HPD600-HPD699**: Conditional skill errors
- **HPD700-HPD799**: Instruction document errors
- **HPD800-HPD899**: MCP integration errors (future)
- **HPD900-HPD999**: Reserved for extensions

---

**Last Updated:** 2025-10-26
**Version:** 1.0.0
