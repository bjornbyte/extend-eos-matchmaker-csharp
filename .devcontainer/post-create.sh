#!/bin/bash
set -e

echo "🚀 Setting up development environment..."

# Restore .NET dependencies
echo "📦 Restoring .NET dependencies..."
if [ -f "src/extend-service-extension-server.sln" ]; then
    dotnet restore src/extend-service-extension-server.sln
else
    echo "⚠️  Solution file not found, skipping .NET restore"
fi

# Install Go dependencies
echo "📦 Installing Go dependencies..."
cd gateway
go mod download
cd ..

# Make scripts executable
echo "🔧 Setting up scripts..."
chmod +x proto.sh
chmod +x wrapper.sh

# Generate protobuf files
echo "✏️ Generating protocol buffer files..."
if command -v protoc &> /dev/null; then
    ./proto.sh || echo "⚠️  Protocol buffer generation skipped"
else
    echo "⚠️  protoc not found"
fi

# Configure git for safe directory
if [ -d ".git" ]; then
    echo "🔧 Setting up git..."
    git config --global --add safe.directory /workspace
fi

echo "✅ Development environment setup complete!"
echo ""
echo "🎯 Quick start commands:"
echo "  • Build .NET solution: dotnet build src/extend-service-extension-server.sln"
echo "  • Run .NET service: cd src/AccelByte.Extend.SimpleEOSMatchmaking.Server && dotnet run"
echo "  • Build Go gateway: cd gateway && go build"
echo "  • Generate protobuf: ./proto.sh"
echo ""
echo "🛟 Ports:"
echo "  • gRPC Server: 6565"
echo "  • gRPC Gateway: 8000"
echo "  • Prometheus Metrics: 8080"
