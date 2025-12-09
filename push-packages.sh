#!/bin/bash
#
# HPD-Agent NuGet Publishing Script
# Usage: ./push-packages.sh <version> <api-key>
# Example: ./push-packages.sh 0.1.2 "your-api-key"
#

set -e

if [ -z "$1" ] || [ -z "$2" ]; then
    echo "Usage: $0 <version> <api-key>"
    echo "Example: $0 0.1.2 your-api-key"
    exit 1
fi

VERSION=$1
API_KEY=$2
NUPKG_DIR="./.nupkg"

echo "=========================================="
echo "HPD-Agent NuGet Publishing Script"
echo "=========================================="
echo "Version: $VERSION"
echo "Package directory: $NUPKG_DIR"
echo ""

# Find all nupkg files for this version (excluding snupkg symbol packages)
PACKAGES=$(find "$NUPKG_DIR" -name "*.${VERSION}.nupkg" ! -name "*.snupkg" 2>/dev/null | wc -l)

if [ "$PACKAGES" -eq 0 ]; then
    echo "Error: No packages found for version $VERSION in $NUPKG_DIR"
    exit 1
fi

echo "Found $PACKAGES packages to publish"
echo ""

PUBLISHED=0
FAILED=0

for nupkg in "$NUPKG_DIR"/*.${VERSION}.nupkg; do
    if [ -f "$nupkg" ] && [[ "$nupkg" != *.snupkg ]]; then
        filename=$(basename "$nupkg")
        echo "Publishing $filename..."
        
        if dotnet nuget push "$nupkg" -k "$API_KEY" -s https://api.nuget.org/v3/index.json --skip-duplicate; then
            echo "  ✓ Published"
            ((PUBLISHED++))
        else
            echo "  ✗ Failed"
            ((FAILED++))
        fi
    fi
done

echo ""
echo "=========================================="
echo "✓ Publishing Complete"
echo "=========================================="
echo ""
echo "Results:"
echo "  Successfully published: $PUBLISHED"
echo "  Failed: $FAILED"
echo ""
echo "Verify at:"
echo "  https://www.nuget.org/packages?q=HPD.Agent"
echo ""
