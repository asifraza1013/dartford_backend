#!/bin/bash

# Inflan Backend Deployment Script
# Simple deployment - pull latest code and restart

echo "🚀 Deploying Inflan Backend..."

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./deploy.sh /path/to/inflan-api-key-pair.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
#!/bin/bash

echo "📦 Deploying latest changes..."

# Stop existing process
echo "Stopping current API..."
pkill -f "dotnet run" || true
sleep 5

# Pull latest code
cd /home/ec2-user/dartford_backend
git pull origin master

# Restore and build
echo "Building application..."
dotnet restore
dotnet build -c Release

# Start application
echo "Starting API..."
nohup dotnet run --project /home/ec2-user/dartford_backend --urls="http://0.0.0.0:10000" --environment="Production" > app.log 2>&1 &

# Wait and test
sleep 10
echo ""
if curl -s http://localhost:10000/api/auth/getUser/1 > /dev/null; then
    echo "✅ Deployment successful!"
else
    echo "❌ Deployment failed. Check logs: tail -f app.log"
fi
EOF

echo "✅ Done! API: https://api.inflan.com"