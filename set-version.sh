#!/bin/bash
#
# HPD-Agent Version Management Script
# Usage: ./set-version.sh <version> [suffix]
#
# Examples:
#   ./set-version.sh 0.1.2
#   ./set-version.sh 0.1.3 alpha
#   ./set-version.sh 1.0.0 preview
#

set -e

if [ -z "$1" ]; then
    echo "Usage: $0 <version> [suffix]"
    echo ""
    echo "Examples:"
    echo "  $0 0.1.2"
    echo "  $0 0.1.3 alpha"
    echo "  $0 1.0.0 preview"
    exit 1
fi

VERSION=$1
SUFFIX=$2

PROPS_FILE="Directory.Build.props"

if [ ! -f "$PROPS_FILE" ]; then
    echo "Error: $PROPS_FILE not found"
    exit 1
fi

echo "=========================================="
echo "HPD-Agent Version Update"
echo "=========================================="
echo "New version: $VERSION"
if [ -n "$SUFFIX" ]; then
    echo "Version suffix: $SUFFIX"
    echo "Full version: $VERSION-$SUFFIX"
else
    echo "No version suffix (stable release)"
fi
echo ""

# Update VersionPrefix
sed -i '' "s/<VersionPrefix>.*<\/VersionPrefix>/<VersionPrefix>$VERSION<\/VersionPrefix>/" "$PROPS_FILE"

# Update or add VersionSuffix
if [ -n "$SUFFIX" ]; then
    # Uncomment and set suffix
    if grep -q "<!-- <VersionSuffix>" "$PROPS_FILE"; then
        # Currently commented, uncomment it
        sed -i '' "s/<!-- <VersionSuffix>.*<\/VersionSuffix> -->/<VersionSuffix>$SUFFIX<\/VersionSuffix>/" "$PROPS_FILE"
    else
        # Already uncommented, just update value
        sed -i '' "s/<VersionSuffix>.*<\/VersionSuffix>/<VersionSuffix>$SUFFIX<\/VersionSuffix>/" "$PROPS_FILE"
    fi
else
    # Comment out suffix for stable release
    if ! grep -q "<!-- <VersionSuffix>" "$PROPS_FILE"; then
        sed -i '' "s/<VersionSuffix>.*<\/VersionSuffix>/<!-- <VersionSuffix><\/VersionSuffix> -->/" "$PROPS_FILE"
    fi
fi

echo "✓ Updated $PROPS_FILE"
echo ""

# Show the current version configuration
echo "Current version configuration:"
grep -A 1 "VersionPrefix" "$PROPS_FILE" | head -2
if [ -n "$SUFFIX" ]; then
    grep "VersionSuffix" "$PROPS_FILE" | head -1
fi

echo ""
echo "=========================================="
echo "All packages will now use this version"
echo "=========================================="
echo ""
echo "Next steps:"
echo "  1. Commit the version change: git add $PROPS_FILE && git commit -m \"Bump version to $VERSION${SUFFIX:+-$SUFFIX}\""
echo "  2. Pack and publish: ./nuget-publish.sh all $VERSION${SUFFIX:+-$SUFFIX} YOUR_API_KEY"
echo ""
