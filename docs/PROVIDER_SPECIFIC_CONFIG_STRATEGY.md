# ADR: Provider-Specific Configuration Strategy

**Date:** October 15, 2025  
**Status:** Approved - Option 6 (Hybrid Approach) ✅  
**Decision Maker(s):** Architecture Team  
**Affected Providers:** Bedrock, AzureAIInference, OnnxRuntime, OpenRouter (4 packages)  

## Executive Summary

**Problem:** Provider-specific configuration classes (`ProviderSpecificConfig`, `OpenAISettings`, etc.) are broken by design. The core library defines them but doesn't reference provider packages, so `config.ProviderSpecific` is always `null` at runtime. Four providers attempt to use this broken feature.

**Solution:** Replace `ProviderSpecific` with `AdditionalProperties` dictionary, following Microsoft.Extensions.AI patterns. Provide JSON schemas for IntelliSense. Optionally add strong typing later if users request it.

**Impact:** 
- Core: Delete ~400 lines of broken code, add flexible `AdditionalProperties`
- Providers: Update 4 packages (7 code locations) to read from dictionary
- Users: Breaking change but fixes non-functional feature
- Timeline: 2-3 hours implementation, 1 day documentation, 3 days schemas

## Context

After separating providers into independent packages (v2.0 modularization), the provider-specific configuration classes in `AgentConfig.cs` have become orphaned. The core `HPD-Agent` project no longer references provider packages, creating a fundamental problem:

**The Problem:**
- `ProviderSpecificConfig` and all 12 provider settings classes (`OpenAISettings`, `AnthropicSettings`, etc.) are defined in core `AgentConfig.cs`
- Core library doesn't reference provider packages (by design - dependency inversion)
- **Provider packages DO try to read from `config.ProviderSpecific`** (e.g., BedrockProvider, OpenRouterProvider, OnnxRuntimeProvider, AzureAIInferenceProvider)
- **But `ProviderSpecific` is always `null` at runtime** - there's no way to populate it since core doesn't know about provider types
- Result: **Broken feature** - code exists and executes, but the configuration mechanism is fundamentally non-functional

**User's Vision:**
A single composable `AgentConfig` class that can be loaded from JSON/YAML and supports all provider-specific features through strong typing and IntelliSense.

**Current State:**
```csharp
public class ProviderConfig
{
    public string ProviderKey { get; set; }
    public string ModelName { get; set; }
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public ChatOptions? DefaultChatOptions { get; set; }
    
    // ⚠️ BROKEN FEATURE - Always null at runtime
    // Provider packages try to read this, but there's no way to populate it
    public ProviderSpecificConfig? ProviderSpecific { get; set; }
}
```

**Evidence of broken usage:**
```csharp
// HPD-Agent.Providers.Bedrock/BedrockProvider.cs
var bedrockSettings = config.ProviderSpecific?.Bedrock; // ❌ Always null

// HPD-Agent.Providers.OpenRouter/OpenRouterProvider.cs  
var orSettings = config.ProviderSpecific?.OpenRouter; // ❌ Always null

// HPD-Agent.Providers.OnnxRuntime/OnnxRuntimeProvider.cs
var onnxSettings = config.ProviderSpecific?.OnnxRuntime; // ❌ Always null

// HPD-Agent.Providers.AzureAIInference/AzureAIInferenceProvider.cs
var azureSettings = config.ProviderSpecific?.AzureAIInference; // ❌ Always null
```

## Decision Drivers

1. **Maintain composable config vision**: Single `AgentConfig` class for all configuration
2. **Respect dependency inversion**: Core shouldn't depend on provider packages
3. **Developer experience**: Strong typing, IntelliSense, documentation
4. **Serialization-friendly**: Work with JSON/YAML config files
5. **Provider autonomy**: Providers control their own features
6. **No dead code**: Every line should serve a purpose

## Options Considered

### Option 1: Remove Provider-Specific Settings Entirely

**Remove all `ProviderSpecificConfig` and settings classes. Use only basic `ProviderConfig`.**

```csharp
public class ProviderConfig
{
    public string ProviderKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public ChatOptions? DefaultChatOptions { get; set; }
    
    // No ProviderSpecific property at all
}
```

**Usage:**
```csharp
var config = new AgentConfig
{
    Provider = new ProviderConfig
    {
        ProviderKey = "openai",
        ModelName = "gpt-4",
        ApiKey = apiKey,
        DefaultChatOptions = new ChatOptions
        {
            Temperature = 0.7,
            // Provider-specific stuff goes in AdditionalProperties
            AdditionalProperties = new()
            {
                ["organization"] = "org-123",
                ["strict_json_schema"] = true
            }
        }
    }
};
```

**Pros:**
- ✅ Clean, no dead code
- ✅ Simplest implementation
- ✅ Follows Microsoft.Extensions.AI patterns (use `ChatOptions.AdditionalProperties`)
- ✅ No dependency issues
- ✅ Provider packages can document their own options

**Cons:**
- ❌ Loss of strong typing for provider-specific features
- ❌ No IntelliSense for provider options
- ❌ Users must read provider docs to know available options
- ❌ Abandons "all-in-one composable config" vision
- ❌ `AdditionalProperties` is `Dictionary<string, object?>` - error-prone

**Impact:**
- Delete ~400 lines of code from `AgentConfig.cs`
- Update documentation to show `AdditionalProperties` pattern
- Breaking change if anyone is using `ProviderSpecific` (unlikely since it never worked)

---

### Option 2: Dynamic Dictionary Approach

**Replace strongly-typed settings with flexible `AdditionalProperties` dictionary in `ProviderConfig`.**

```csharp
public class ProviderConfig
{
    public string ProviderKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public ChatOptions? DefaultChatOptions { get; set; }
    
    /// <summary>
    /// Provider-specific configuration as key-value pairs.
    /// See provider documentation for available options.
    /// 
    /// Examples:
    /// - OpenAI: { "Organization": "org-123", "StrictJsonSchema": true }
    /// - Anthropic: { "PromptCachingType": "AutomaticToolsAndSystem" }
    /// - OpenRouter: { "HttpReferer": "https://myapp.com" }
    /// - Ollama: { "NumCtx": 8192, "KeepAlive": "5m" }
    /// </summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}
```

**Usage:**
```json
{
  "Provider": {
    "ProviderKey": "openai",
    "ModelName": "gpt-4",
    "ApiKey": "sk-...",
    "AdditionalProperties": {
      "Organization": "org-123",
      "StrictJsonSchema": true,
      "ImageDetail": "high"
    }
  }
}
```

**Provider packages read from dictionary:**
```csharp
// In HPD-Agent.Providers.OpenAI
if (config.AdditionalProperties?.TryGetValue("Organization", out var org) == true)
{
    clientOptions.Organization = org.ToString();
}
```

**Pros:**
- ✅ Provider-agnostic (core doesn't need to know about providers)
- ✅ Flexible - supports any provider feature
- ✅ Serialization-friendly (JSON/YAML)
- ✅ No dependency issues
- ✅ Composable config intact
- ✅ Provider packages control their own features

**Cons:**
- ❌ No strong typing
- ❌ No IntelliSense
- ❌ Runtime errors for typos/wrong types
- ❌ Still requires reading provider docs
- ❌ No compile-time validation

**Impact:**
- Delete `ProviderSpecificConfig` and all settings classes (~400 lines)
- Add `AdditionalProperties` to `ProviderConfig`
- **Update 4 provider packages** to read from `AdditionalProperties` instead of `ProviderSpecific`:
  - HPD-Agent.Providers.Bedrock (2 usage sites)
  - HPD-Agent.Providers.AzureAIInference (2 usage sites)
  - HPD-Agent.Providers.OnnxRuntime (2 usage sites)
  - HPD-Agent.Providers.OpenRouter (1 usage site)
- Document available options per provider

---

### Option 3: Keep Settings as Serialization Models with Conversion

**Keep strongly-typed settings classes for documentation/serialization, convert to dictionary at runtime.**

```csharp
public class ProviderConfig
{
    public string ProviderKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public ChatOptions? DefaultChatOptions { get; set; }
    
    /// <summary>
    /// Provider-specific strongly-typed configuration.
    /// These are converted to AdditionalProperties at build time.
    /// </summary>
    public ProviderSpecificConfig? ProviderSpecific { get; set; }
    
    /// <summary>
    /// Raw provider-specific properties (alternative to ProviderSpecific).
    /// If both are set, AdditionalProperties takes precedence.
    /// </summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}
```

**AgentBuilder converts settings to dictionary:**
```csharp
private Dictionary<string, object> ConvertProviderSpecificConfig(ProviderSpecificConfig config, string providerKey)
{
    return providerKey.ToLowerInvariant() switch
    {
        "openai" when config.OpenAI != null => new()
        {
            ["organization"] = config.OpenAI.Organization,
            ["strict_json_schema"] = config.OpenAI.StrictJsonSchema,
            ["image_detail"] = config.OpenAI.ImageDetail,
            // ... etc
        },
        "anthropic" when config.Anthropic != null => new()
        {
            ["prompt_caching_type"] = config.Anthropic.PromptCaching?.Type.ToString(),
            ["max_retries"] = config.Anthropic.MaxRetries,
            // ... etc
        },
        // ... other providers
        _ => new()
    };
}
```

**Usage (strongly-typed):**
```json
{
  "Provider": {
    "ProviderKey": "openai",
    "ModelName": "gpt-4",
    "ApiKey": "sk-...",
    "ProviderSpecific": {
      "OpenAI": {
        "Organization": "org-123",
        "StrictJsonSchema": true,
        "ImageDetail": "high"
      }
    }
  }
}
```

**Usage (dictionary alternative):**
```json
{
  "Provider": {
    "ProviderKey": "openai",
    "ModelName": "gpt-4",
    "ApiKey": "sk-...",
    "AdditionalProperties": {
      "Organization": "org-123",
      "StrictJsonSchema": true
    }
  }
}
```

**Pros:**
- ✅ **Strong typing for JSON config files**
- ✅ **IntelliSense and documentation**
- ✅ Compile-time validation for known providers
- ✅ Maintains composable config vision
- ✅ Backward compatible (if anyone used it)
- ✅ Best developer experience

**Cons:**
- ❌ **Core library has switch statement with all provider names** (coupling)
- ❌ **Must update core when adding new providers** (maintenance burden)
- ❌ Conversion logic adds complexity
- ❌ Settings classes are still "mostly dead code" (only used for deserialization)
- ❌ ~100 lines of conversion logic in `AgentBuilder`

**Impact:**
- Keep all settings classes in `AgentConfig.cs`
- Add `AdditionalProperties` as alternative
- Implement conversion logic in `AgentBuilder.cs`
- Update provider packages to read from converted dictionary
- Document both approaches

---

### Option 4: Provider Extension Packages for Configuration

**Move settings classes to separate config packages that providers can optionally depend on.**

**New structure:**
```
HPD-Agent.Providers.OpenAI.Configuration/
  - OpenAISettings.cs
  - OpenAIConfigurationExtensions.cs

HPD-Agent.Providers.Anthropic.Configuration/
  - AnthropicSettings.cs
  - AnthropicConfigurationExtensions.cs
```

**Core config stays generic:**
```csharp
public class ProviderConfig
{
    public string ProviderKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public ChatOptions? DefaultChatOptions { get; set; }
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}
```

**Users who want strong typing reference config packages:**
```csharp
// Reference: HPD-Agent.Providers.OpenAI.Configuration
var config = new AgentConfig
{
    Provider = new ProviderConfig
    {
        ProviderKey = "openai",
        ModelName = "gpt-4",
        ApiKey = apiKey
    }
};

// Extension method from config package
config.Provider.ConfigureOpenAI(settings =>
{
    settings.Organization = "org-123";
    settings.StrictJsonSchema = true;
});
```

**Or JSON with schema validation:**
```json
{
  "$schema": "https://hpd-agent.dev/schemas/openai-config.json",
  "Provider": {
    "ProviderKey": "openai",
    "ModelName": "gpt-4",
    "ApiKey": "sk-...",
    "AdditionalProperties": {
      "Organization": "org-123",
      "StrictJsonSchema": true
    }
  }
}
```

**Pros:**
- ✅ Strong typing available for those who want it
- ✅ Core stays clean and provider-agnostic
- ✅ Each provider controls its own configuration
- ✅ No coupling between core and providers
- ✅ Optional - users can use dictionary approach instead

**Cons:**
- ❌ **Most complex solution** (12 new packages + maintenance)
- ❌ Users must reference additional packages for strong typing
- ❌ Doesn't work well with JSON config files (no compile-time validation)
- ❌ More NuGet packages to manage
- ❌ Configuration split across multiple packages

**Impact:**
- Create 12 new configuration packages
- Move settings classes to respective packages
- Implement extension methods for configuration
- Document both approaches (dictionary vs. extensions)
- Significant architecture change

---

### Option 5: JSON Schema Documentation Pattern

**Remove settings classes, provide JSON schemas for validation and IntelliSense.**

**Core config (no settings):**
```csharp
public class ProviderConfig
{
    public string ProviderKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public ChatOptions? DefaultChatOptions { get; set; }
    
    /// <summary>
    /// Provider-specific configuration. See JSON schemas at:
    /// https://hpd-agent.dev/schemas/{providerKey}-config.json
    /// </summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}
```

**Provide JSON Schema files:**
```json
// openai-config.schema.json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "Organization": {
      "type": "string",
      "description": "Organization ID for OpenAI API requests"
    },
    "StrictJsonSchema": {
      "type": "boolean",
      "description": "Enable strict JSON schema validation"
    },
    "ImageDetail": {
      "type": "string",
      "enum": ["low", "high", "auto"],
      "description": "Image detail level for vision models"
    }
  }
}
```

**VSCode/JetBrains gets IntelliSense:**
```json
{
  "$schema": "https://hpd-agent.dev/schemas/agent-config.json",
  "Provider": {
    "ProviderKey": "openai",
    "ModelName": "gpt-4",
    "ApiKey": "sk-...",
    "AdditionalProperties": {
      "Organization": "org-123",  // ✅ Autocomplete from schema
      "StrictJsonSchema": true     // ✅ Validation from schema
    }
  }
}
```

**Pros:**
- ✅ Clean core code (no settings classes)
- ✅ IntelliSense in JSON/YAML editors
- ✅ Validation at config file level
- ✅ Provider-agnostic core
- ✅ Each provider can update its own schema
- ✅ Industry-standard approach

**Cons:**
- ❌ No compile-time validation in C# code
- ❌ Requires maintaining JSON schema files
- ❌ Only helps with config files (not programmatic usage)
- ❌ Runtime errors for typos in C# code

**Impact:**
- Delete all settings classes from `AgentConfig.cs`
- Create JSON schema files for each provider
- Host schemas on GitHub or CDN
- Update documentation
- Provider packages document their schemas

---

### Option 6: Hybrid Approach (Recommended)

**Combine JSON Schema (Option 5) + Optional Strong Typing (Option 3 lite).**

**Phase 1: Immediate (Clean up dead code)**
```csharp
public class ProviderConfig
{
    public string ProviderKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public ChatOptions? DefaultChatOptions { get; set; }
    
    /// <summary>
    /// Provider-specific configuration as key-value pairs.
    /// See https://hpd-agent.dev/providers/{providerKey}#configuration
    /// for available options and JSON schemas.
    /// </summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}
```

**Phase 2: Future (If demand exists)**
- Keep settings classes in **documentation** (separate markdown/examples)
- Provide JSON schemas for each provider
- If users really want strong typing, create optional config packages later

**Usage patterns:**

**Pattern A: JSON config with schema validation (Recommended)**
```json
{
  "$schema": "https://hpd-agent.dev/schemas/agent-config.json",
  "Provider": {
    "ProviderKey": "openai",
    "AdditionalProperties": {
      "Organization": "org-123"  // Schema provides IntelliSense
    }
  }
}
```

**Pattern B: Programmatic with helper methods**
```csharp
var config = new AgentConfig
{
    Provider = ProviderConfig.ForOpenAI(
        modelName: "gpt-4",
        apiKey: apiKey,
        additionalOptions: new()
        {
            ["Organization"] = "org-123",
            ["StrictJsonSchema"] = true
        })
};
```

**Pattern C: Extension methods (future, if needed)**
```csharp
// If HPD-Agent.Providers.OpenAI.Configuration package is referenced
config.Provider.ConfigureOpenAI(s => s.Organization = "org-123");
```

**Pros:**
- ✅ **Clean immediate solution** (remove dead code)
- ✅ **Flexible for future needs** (can add typing later)
- ✅ **No premature optimization**
- ✅ JSON schema provides good DX for config files
- ✅ Helper methods can provide some type safety
- ✅ Keeps core provider-agnostic

**Cons:**
- ❌ No immediate strong typing for C# code
- ❌ Requires documenting provider options
- ⚠️ Deferred decision on strong typing

**Impact:**
- **Immediate**: Delete `ProviderSpecificConfig` and settings classes
- **Short-term**: Create JSON schemas and documentation
- **Long-term**: Add optional config packages if requested

---

## Decision Matrix

| Criteria | Option 1<br>Remove All | Option 2<br>Dictionary | Option 3<br>Convert | Option 4<br>Extension | Option 5<br>Schema | Option 6<br>Hybrid |
|----------|------------|-----------|----------|----------|---------|---------|
| **Composable Config** | ❌ | ✅ | ✅ | ⚠️ | ✅ | ✅ |
| **No Dead Code** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Strong Typing** | ❌ | ❌ | ✅ | ✅ | ⚠️ | ⚠️ |
| **IntelliSense** | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| **Provider Agnostic** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Simplicity** | ✅✅ | ✅ | ❌ | ❌❌ | ✅ | ✅ |
| **Maintenance Burden** | ✅✅ | ✅ | ❌ | ❌❌ | ⚠️ | ✅ |
| **FFI-Friendly** | ✅ | ✅ | ⚠️ | ❌ | ✅ | ✅ |
| **Implementation Cost** | Low | Low | Medium | High | Medium | Low→Medium |

## Recommendation

**Choose Option 6: Hybrid Approach**

### Rationale

1. **Immediate pragmatism**: Remove dead code now, don't carry technical debt
2. **Flexibility**: Can add strong typing later if users demand it
3. **JSON Schema is proven**: Industry standard for config file IntelliSense (ESLint, TypeScript, Kubernetes all use this)
4. **Provider autonomy**: Each provider documents its own options
5. **No premature complexity**: Don't build features nobody asked for yet
6. **Composable vision intact**: Single config class with flexible properties

### Implementation Plan

**Phase 1: Clean Up (Immediate - 2-3 hours)**

1. **Core Library Updates** (`HPD-Agent/Agent/AgentConfig.cs`):
   - [ ] Remove `ProviderSpecificConfig` class and all 12 settings classes (~400 lines)
   - [ ] Add `AdditionalProperties` dictionary to `ProviderConfig`
   - [ ] Update XML documentation with examples
   - [ ] Test that `AgentConfig` serialization/deserialization still works

2. **Provider Package Updates** (4 packages to fix):
   
   **HPD-Agent.Providers.Bedrock:**
   - [ ] Update `BedrockProvider.cs` line 18: Read `Region`, `AccessKeyId`, `SecretAccessKey` from `AdditionalProperties`
   - [ ] Update `BedrockProvider.cs` line 74: Same for validation method
   
   **HPD-Agent.Providers.AzureAIInference:**
   - [ ] Update `AzureAIInferenceProvider.cs` line 18: Read `Endpoint`, `ApiKey` from `AdditionalProperties`
   - [ ] Update `AzureAIInferenceProvider.cs` line 61: Same for validation method
   
   **HPD-Agent.Providers.OnnxRuntime:**
   - [ ] Update `OnnxRuntimeProvider.cs` line 17: Read `ModelPath`, `StopSequences`, etc. from `AdditionalProperties`
   - [ ] Update `OnnxRuntimeProvider.cs` line 57: Same for validation method
   
   **HPD-Agent.Providers.OpenRouter:**
   - [ ] Update `OpenRouterProvider.cs` line 19: Read `HttpReferer`, `AppName`, `PreferredProvider` from `AdditionalProperties`

3. **Testing:**
   - [ ] Test each of the 4 affected providers with `AdditionalProperties`
   - [ ] Verify AgentConsoleTest still works with updated config
   - [ ] Test JSON config file deserialization
   - [ ] Ensure build succeeds with 0 errors across all projects

**Phase 2: Documentation (Short-term - 1 day)**
1. Delete `ProviderSpecificConfig` and all 12 settings classes from `AgentConfig.cs`
2. Add `AdditionalProperties` dictionary to `ProviderConfig`
3. Update documentation examples
4. Test that basic provider creation still works

**Phase 2: Documentation (Short-term - 1 day)**
1. Document available options for each provider
2. Create example config files
3. Add inline examples to `AdditionalProperties` XML docs

**Phase 3: JSON Schema (Medium-term - 3 days)**
1. Create JSON schema for core `AgentConfig`
2. Create provider-specific sub-schemas
3. Host on GitHub Pages or CDN
4. Update docs with schema URLs

**Phase 4: Optional Enhancement (Future - as needed)**
1. If users request strong typing, create config extension packages
2. Monitor feedback and usage patterns
3. Add helper methods if common patterns emerge

### Breaking Changes

- `ProviderConfig.ProviderSpecific` property removed
- **4 provider packages require updates** (feature was broken but code attempted to use it):
  - `HPD-Agent.Providers.Bedrock` - Reads `ProviderSpecific?.Bedrock` (2 locations)
  - `HPD-Agent.Providers.AzureAIInference` - Reads `ProviderSpecific?.AzureAIInference` (2 locations)
  - `HPD-Agent.Providers.OnnxRuntime` - Reads `ProviderSpecific?.OnnxRuntime` (2 locations)
  - `HPD-Agent.Providers.OpenRouter` - Reads `ProviderSpecific?.OpenRouter` (1 location)
- **User-facing impact**: Low - feature never worked due to `ProviderSpecific` always being `null`

### Migration Guide

**For Users (didn't work before, now it will):**
```csharp
// ❌ Before (never worked - always null)
var config = new AgentConfig
{
    Provider = new ProviderConfig
    {
        ProviderKey = "openrouter",
        ProviderSpecific = new() 
        { 
            OpenRouter = new() 
            { 
                HttpReferer = "https://myapp.com",
                PreferredProvider = "openai"
            } 
        }
    }
};

// ✅ After (works!)
var config = new AgentConfig
{
    Provider = new ProviderConfig
    {
        ProviderKey = "openrouter",
        AdditionalProperties = new()
        {
            ["HttpReferer"] = "https://myapp.com",
            ["PreferredProvider"] = "openai"
        }
    }
};
```

**For Provider Package Developers:**
```csharp
// ❌ Before (always null)
var settings = config.ProviderSpecific?.OpenRouter;
if (settings != null)
{
    httpClient.DefaultRequestHeaders.Add("HTTP-Referer", settings.HttpReferer);
}

// ✅ After (works!)
if (config.AdditionalProperties?.TryGetValue("HttpReferer", out var referer) == true)
{
    httpClient.DefaultRequestHeaders.Add("HTTP-Referer", referer?.ToString());
}

// Or use a helper method:
var httpReferer = config.AdditionalProperties?.GetValueOrDefault("HttpReferer")?.ToString();
if (!string.IsNullOrEmpty(httpReferer))
{
    httpClient.DefaultRequestHeaders.Add("HTTP-Referer", httpReferer);
}
```

## Consequences

### Positive
- ✅ Clean, maintainable codebase
- ✅ Provider packages stay independent
- ✅ Flexible for future enhancements
- ✅ Good developer experience via JSON schemas
- ✅ Follows Microsoft.Extensions.AI patterns

### Negative
- ⚠️ No compile-time validation for provider options in C# code
- ⚠️ Runtime errors for typos (mitigated by JSON schema for config files)
- ⚠️ Requires documentation maintenance

### Neutral
- 🔄 Can revisit strong typing decision based on user feedback
- 🔄 Incremental improvement path available

## Follow-up Actions

1. **Immediate**: Delete dead code, implement `AdditionalProperties`
2. **Week 1**: Create provider documentation with available options
3. **Week 2**: Create and publish JSON schemas
4. **Ongoing**: Monitor user feedback for strong typing demand

## References

- Microsoft.Extensions.AI: `ChatOptions.AdditionalProperties` pattern
- JSON Schema: https://json-schema.org/
- Similar patterns: ESLint, TypeScript, Kubernetes configs
- Discussion: GitHub Issue #[TBD]

---

**Next Steps:**
- [ ] Get stakeholder approval for Option 6
- [ ] Create GitHub issue for tracking
- [ ] Implement Phase 1 (delete dead code)
- [ ] Update FFI_PROJECT_SEPARATION.md if needed
