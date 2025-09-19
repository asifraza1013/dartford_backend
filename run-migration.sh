#!/bin/bash

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./run-migration.sh /path/to/inflan-api-key-pair.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "=== RUNNING DATABASE MIGRATION ==="

cd /home/ec2-user/dartford_backend

echo "Current migrations:"
dotnet ef migrations list || echo "EF tools not available"

echo ""
echo "Installing EF tools..."
dotnet tool install --global dotnet-ef --version 8.0.0 || true
export PATH="$PATH:/home/ec2-user/.dotnet/tools"

echo ""
echo "Running migration to remove Twitter fields and add YouTube fields..."
dotnet ef database update

echo ""
echo "Migration completed! Checking if API is still running..."
curl -s http://localhost:10000/api/auth/getUser/1 > /dev/null && echo "✅ API is still running" || echo "❌ API may have issues"
EOF