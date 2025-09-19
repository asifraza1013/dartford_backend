#!/bin/bash

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./fix-deployment.sh /path/to/inflan-api-key-pair.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "=== FIXING DEPLOYMENT ISSUES ==="

# Stop current processes
echo "Stopping all dotnet processes..."
pkill -f "dotnet" || true
sleep 5

cd /home/ec2-user/dartford_backend

echo "Current appsettings files:"
ls -la appsettings*

echo "Checking Production appsettings:"
if [ -f "appsettings.Production.json" ]; then
    echo "Production config exists:"
    cat appsettings.Production.json | head -10
else
    echo "No Production config found - using Development"
fi

echo ""
echo "Starting API in Development mode (not Production)..."
nohup dotnet run --urls="http://0.0.0.0:10000" > app.log 2>&1 &

sleep 15

echo "Checking if API started successfully:"
if curl -s http://localhost:10000/api/auth/getUser/1 > /dev/null; then
    echo "✅ API is running!"
    echo "Testing Swagger endpoint:"
    curl -s http://localhost:10000/swagger/v1/swagger.json | grep -A 5 -B 5 "youTube" || echo "YouTube field not found in Swagger"
else
    echo "❌ API failed to start. Recent logs:"
    tail -30 app.log
fi
EOF