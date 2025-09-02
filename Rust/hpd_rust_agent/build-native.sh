#!/bin/bash
# Cross-platform build script for HPD-Agent C# library

set -e

# Default to current platform if no argument provided
PLATFORM=${1:-"auto"}

# Detect current platform if auto
if [ "$PLATFORM" = "auto" ]; then
    case "$(uname -s)" in
        Darwin)
            case "$(uname -m)" in
                arm64) PLATFORM="osx-arm64" ;;
                x86_64) PLATFORM="osx-x64" ;;
                *) echo "Unsupported macOS architecture: $(uname -m)"; exit 1 ;;
            esac
            ;;
        Linux)
            case "$(uname -m)" in
                x86_64) PLATFORM="linux-x64" ;;
                aarch64) PLATFORM="linux-arm64" ;;
                *) echo "Unsupported Linux architecture: $(uname -m)"; exit 1 ;;
            esac
            ;;
        CYGWIN*|MINGW*|MSYS*)
            PLATFORM="win-x64"
            ;;
        *)
            echo "Unsupported operating system: $(uname -s)"
            exit 1
            ;;
    esac
fi

echo "Building HPD-Agent for platform: $PLATFORM"

# Navigate to the C# project directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSHARP_PROJECT_DIR="$SCRIPT_DIR/../../HPD-Agent"

if [ ! -d "$CSHARP_PROJECT_DIR" ]; then
    echo "Error: C# project directory not found at $CSHARP_PROJECT_DIR"
    exit 1
fi

cd "$CSHARP_PROJECT_DIR"

# Build the native library
echo "Publishing C# project for $PLATFORM..."
dotnet publish -r "$PLATFORM" -c Release --self-contained

# Determine the library file name based on platform
case "$PLATFORM" in
    win-*)
        LIB_NAME="HPD-Agent.dll"
        ;;
    osx-*)
        LIB_NAME="HPD-Agent.dylib"
        ;;
    linux-*)
        LIB_NAME="libHPD-Agent.so"
        ;;
    *)
        echo "Unknown platform: $PLATFORM"
        exit 1
        ;;
esac

# Copy the library to the Rust project directory
SOURCE_LIB="bin/Release/net9.0/$PLATFORM/publish/$LIB_NAME"
DEST_LIB="$SCRIPT_DIR/$LIB_NAME"

if [ ! -f "$SOURCE_LIB" ]; then
    echo "Error: Built library not found at $SOURCE_LIB"
    exit 1
fi

echo "Copying $LIB_NAME to Rust project directory..."
cp "$SOURCE_LIB" "$DEST_LIB"

# Fix library install name on macOS
if [[ "$PLATFORM" == osx-* ]]; then
    echo "Fixing install name for macOS..."
    install_name_tool -id "@loader_path/$LIB_NAME" "$DEST_LIB"
fi

# Create symlinks for Unix systems if needed
case "$PLATFORM" in
    osx-*)
        # Create the symlink that Rust expects
        cd "$SCRIPT_DIR"
        ln -sf "$LIB_NAME" "libhpdagent.dylib"
        echo "Created symlink: libhpdagent.dylib -> $LIB_NAME"
        ;;
    linux-*)
        # Linux libraries should already have the lib prefix
        if [ "$LIB_NAME" != "libHPD-Agent.so" ]; then
            cd "$SCRIPT_DIR"
            ln -sf "$LIB_NAME" "libHPD-Agent.so"
            echo "Created symlink: libHPD-Agent.so -> $LIB_NAME"
        fi
        ;;
esac

echo "Build completed successfully!"
echo "Library: $DEST_LIB"
