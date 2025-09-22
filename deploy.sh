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
pkill -f dotnet || true
sleep 5

# Force kill if still running
pkill -9 -f dotnet 2>/dev/null || true
sleep 2

# Pull latest code
cd /home/ec2-user/inflat-api-server
git pull origin master

# Restore and build
echo "Building application..."
dotnet restore
dotnet build -c Release

# Start application on port 8080 (the port nginx expects)
echo "Starting API on port 8080..."
nohup dotnet run > app.log 2>&1 &

# Wait for API to fully start
echo "Waiting for API to start (30 seconds)..."
sleep 30

# Check what port API is actually running on
API_PORT=$(tail -10 app.log | grep "Now listening" | grep -o '[0-9]*' | tail -1)
echo "API is listening on port: $API_PORT"

# Test API locally
echo ""
if curl -s http://localhost:8080/api/auth/getUser/1 > /dev/null; then
    echo "✅ API running on port 8080!"
else
    echo "❌ API not responding on port 8080"
fi

# Test domain access
echo "Testing domain access..."
if curl -s https://api.inflan.com/api/auth/getUser/1 > /dev/null; then
    echo "✅ Domain working!"
else
    echo "❌ Domain showing bad gateway - checking nginx..."
    
    # Check nginx error logs
    echo "Recent nginx errors:"
    sudo tail -5 /var/log/nginx/error.log
    
    # Restart nginx if needed
    echo "Restarting nginx..."
    sudo systemctl restart nginx
    sleep 5
    
    # Test again
    if curl -s https://api.inflan.com/api/auth/getUser/1 > /dev/null; then
        echo "✅ Fixed after nginx restart!"
    else
        echo "❌ Still not working. Check logs: tail -f app.log"
    fi
fi

echo ""
echo "Deployment complete. API logs:"
tail -10 app.log
EOF

echo "✅ Done! API: https://api.inflan.com"