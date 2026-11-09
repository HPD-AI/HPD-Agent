# Resources And Scripts

Resources and scripts become individually selectable functions only after their parent skill activates. Resources are parameterless. Every script has an explicit input contract, including scripts that intentionally accept no arguments.

Resource and script names must contain 1-64 ASCII letters, digits, underscores,
or hyphens and must start with a letter or underscore. The same model-facing
name rules apply to code-first and directory-defined capabilities.

## Resources

A resource is read-only supporting content:

```csharp
capabilities:
[
    SkillCapabilities.Resource(
        "read_validation_guide",
        "Reads the requirements used to validate a dataset.",
        "Every row must have a unique identifier.")
]
```

Available resource implementations include:

| Type | Use it for |
|---|---|
| `InlineSkillResource` | Small immutable values |
| `DelegateSkillResource` | Values resolved by an AOT-safe delegate |
| `EmbeddedSkillResource` | Content shipped with an assembly through an explicit stream accessor |
| `ContentStoreSkillResource` | Exact installed or versioned content snapshots |
| `FileSkillResource` | Development content beneath an approved root |

A content-store resource uses an exact `ContentAddress`; HPD does not search every scope for a logical filename. A capability that changes external state should be an `[AIFunction]` or a script, not a resource.

## External Scripts

Use `SkillScript` for Python, shell, JavaScript, WASM, remote jobs, or other externally executed content. C# behavior should remain an ordinary `[AIFunction]`.

```csharp
SkillCapabilities.Script(
    name: "normalize_dataset",
    description: "Normalizes the active dataset and returns the resulting artifact.",
    reference: new FileScriptReference(
        "scripts/normalize.py",
        runtime: "python"),
    input: SkillScriptInput.Empty,
    requiresPermission: true,
    timeout: TimeSpan.FromMinutes(1),
    maximumOutputBytes: 512_000)
```

`SkillScriptInput.Empty` is explicit: HPD does not infer an empty contract when `input` is omitted. For typed arguments, declare a partial input type and let the source generator own its schema, validation, binding, defaults, and effective JSON:

```csharp
[AIInputContract]
public sealed partial record NormalizeInput(
    string InputFile,
    int MaximumRows = 1_000);

SkillCapabilities.Script(
    name: "normalize_dataset",
    description: "Normalizes one dataset and returns the resulting artifact.",
    reference: new FileScriptReference("scripts/normalize.py", "python"),
    input: NormalizeInput.AIContract)
```

For installed or directory-defined scripts whose CLR type cannot be known at build time, use a bounded canonical schema:

```csharp
using var schema = JsonDocument.Parse("""
    {
      "type": "object",
      "properties": {
        "inputFile": { "type": "string" },
        "maximumRows": {
          "type": "integer",
          "minimum": 1,
          "maximum": 100000,
          "default": 1000
        }
      },
      "required": ["inputFile"],
      "additionalProperties": false
    }
    """);

var input = SkillScriptInput.FromCanonicalSchema(schema.RootElement);
```

Dynamic schemas are compiled immediately. Unsupported keywords, open roots, invalid defaults, and excessive schema complexity fail before the script is published.

HPD does not ship an in-process Python, shell, or JavaScript interpreter. Register an `ISkillScriptRunner` that owns the execution and isolation policy:

```csharp
public sealed class PythonSkillRunner : ISkillScriptRunner
{
    public bool CanRun(SkillScript script) =>
        script.Reference.Runtime == "python";

    public ValueTask<object?> RunAsync(
        SkillScriptExecutionContext context,
        CancellationToken cancellationToken)
    {
        JsonElement effectiveInput = context.Arguments.Json;
        if (context.Arguments.TryGet<NormalizeInput>(out var typed))
        {
            // In-process infrastructure may use the generated value.
        }

        // External runtimes receive effectiveInput as UTF-8 JSON through
        // stdin or the equivalent request body. Never interpolate model
        // values into a command line.
        throw new NotImplementedException();
    }
}

services.AddSingleton<ISkillScriptRunner, PythonSkillRunner>();
```

For process-like runners, write the validated payload directly to stdin:

```csharp
await context.Arguments.WriteJsonAsync(
    process.StandardInput.BaseStream,
    cancellationToken);
process.StandardInput.Close();
```

This writes the effective canonical JSON exactly. Do not interpolate model
arguments into an executable name, command line, or shell fragment.

Exactly one registered runner must accept each visible script. Zero or multiple matches cause agent construction or reload validation to fail.

Scripts require permission by default. Activating a skill does not run its scripts. The runner always receives validated, detached effective JSON with declared defaults materialized. Generated contracts also expose their typed CLR value through `TryGet<T>` or `GetRequired<T>`. Ambient execution context, artifacts, package content, and injected services remain available.
