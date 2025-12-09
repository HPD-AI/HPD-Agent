#!/bin/bash
#
# HPD-Agent NuGet Package Deletion Script
# Usage: ./delete-packages.sh <version> [api-key]
# Example: ./delete-packages.sh 0.1.1-alpha
# Or set environment variable: export NUGET_API_KEY="your-key" && ./delete-packages.sh 0.1.1-alpha
#

set -e

if [ -z "$1" ]; then
    echo "Usage: $0 <version> [api-key]"
    echo "Example: $0 0.1.1-alpha"
    echo "Or: export NUGET_API_KEY='your-key' && $0 0.1.1-alpha"
    exit 1
fi

VERSION=$1
API_KEY="${2:-$NUGET_API_KEY}"

if [ -z "$API_KEY" ]; then
    echo "Error: NUGET_API_KEY not provided"
    echo ""
    echo "Options:"
    echo "  1. Pass as argument: ./delete-packages.sh $VERSION your-api-key"
    echo "  2. Set environment: export NUGET_API_KEY='your-key' && ./delete-packages.sh $VERSION"
    echo ""
    echo "To get your API key from GitHub:"
    echo "  - Visit: https://www.nuget.org/account/apikeys"
    echo "  - Or in GitHub: Settings > Secrets and variables > Actions > NUGET_API_KEY"
    exit 1
fi

PACKAGES=(
    "HPD.Agent.Framework"
    "HPD.Agent.TextExtraction"
    "HPD.Agent.Plugins.FileSystem"
    "HPD.Agent.Plugins.WebSearch"
    "HPD-Agent.Providers.Anthropic"
    "HPD-Agent.Providers.AzureAIInference"
    "HPD-Agent.Providers.Bedrock"
    "HPD-Agent.Providers.GoogleAI"
    "HPD-Agent.Providers.HuggingFace"
    "HPD-Agent.Providers.Mistral"
    "HPD-Agent.Providers.Ollama"
    "HPD-Agent.Providers.OnnxRuntime"
    "HPD-Agent.Providers.OpenAI"
    "HPD-Agent.Providers.OpenRouter"
)

echo "=========================================="
echo "HPD-Agent NuGet Package Deletion Script"
echo "=========================================="
echo "Version to delete: $VERSION"
echo "Number of packages: ${#PACKAGES[@]}"
echo ""
echo "Packages to delete:"
printf '  - %s\n' "${PACKAGES[@]}"
echo ""

FAILED=0
DELETED=0

for PACKAGE in "${PACKAGES[@]}"; do
    echo "Deleting $PACKAGE/$VERSION..."
    if dotnet nuget delete "$PACKAGE" "$VERSION" -k "$API_KEY" -s https://api.nuget.org/v3/index.json --non-interactive; then
        echo "  ✓ Deleted"
        ((DELETED++))
    else
        echo "  ✗ Failed (package may not exist or already deleted)"
        ((FAILED++))
    fi
done

echo ""
echo "=========================================="
echo "Results:"
echo "  Successfully deleted: $DELETED"
echo "  Failed: $FAILED"
echo "=========================================="
