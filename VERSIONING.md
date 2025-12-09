# HPD-Agent Centralized Version Management

## Overview

HPD-Agent uses **centralized version management** via `Directory.Build.props`, similar to Microsoft's approach in their agent framework. This ensures all packages are published with the same version number, avoiding dependency conflicts.

## How It Works

### Single Source of Truth

All package versions are defined in [`Directory.Build.props`](Directory.Build.props):

```xml
<PropertyGroup>
  <!-- Centralized version for ALL packages -->
  <VersionPrefix>0.1.2</VersionPrefix>
  <!-- Uncomment to add suffix like "alpha", "beta", "preview" -->
  <!-- <VersionSuffix>preview</VersionSuffix> -->
</PropertyGroup>
```

### Automatic Inheritance

Every `.csproj` file in the repository automatically inherits:
- `Version` - from `VersionPrefix` and optional `VersionSuffix`
- `Authors` - "Einstein Essibu"
- `Company` - "HPD"
- `Copyright` - "Copyright © 2024 Einstein Essibu. All rights reserved."
- `PackageLicenseFile` - LICENSE
- `PackageReadmeFile` - README.md
- Symbol package settings

Individual projects only need to specify:
- `<IsPackable>true</IsPackable>` - to enable packaging
- `<PackageId>` - the NuGet package name
- `<Description>` - package-specific description
- `<PackageTags>` - relevant tags

## Changing the Version

### Method 1: Using the Script (Recommended)

```bash
# Set stable version
./set-version.sh 0.1.3

# Set prerelease version
./set-version.sh 0.1.3 alpha
./set-version.sh 1.0.0 preview
```

### Method 2: Manual Edit

Edit [`Directory.Build.props`](Directory.Build.props) and change `<VersionPrefix>`:

```xml
<VersionPrefix>0.1.3</VersionPrefix>
```

For prerelease, uncomment and set `<VersionSuffix>`:

```xml
<VersionSuffix>alpha</VersionSuffix>
```

## Publishing Workflow

### 1. Update Version

```bash
./set-version.sh 0.1.3
```

### 2. Commit Version Change

```bash
git add Directory.Build.props
git commit -m "Bump version to 0.1.3"
```

### 3. Pack and Publish

```bash
# Pack and publish in one step
./nuget-publish.sh all 0.1.3 YOUR_API_KEY

# Or do separately
./nuget-publish.sh pack 0.1.3
./nuget-publish.sh push 0.1.3 YOUR_API_KEY
```

## Package Dependencies

Provider packages use `<ProjectReference>` to depend on the main framework:

```xml
<ItemGroup>
  <ProjectReference Include="../../HPD-Agent/HPD-Agent.csproj" />
</ItemGroup>
```

When packed, this becomes a NuGet dependency:
- `HPD.Agent.Providers.Anthropic` version `0.1.3` depends on `HPD.Agent.Framework` version `0.1.3`

This is why **all packages must use the same version** - otherwise you get dependency resolution errors.

## Published Packages

All packages share the same version and are published together:

1. **HPD.Agent.Framework** - Main framework (includes embedded source generator)
2. **HPD.Agent.FFI** - Foreign function interface
3. **HPD.Agent.MCP** - Model Context Protocol support
4. **HPD.Agent.Memory** - Memory management
5. **HPD.Agent.TextExtraction** - Text extraction utilities
6. **HPD.Agent.Plugins.FileSystem** - File system operations plugin
7. **HPD.Agent.Plugins.WebSearch** - Web search plugin
8. **HPD.Agent.Providers.Anthropic** - Claude provider
9. **HPD.Agent.Providers.AzureAIInference** - Azure AI provider
10. **HPD.Agent.Providers.Bedrock** - AWS Bedrock provider
11. **HPD.Agent.Providers.GoogleAI** - Google AI provider
12. **HPD.Agent.Providers.HuggingFace** - HuggingFace provider
13. **HPD.Agent.Providers.Mistral** - Mistral provider
14. **HPD.Agent.Providers.Ollama** - Ollama provider
15. **HPD.Agent.Providers.OnnxRuntime** - ONNX Runtime provider
16. **HPD.Agent.Providers.OpenAI** - OpenAI provider
17. **HPD.Agent.Providers.OpenRouter** - OpenRouter provider

**Note**: `HPD.Agent.SourceGenerator` is NOT published separately - it's embedded as an analyzer inside `HPD.Agent.Framework`.

## Benefits

1. **No version conflicts** - All packages compatible by design
2. **Single command updates** - Change one file to update all packages
3. **Reduced maintenance** - Common metadata defined once
4. **Industry standard** - Follows Microsoft's approach
5. **Automated workflow** - Scripts handle the complexity

## Testing Locally

Before publishing, test packages locally:

```bash
# Pack packages
./nuget-publish.sh pack 0.1.3

# In your test project
cd ~/your-test-project
dotnet add package HPD.Agent.Framework --version 0.1.3 --source ~/Documents/HPD-Agent/.nupkg
```

## Migration from Old System

Before centralized versioning, versions were specified in each `.csproj` file, leading to:
- Version mismatches between packages
- Dependency resolution errors
- Manual updates across 17+ files

Now, all versioning is centralized and automated.
