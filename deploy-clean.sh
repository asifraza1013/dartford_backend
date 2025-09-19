#!/bin/bash

# Inflan Backend Clean Deployment Script
# Force clean build and restart

echo "🚀 Deploying Inflan Backend (Clean Build)..."

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./deploy-clean.sh /path/to/inflan-api-key-pair.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
#!/bin/bash

echo "📦 Deploying latest changes with clean build..."

# Stop existing process
echo "Stopping current API..."
pkill -f "dotnet" || true
sleep 5

# Pull latest code
cd /home/ec2-user/dartford_backend
echo "Current branch and status:"
git branch
git status

echo "Fetching latest code..."
git fetch origin
git reset --hard origin/master

# Clean build directories
echo "Cleaning build artifacts..."
rm -rf bin obj

# Restore and build
echo "Building application..."
dotnet restore
dotnet build -c Release

# Show what model we have
echo "Checking if YouTube field exists in code..."
grep -n "YouTube" Models/Influencer.cs || echo "YouTube field not found!"

# Start application from Release build
echo "Starting API from Release build..."
cd /home/ec2-user/dartford_backend
nohup dotnet bin/Release/net8.0/inflan_api.dll --urls="http://0.0.0.0:10000" > app.log 2>&1 &

# Wait and test
sleep 10
echo ""
if curl -s http://localhost:10000/api/auth/getUser/1 > /dev/null; then
    echo "✅ Deployment successful!"
    echo "Recent logs:"
    tail -20 app.log
else
    echo "❌ Deployment failed. Check logs:"
    tail -50 app.log
fi
EOF

echo "✅ Done! API: https://api.inflan.com"