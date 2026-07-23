# AI Function Parameter Contracts

An `[AIFunction]` method is a compiled protocol between the model and the tool implementation. HPD's source generator emits the model-facing JSON Schema, a strict argument binder, the typed argument carrier, and the invocation adapter from the same C# signature.

Use this page when deciding how to represent tool parameters. For harness registration and construction, see [Author A Tool Harness](author-a-tool-harness.md). For generation and Native AOT behavior, see [Source Generation, AOT, And Trimming](source-generation-aot-and-trimming.md).

## Contract Lifecycle

```text
C# method signature
  -> generated closed JSON Schema
  -> model function call
  -> raw or runtime-canonical JSON
  -> generated strict binder
  -> typed arguments
  -> function body
```

The generated schema and binder are emitted together. A property advertised to the model is bound by the same generated contract; runtime-only parameters are injected separately.

Generated contracts are closed. Unknown properties, incorrect JSON kinds, alternate property casing, invalid enum names, and fields from the wrong union branch fail before the function body runs.

## Scalar Parameters

Use ordinary scalar parameters directly:

```csharp
public sealed class SearchTools
{
    [AIFunction(Description = "Searches indexed documents.")]
    public string Search(
        [AIDescription("Text to search for.")] string query,
        [AIDescription("Maximum number of results.")] int limit = 20,
        [AIDescription("Whether matching is case-sensitive.")] bool caseSensitive = false)
    {
        // ...
    }
}
```

Required non-nullable parameters appear in the schema's `required` array. Parameters with C# defaults are optional and use the declared default when omitted.

Use nullable types when JSON `null` is meaningful:

```csharp
public string FindOwner(string repository, string? team = null)
```

Nullability and optionality are different:

- `string value` without a default is required and cannot be `null`.
- `string? value` without a default is required but may be `null`.
- `string? value = null` may be omitted or supplied as `null`.

## Enums

Enums produce exact string values:

```csharp
public enum SearchMode
{
    Files,
    Content,
    Count
}

[AIFunction]
public string Search(string query, SearchMode mode = SearchMode.Content)
```

The model must use the generated name exactly:

```json
{
  "query": "TODO",
  "mode": "Content"
}
```

`"content"`, numeric enum values, and undeclared aliases are rejected. Choose stable enum member names because they are part of the model-facing protocol.

## Nested Objects And Records

Use a public record or class when several values form one concept:

```csharp
using System.ComponentModel;

public sealed record DateRange(
    [property: Description("Inclusive start date.")] DateOnly Start,
    [property: Description("Inclusive end date.")] DateOnly End);

public sealed class ReportTools
{
    [AIFunction]
    public string CreateReport(string project, DateRange range)
    {
        // ...
    }
}
```

The model sees a nested object:

```json
{
  "project": "HPD Agent",
  "range": {
    "start": "2026-07-01",
    "end": "2026-07-31"
  }
}
```

Prefer immutable positional records for model-facing value objects. They make required values and construction semantics explicit. Nested generated objects are also closed, so an unexpected property inside `range` is rejected.

## Collections

Use supported arrays or collection interfaces when the binder has deterministic construction semantics:

```csharp
public sealed record Replacement(string OldText, string NewText);

[AIFunction]
public string ReplaceMany(
    string path,
    IReadOnlyList<Replacement> replacements,
    IReadOnlyList<string>? includeGlobs = null)
```

The generated schema describes both the array and its item contract. Every nested item is validated before construction.

Use a dictionary for model-selected string keys:

```csharp
[AIFunction]
public string RunJob(
    string name,
    IReadOnlyDictionary<string, string>? environment = null)
```

This produces an object whose additional property values must be strings:

```json
{
  "name": "build",
  "environment": {
    "CONFIGURATION": "Release",
    "CI": "true"
  }
}
```

Do not use a dictionary as a substitute for a known object shape. A record gives the model stronger guidance and lets HPD reject misspelled fields.

## Closed Operation Unions

Use a discriminated union when one function performs several mutually exclusive operations. Do not model the operations as one flat list of loosely related optional parameters.

```csharp
using System.Text.Json.Serialization;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "action")]
[JsonDerivedType(typeof(StartJob), "start")]
[JsonDerivedType(typeof(ReadJob), "read")]
[JsonDerivedType(typeof(StopJob), "stop")]
public abstract record JobOperation;

public sealed record StartJob(
    string Command,
    IReadOnlyDictionary<string, string>? Environment = null)
    : JobOperation;

public sealed record ReadJob(string HandleId, int TailLines = 200)
    : JobOperation;

public sealed record StopJob(string HandleId)
    : JobOperation;

public sealed class JobTools
{
    [AIFunction]
    public Task<string> ManageJob(
        JobOperation request,
        CancellationToken cancellationToken = default)
        => request switch
        {
            StartJob start => StartAsync(start, cancellationToken),
            ReadJob read => ReadAsync(read, cancellationToken),
            StopJob stop => StopAsync(stop, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
}
```

The generated schema contains one closed branch per discriminator:

```json
{
  "request": {
    "action": "read",
    "handleId": "job_123",
    "tailLines": 100
  }
}
```

The discriminator is ordinal and case-sensitive. A `read` request containing `command`, or a `start` request containing `handleId`, is rejected before invocation.

Use a union when:

- each operation has different required fields;
- fields are valid only for one operation;
- one flat signature would contain many optional parameters;
- permission or policy analysis depends on the selected operation.

## Runtime-Only Parameters

HPD supplies runtime parameters. They are excluded from the model schema and generated argument carrier:

```csharp
[AIFunction]
public async Task<string> Inspect(
    string path,
    FunctionExecutionContext context,
    CancellationToken cancellationToken = default)
{
    // The model supplies path. HPD supplies context and cancellationToken.
}
```

Supported runtime parameters include:

- `FunctionExecutionContext`
- `CancellationToken`
- `AIFunctionArguments`
- `IServiceProvider`

Use `FunctionExecutionContext` for call metadata, events, services, content storage, background work, and bidirectional requests. Unsupported runtime-like parameters produce generator diagnostic `HPD020`.

## Framework Invocation Mode Is Separate

`AgentInvocationModePolicy.ModelChoice` adds a framework control named `invocationMode`. It decides whether HPD waits for the function result or registers runtime-owned background work.

It is not a normal method parameter, and it is not the same as a domain parameter such as a command's `executionMode`.

```csharp
[AIFunction(InvocationModePolicy = AgentInvocationModePolicy.ModelChoice)]
public Task<string> BuildIndex(string path, CancellationToken cancellationToken)
```

The model may call it with:

```json
{
  "path": "src",
  "invocationMode": "background"
}
```

HPD removes the framework control before generated argument binding.

## Strict Binding Rules

Generated binding intentionally rejects ambiguous input:

- unknown properties;
- duplicate properties when original JSON remains available;
- alternate property casing;
- missing required values;
- `null` for non-nullable values;
- incorrect JSON kinds;
- numeric or incorrectly cased enum values;
- unknown, missing, blank, or duplicate union discriminators;
- properties belonging to another union branch;
- invalid nested collection items.

The validation result identifies the precise property path, such as `request.command` or `replacements[2].newText`.

## Raw And Canonical JSON Ingress

Generated binders consume JSON rather than rebuilding a contract from arbitrary runtime objects.

When a provider integration exposes the original argument document, HPD preserves it. `Microsoft.Extensions.AI.FunctionCallContent` otherwise guarantees parsed arguments, so the agent runtime creates a canonical JSON document with rooted, source-generated serializer metadata before invoking the binder.

Direct programmatic invocation of a generated `HPDAIFunction` must attach JSON through HPD's argument APIs. This strict boundary prevents a reflection-based dictionary fallback from becoming the production contract.

## Unsupported Or Discouraged Shapes

Avoid:

- open-ended `object` parameters;
- `dynamic`;
- dictionaries used for known records;
- recursive model-facing object graphs;
- collection interfaces without deterministic construction semantics;
- types that require runtime reflection or unrooted serializer metadata;
- flat action methods with branch-specific optional fields;
- runtime services presented as model parameters.

If the generator cannot produce a deterministic schema and binder, change the public contract instead of adding a runtime compatibility path.

## Design Checklist

Before publishing a function:

1. Give every parameter and nested property one stable meaning.
2. Use defaults only when omission has an unambiguous behavior.
3. Use nullable types only when `null` is meaningful.
4. Prefer enums over free-form strings for closed choices.
5. Prefer records over dictionaries for known objects.
6. Prefer a discriminated union over branch-specific optional fields.
7. Keep runtime services in sanctioned runtime parameters.
8. Add descriptions where names alone do not guide the model.
9. Inspect the generated schema in a contract test.
10. Test rejection of unknown fields, incorrect enum casing, and wrong union branches.
