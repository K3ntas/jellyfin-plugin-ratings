#!/bin/bash
# Build script for Jellyfin Ratings Plugin
# Shell script for Linux/macOS

CONFIGURATION="${1:-Release}"

echo "Building Jellyfin Ratings Plugin..."

# Clean previous builds
if [ -d "bin" ]; then
    echo "Cleaning previous builds..."
    rm -rf bin
fi

if [ -d "obj" ]; then
    rm -rf obj
fi

# Build the project
echo "Building project with configuration: $CONFIGURATION"
dotnet build --configuration "$CONFIGURATION"

if [ $? -eq 0 ]; then
    echo ""
    echo "Build successful!"
    echo "Output: bin/$CONFIGURATION/net8.0/Jellyfin.Plugin.Ratings.dll"
    echo ""
    echo "To install the plugin:"
    echo "1. Copy the DLL to your Jellyfin plugins folder:"
    echo "   Linux: /var/lib/jellyfin/plugins/Ratings_1.0.0.0/"
    echo "   Docker: /config/plugins/Ratings_1.0.0.0/"
    echo "2. Restart Jellyfin"
else
    echo ""
    echo "Build failed!"
    exit 1
fi
