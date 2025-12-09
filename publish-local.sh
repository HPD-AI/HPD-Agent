#!/bin/bash
#
# HPD-Agent Local NuGet Publishing Script
# Usage: ./publish-local.sh <version> [output-dir]
# Example: ./publish-local.sh 0.1.2 ./nupkg
#

set -e

if [ -z "$1" ]; then
    echo "Usage: $0 <version> [output-dir]"
    echo "Example: $0 0.1.2 ./nupkg"
    exit 1
fi

VERSION=$1
OUTPUT_DIR="${2:-./.nupkg}"

# Create output directory
mkdir -p "$OUTPUT_DIR"

echo "=========================================="
echo "HPD-Agent Local NuGet Publishing"
echo "=========================================="
echo "Version: $VERSION"
echo "Output directory: $OUTPUT_DIR"
echo ""

# Projects to pack
PROJECTS=(
    "HPD-Agent/HPD-Agent.csproj"
    "HPD-Agent.FFI/HPD-Agent.FFI.csproj"
    "HPD-Agent.MCP/HPD-Agent.MCP.csproj"
    "HPD-Agent.Memory/HPD-Agent.Memory.csproj"
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
    "HPD-Agent.SourceGenerator/HPD-Agent.SourceGenerator.csproj"
    "HPD-Agent.TextExtraction/HPD-Agent.TextExtraction.csproj"
)

echo "Building solution..."
dotnet build --configuration Release

echo ""
echo "Packing packages..."
PACKED=0
FAILED=0

for PROJECT in "${PROJECTS[@]}"; do
    if [ -f "$PROJECT" ]; then
        echo "  Packing $PROJECT..."
        if dotnet pack "$PROJECT" -c Release -o "$OUTPUT_DIR" -p:Version="$VERSION" > /dev/null 2>&1; then
            ((PACKED++))
            echo "    ✓ Success"
        else
            ((FAILED++))
            echo "    ✗ Failed"
        fi
    else
        echo "  ⚠ Skipping $PROJECT (not found)"
    fi
done

echo ""
echo "=========================================="
echo "✓ Local NuGet Packages Ready"
echo "=========================================="
echo ""
echo "Results:"
echo "  Packed: $PACKED"
echo "  Failed: $FAILED"
echo ""
echo "Packages location: $OUTPUT_DIR"
echo ""
echo "To test locally, you can:"
echo "  1. Add local source: dotnet nuget add source \"$OUTPUT_DIR\" -n local"
echo "  2. Install from local: dotnet add package <PackageName> -v $VERSION -s local"
echo ""
echo "To test in a project:"
echo "  dotnet add reference <your-project> -v $VERSION -s \"$OUTPUT_DIR\""
echo ""
