#!/bin/bash
# Standardize PackageIds to use dots instead of hyphens

files=(
  "HPD-Agent.FFI/HPD-Agent.FFI.csproj"
  "HPD-Agent.MCP/HPD-Agent.MCP.csproj"
  "HPD-Agent.Memory/HPD-Agent.Memory.csproj"
  "HPD-Agent.SourceGenerator/HPD-Agent.SourceGenerator.csproj"
  "HPD-Agent.Plugins/HPD-Agent.Plugins.FileSystem/HPD-Agent.Plugins.FileSystem.csproj"
  "HPD-Agent.Plugins/HPD-Agent.Plugins.WebSearch/HPD-Agent.Plugins.WebSearch.csproj"
  "HPD-Agent.Providers/HPD-Agent.Providers.Anthropic/HPD-Agent.Providers.Anthropic.csproj"
  "HPD-Agent.Providers/HPD-Agent.Providers.AzureAIInference/HPD-Agent.Providers.AzureAIInference.csproj"
  "HPD-Agent.Providers/HPD-Agent.Providers.Bedrock/HPD-Agent.Providers.Bedrock.csproj"
  "HPD-Agent.Providers/HPD-Agent.Providers.GoogleAI/HPD-Agent.Providers.GoogleAI.csproj"
  "HPD-Agent.Providers/HPD-Agent.Providers.HuggingFace/HPD-Agent.Providers.HuggingFace.csproj"
  "HPD-Agent.Providers/HPD-Agent.Providers.Mistral/HPD-Agent.Providers.Mistral.csproj"
  "HPD-Agent.Providers/HPD-Agent.Providers.Ollama/HPD-Agent.Providers.Ollama.csproj"
  "HPD-Agent.Providers/HPD-Agent.Providers.OnnxRuntime/HPD-Agent.Providers.OnnxRuntime.csproj"
  "HPD-Agent.Providers/HPD-Agent.Providers.OpenAI/HPD-Agent.Providers.OpenAI.csproj"
  "HPD-Agent.Providers/HPD-Agent.Providers.OpenRouter/HPD-Agent.Providers.OpenRouter.csproj"
)

for file in "${files[@]}"; do
  if [ -f "$file" ]; then
    # Check if file has PackageId
    if grep -q "<PackageId>" "$file"; then
      echo "Updating $file..."
      sed -i '' 's/<PackageId>HPD-Agent\./<PackageId>HPD.Agent./g' "$file"
      sed -i '' 's/<PackageId>HPD-Agent\./<PackageId>HPD.Agent./g' "$file"
    else
      # No PackageId, need to add one based on filename
      filename=$(basename "$file" .csproj)
      packageid=$(echo "$filename" | sed 's/-/./g')
      echo "Adding PackageId to $file: $packageid"
      # Insert after first PropertyGroup
      sed -i '' "/<PropertyGroup>/a\\
    <PackageId>$packageid</PackageId>
" "$file"
    fi
  fi
done

echo "Done!"
