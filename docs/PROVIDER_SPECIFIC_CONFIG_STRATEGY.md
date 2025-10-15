# ADR: Provider-Specific Configuration Strategy

**Date:** October 15, 2025  
**Status:** Approved & Implemented (Phase 1) ✅  
**Decision Maker(s):** Architecture Team  
**Affected Providers:** Bedrock, AzureAIInference, OnnxRuntime, OpenRouter (4 packages)  

## Executive Summary

**Problem:** Provider-specific configuration classes (`ProviderSpecificConfig`, `OpenAISettings`, etc.) were broken by design. The core library defined them but didn't reference provider packages, so `config.ProviderSpecific` was always `null` at runtime. Four providers attempted to use this broken feature.

**Solution:** The `ProviderSpecific` property was replaced with an `AdditionalProperties` dictionary, following Microsoft.Extensions.AI patterns. This immediately fixes the broken configuration path. The next phases involve providing JSON schemas for IntelliSense and optionally adding strong typing later if users request it.

**Impact:** 
- Core: Deleted ~400 lines of broken code and added the flexible `AdditionalProperties` dictionary to `ProviderConfig`.
- Providers: Updated 4 packages (7 code locations) to read from the new dictionary.
- Users: A minor breaking change that fixes a non-functional feature and enables provider-specific configuration.

## Context

After separating providers into independent packages (v2.0 modularization), the provider-specific configuration classes in `AgentConfig.cs` became orphaned. The core `HPD-Agent` project no longer references provider packages, creating a fundamental problem.

**The Problem:**
- `ProviderSpecificConfig` and all 12 provider settings classes were defined in core `AgentConfig.cs`.
- The core library doesn't reference provider packages (by design - dependency inversion).
- Provider packages *did* try to read from `config.ProviderSpecific` (e.g., BedrockProvider, OpenRouterProvider).
- **But `ProviderSpecific` was always `null` at runtime** - there was no way to populate it since the core library didn't know about provider-specific types.
- Result: A **broken feature** - the code existed and executed, but the configuration mechanism was fundamentally non-functional.

**User's Vision:**
A single composable `AgentConfig` class that can be loaded from JSON/YAML and supports all provider-specific features through strong typing and IntelliSense.

**New State (After Phase 1):**
```csharp
public class ProviderConfig
{
    public string ProviderKey { get; set; }
    public string ModelName { get; set; }
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public ChatOptions? DefaultChatOptions { get; set; }
    
    /// <summary>
    /// Provider-specific configuration as key-value pairs.
    /// See provider documentation for available options.
    /// </summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}
```

**Evidence of original broken usage:**
```csharp
// HPD-Agent.Providers.Bedrock/BedrockProvider.cs (Before change)
var bedrockSettings = config.ProviderSpecific?.Bedrock; // ❌ Always null

// HPD-Agent.Providers.OpenRouter/OpenRouterProvider.cs (Before change)
var orSettings = config.ProviderSpecific?.OpenRouter; // ❌ Always null
```

## Decision Drivers

1. **Maintain composable config vision**: Single `AgentConfig` class for all configuration.
2. **Respect dependency inversion**: Core shouldn't depend on provider packages.
3. **Developer experience**: Strong typing, IntelliSense, documentation.
4. **Serialization-friendly**: Work with JSON/YAML config files.
5. **Provider autonomy**: Providers control their own features.
6. **No dead code**: Every line should serve a purpose.

## Options Considered
(Options 1-5 remain for historical context but are omitted here for brevity)

---

## Recommendation: Option 6 (Hybrid Approach)

**This option was approved and Phase 1 has been implemented.**

**Phase 1: Immediate (Clean up broken code)** - ✅ **DONE**
- The broken `ProviderSpecificConfig` and all related settings classes were deleted.
- A `Dictionary<string, object>? AdditionalProperties` was added to `ProviderConfig`.
- The 4 affected provider packages were updated to read from this new dictionary.

**Phase 2: Documentation (Short-term)**
- Document the available `AdditionalProperties` keys for each provider.
- Create example config files for both C# and JSON.

**Phase 3: JSON Schema (Medium-term)**
- Create and host a JSON schema for `AgentConfig` and provider-specific sub-schemas.
- This will enable IntelliSense and validation in JSON files.

**Phase 4: Optional Enhancement (Future)**
- If users request strong typing for C# configuration, create optional helper/extension packages.

### Implementation Plan

**Phase 1: Clean Up (Immediate - 2-3 hours)** - ✅ **COMPLETED**

1.  **Core Library Updates** (`HPD-Agent/Agent/AgentConfig.cs`):
    - [x] Remove `ProviderSpecificConfig` class and all 12 settings classes (~400 lines).
    - [x] Add `AdditionalProperties` dictionary to `ProviderConfig`.
    - [x] Update XML documentation with examples.

2.  **Provider Package Updates** (4 packages fixed):
    - [x] **HPD-Agent.Providers.Bedrock:** Updated `BedrockProvider.cs` to read `Region`, `AccessKeyId`, `SecretAccessKey` from `AdditionalProperties`.
    - [x] **HPD-Agent.Providers.AzureAIInference:** Updated `AzureAIInferenceProvider.cs` to read `Endpoint`, `ApiKey` from `AdditionalProperties`.
    - [x] **HPD-Agent.Providers.OnnxRuntime:** Updated `OnnxRuntimeProvider.cs` to read `ModelPath`, `StopSequences`, etc. from `AdditionalProperties`.
    - [x] **HPD-Agent.Providers.OpenRouter:** Updated `OpenRouterProvider.cs` to read `HttpReferer`, `AppName`, etc. from `AdditionalProperties`.

3.  **Testing:**
    - [x] Tested each of the 4 affected providers with `AdditionalProperties`.
    - [x] Verified AgentConsoleTest still works with updated config.
    - [x] Tested JSON config file deserialization.
    - [x] Ensured build succeeds with 0 errors across all projects.

**Phase 2: Documentation (Next)**
- [ ] Document available options for each provider in their respective `README.md` files.
- [ ] Create example config files in the `/samples` directory.

**Phase 3: JSON Schema (Medium-term)**
- [ ] Create JSON schema for core `AgentConfig`.
- [ ] Create provider-specific sub-schemas.
- [ ] Host schemas on GitHub Pages or a CDN.
- [ ] Update docs with schema URLs.

### Breaking Changes

- `ProviderConfig.ProviderSpecific` property was removed.
- **User-facing impact**: Low. The feature was non-functional before; this change makes it work as intended via `AdditionalProperties`.

### Migration Guide

**For End Users (this feature now works):**
```csharp
// ❌ Before (never worked - ProviderSpecific was always null)
var config = new AgentConfig
{
    Provider = new ProviderConfig
    {
        ProviderKey = "openrouter",
        ProviderSpecific = new() { /* ... */ }
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
            ["AppName"] = "MyAgent"
        }
    }
};
```

**For Provider Package Developers:**
```csharp
// ❌ Before (settings was always null)
var settings = config.ProviderSpecific?.OpenRouter;
var referer = settings?.HttpReferer;

// ✅ After (works!)
config.AdditionalProperties?.TryGetValue("HttpReferer", out var refererObj);
var referer = refererObj?.ToString();
```

## Consequences

### Positive
- ✅ Clean, maintainable codebase.
- ✅ Provider packages remain independent.
- ✅ Flexible for future enhancements.
- ✅ Good developer experience is now possible via JSON schemas.
- ✅ Follows standard industry patterns (`AdditionalProperties`).

### Negative
- ⚠️ No compile-time validation for provider options in C# code (by design for this phase).
- ⚠️ Runtime errors for typos (will be mitigated by JSON schema for config files).
- ⚠️ Requires documentation maintenance.

---

**Next Steps:**
- [x] Implement Phase 1.
- [ ] Begin Phase 2 (Documentation).
- [ ] Create GitHub issues for tracking Phase 2 and 3.
