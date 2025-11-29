# Container Parameter Validation Architecture

## Overview

This document describes the container-specific parameter validation mechanism that prevents LLMs from incorrectly invoking plugin containers with parameters. Plugin containers must be called with no arguments to expand, then individual functions can be called.

## Problem Statement

When plugin collapsing is enabled, the LLM sees container function descriptions that mention individual function names (e.g., "Math plugin with Add, Multiply, Divide..."). The LLM may incorrectly try to invoke the container with parameters like:

```json
{
  "name": "Math",
  "arguments": {
    "function": "Add",
    "a": 5,
    "b": 10
  }
}
```

This is incorrect because containers must be expanded first with no arguments.

## Solution Architecture

```mermaid
graph TD
    A[LLM calls function] --> B{Is function invocation?}
    B -->|No| Z[Process normally]
    B -->|Yes| C[InvokeCoreAsync]

    C --> D[Get JSON arguments]
    D --> E{Check: IsContainer metadata?}

    E -->|No| K[Continue to regular validation]
    E -->|Yes| F{Check: Has parameters?}

    F -->|No parameters| G[Allow invocation - Expand container]
    F -->|Has parameters| H[Extract metadata]

    H --> I[Build ContainerInvocationErrorResponse]
    I --> J[Return structured error to LLM]

    K --> L{Has validation errors?}
    L -->|Yes| M[Return ValidationErrorResponse]
    L -->|No| N[Invoke function normally]

    G --> O[Container expansion succeeds]
    O --> P[Individual functions now visible]

    J --> Q[LLM reads error]
    Q --> R[LLM retries correctly]
    R --> S[Call container with no args]
    S --> P
```

## Validation Flow Detail

```mermaid
sequenceDiagram
    participant LLM
    participant Agent
    participant HPDAIFunction
    participant Container

    Note over LLM: Sees: Math (description mentions Add, Multiply...)

    LLM->>Agent: Call Math({function: "Add", a: 5, b: 10})
    Agent->>HPDAIFunction: InvokeCoreAsync(arguments)

    HPDAIFunction->>HPDAIFunction: 1. Get JSON arguments
    HPDAIFunction->>HPDAIFunction: 2. Check IsContainer metadata

    alt IsContainer = true
        HPDAIFunction->>HPDAIFunction: 3. Check if parameters present

        alt Has parameters
            HPDAIFunction->>HPDAIFunction: Extract FunctionNames metadata
            HPDAIFunction->>HPDAIFunction: Build error response
            HPDAIFunction-->>Agent: ContainerInvocationErrorResponse
            Agent-->>LLM: Error with retry guidance

            Note over LLM: Reads: "TWO separate tool calls"<br/>"(1) Call 'Math' with NO arguments"<br/>"(2) Call individual function"

            LLM->>Agent: Call Math() [no parameters]
            Agent->>HPDAIFunction: InvokeCoreAsync({})
            HPDAIFunction->>HPDAIFunction: No parameters ✓
            HPDAIFunction->>Container: Expand container
            Container-->>HPDAIFunction: Success message
            HPDAIFunction-->>Agent: "Math expanded. Functions: Add, Multiply..."
            Agent-->>LLM: Success + Individual functions now visible

            LLM->>Agent: Call Add(5, 10)
            Agent->>Agent: Execute Add function
            Agent-->>LLM: Result: 15
        else No parameters
            HPDAIFunction->>Container: Execute expansion
            Container-->>HPDAIFunction: Success
            HPDAIFunction-->>Agent: Expansion result
        end
    else IsContainer = false
        HPDAIFunction->>HPDAIFunction: Regular validation
        HPDAIFunction->>Container: Execute function
    end
```

## Component Architecture

```mermaid
classDiagram
    class HPDAIFunction {
        -HPDAIFunctionFactoryOptions HPDOptions
        -Func invocationHandler
        +InvokeCoreAsync(arguments, ct) ValueTask~object~
    }

    class HPDAIFunctionFactoryOptions {
        +string Name
        +string Description
        +Dictionary~string,object~ AdditionalProperties
        +Func~JsonElement,List~ValidationError~~ Validator
        +Func~JsonElement~ SchemaProvider
    }

    class ContainerInvocationErrorResponse {
        +string ErrorType = "container_invocation_error"
        +string ContainerName
        +JsonElement AttemptedParameters
        +string[] AvailableFunctions
        +string ErrorMessage
        +string RetryGuidance
    }

    class ValidationErrorResponse {
        +string ErrorType = "validation_error"
        +List~ValidationError~ Errors
        +string RetryGuidance
    }

    HPDAIFunction --> HPDAIFunctionFactoryOptions
    HPDAIFunction ..> ContainerInvocationErrorResponse : creates
    HPDAIFunction ..> ValidationErrorResponse : creates
```

## Metadata Structure

```mermaid
graph LR
    A[Container Function] --> B[AdditionalProperties]

    B --> C[IsContainer: true]
    B --> D[PluginName: 'Math']
    B --> E[FunctionNames: string[]]
    B --> F[FunctionCount: int]
    B --> G[SourceType: MCP/Frontend/Plugin]

    E --> H[Add]
    E --> I[Multiply]
    E --> J[Divide]
    E --> K[...]

    style A fill:#f9f,stroke:#333,stroke-width:2px
    style C fill:#9f9,stroke:#333,stroke-width:2px
```

## Validation Decision Tree

```mermaid
graph TD
    A[Function Called] --> B{IsContainer?}

    B -->|No| C[Skip container validation]
    B -->|Yes| D{jsonArgs has properties?}

    D -->|No properties| E[Allow - expand container]
    D -->|Has properties| F{FunctionNames available?}

    F -->|Yes| G[Build error with function list]
    F -->|No| H[Build generic error]

    G --> I[Show first 5 functions + '...']
    H --> J[Generic retry guidance]

    I --> K[Return ContainerInvocationErrorResponse]
    J --> K

    C --> L[Continue to regular validation]
    E --> M[Execute container expansion]

    style B fill:#ff9,stroke:#333,stroke-width:2px
    style D fill:#ff9,stroke:#333,stroke-width:2px
    style K fill:#f99,stroke:#333,stroke-width:2px
    style M fill:#9f9,stroke:#333,stroke-width:2px
```

## Error Response Examples

### Container with Parameters Error

```json
{
  "error_type": "container_invocation_error",
  "container_name": "Math",
  "attempted_parameters": {
    "function": "Add",
    "a": 5,
    "b": 10
  },
  "available_functions": ["Add", "Multiply", "Abs", "Square", "Subtract", "Min", "SolveQuadratic"],
  "error_message": "'Math' is a container function that groups related functions. It cannot be called with parameters.",
  "retry_guidance": "This requires TWO separate tool calls: (1) First call 'Math' with NO arguments to expand it. (2) After expansion succeeds, call the individual function you need. Available functions: Add, Multiply, Abs, Square, Subtract, ..."
}
```

### Regular Validation Error (for comparison)

```json
{
  "error_type": "validation_error",
  "errors": [
    {
      "property": "file_path",
      "attempted_value": null,
      "error_message": "file_path is required",
      "error_code": "REQUIRED_FIELD"
    }
  ],
  "retry_guidance": "The provided arguments are invalid. Please review the errors, correct the arguments based on the function schema, and try again."
}
```

## Integration Points

```mermaid
graph TD
    A[Container Types] --> B[Source-Generated Plugins]
    A --> C[MCP Server Tools]
    A --> D[Frontend Tools]
    A --> E[Skill Containers]

    B --> F[HPDPluginSourceGenerator]
    C --> G[ExternalToolScopingWrapper.WrapMCPServerTools]
    D --> H[ExternalToolScopingWrapper.WrapFrontendTools]
    E --> I[SkillCodeGenerator]

    F --> J[HPDAIFunctionFactory.Create]
    G --> J
    H --> J
    I --> J

    J --> K[HPDAIFunction with metadata]
    K --> L[IsContainer=true]
    K --> M[FunctionNames array]
    K --> N[PluginName]

    style J fill:#9ff,stroke:#333,stroke-width:2px
    style L fill:#9f9,stroke:#333,stroke-width:2px
```

## Code Locations

| Component | File | Lines |
|-----------|------|-------|
| Container Validation Logic | `HPD-Agent/Plugins/HPD-AIFunctionFactory.cs` | 85-120 |
| Error Response Class | `HPD-Agent/Plugins/HPD-AIFunctionFactory.cs` | 188-211 |
| JSON Serialization | `HPD-Agent/AOT/HPDContext.cs` | 19 |
| Test Suite | `test/HPD-Agent.Tests/Validation/ContainerValidationTests.cs` | Full file |
| MCP Container Creation | `HPD-Agent/Plugins/ExternalToolScopingWrapper.cs` | 48-95 |
| Frontend Container Creation | `HPD-Agent/Plugins/ExternalToolScopingWrapper.cs` | 98-178 |

## Validation Algorithm

```mermaid
flowchart TD
    Start([InvokeCoreAsync called]) --> GetJSON[Get JSON arguments from AIFunctionArguments]

    GetJSON --> CheckMeta{Check AdditionalProperties<br/>for IsContainer}

    CheckMeta -->|IsContainer != true| RegularPath[Continue to regular validation]
    CheckMeta -->|IsContainer == true| CheckParams{Enumerate JSON object<br/>Any properties?}

    CheckParams -->|No properties| AllowExpand[Allow container expansion]
    CheckParams -->|Has properties| ExtractMeta[Extract metadata:<br/>- PluginName<br/>- FunctionNames]

    ExtractMeta --> BuildError[Build ContainerInvocationErrorResponse]

    BuildError --> CheckFuncNames{FunctionNames available?}
    CheckFuncNames -->|Yes| DetailedGuidance[Retry guidance with<br/>first 5 function names]
    CheckFuncNames -->|No| GenericGuidance[Generic retry guidance]

    DetailedGuidance --> ReturnError[Return error to LLM]
    GenericGuidance --> ReturnError

    AllowExpand --> Invoke[Invoke container expansion]
    Invoke --> Success([Return expansion result])

    RegularPath --> Validate{Custom validator<br/>returns errors?}
    Validate -->|Yes| ValError[Return ValidationErrorResponse]
    Validate -->|No| InvokeFunc[Invoke function normally]

    InvokeFunc --> Success
    ValError --> End([Return to Agent])
    ReturnError --> End

    style Start fill:#9f9,stroke:#333,stroke-width:2px
    style Success fill:#9f9,stroke:#333,stroke-width:2px
    style ReturnError fill:#f99,stroke:#333,stroke-width:2px
    style CheckMeta fill:#ff9,stroke:#333,stroke-width:2px
    style CheckParams fill:#ff9,stroke:#333,stroke-width:2px
```

## Testing Coverage

```mermaid
graph TD
    A[Test Suite: 12 Tests] --> B[Container Detection]
    A --> C[Different Container Types]
    A --> D[Error Message Guidance]
    A --> E[Regular Functions]

    B --> B1[Container with parameters → error]
    B --> B2[Container without parameters → success]
    B --> B3[Empty JSON object handling]
    B --> B4[Nested parameters detection]

    C --> C1[Source-generated plugin containers]
    C --> C2[MCP server containers]
    C --> C3[Frontend tools containers]
    C --> C4[Skill containers]

    D --> D1[First 5 function names shown]
    D --> D2[Generic guidance when no names]
    D --> D3[Attempted parameters captured]

    E --> E1[Regular functions work normally]
    E --> E2[Functions without metadata unaffected]

    style A fill:#9ff,stroke:#333,stroke-width:3px
```

## Performance Characteristics

```mermaid
graph LR
    A[Container Validation] --> B[Metadata Check]
    B --> C[O 1 - Dictionary lookup]

    A --> D[Parameter Check]
    D --> E[O 1 - JsonElement.EnumerateObject.Any]

    A --> F[Metadata Extraction]
    F --> G[O 1 - Dictionary lookups]

    A --> H[Error Building]
    H --> I[O min n,5 - Take first 5 names]

    style A fill:#9f9,stroke:#333,stroke-width:2px
    style C fill:#9ff
    style E fill:#9ff
    style G fill:#9ff
    style I fill:#9ff
```

## LLM Interaction Flow

```mermaid
sequenceDiagram
    autonumber

    participant U as User
    participant L as LLM
    participant A as Agent
    participant V as Validator
    participant C as Container

    U->>L: "Add 5 and 10"

    Note over L: Sees Math container<br/>description mentions "Add"

    L->>A: Math({function:"Add", a:5, b:10})
    A->>V: Validate function call
    V->>V: IsContainer? YES<br/>Has params? YES
    V-->>A: ContainerInvocationErrorResponse
    A-->>L: Error + Retry guidance

    Note over L: Reads: TWO separate calls<br/>(1) Math() with NO args<br/>(2) Then call Add

    L->>A: Math() [no params]
    A->>C: Expand container
    C-->>A: Success + Available functions
    A-->>L: Expansion successful

    Note over L: Now sees Add, Multiply, etc.<br/>as separate functions

    L->>A: Add(5, 10)
    A->>A: Execute Add
    A-->>L: Result: 15
    L-->>U: "The result is 15"
```

## Future Enhancements

1. **Dot Notation Detection**: Detect patterns like `Math.Add` and provide specific guidance
2. **Smart Function Matching**: Fuzzy matching to suggest correct function names
3. **Container Auto-Expansion**: Option to auto-expand on first invalid attempt
4. **Telemetry**: Track how often validation catches errors for analytics

## Related Documentation

- [Plugin Collapsing Implementation Notes](plugin-scoping-implementation-notes.md)
- [Plugin User Guide](plugins/USER_GUIDE.md)
- [Middleware Architecture](middleware/README.md)
- [Error Handling](ERROR_HANDLING_MIDDLEWARE.md)
