#!/bin/bash

# Inflan Backend Deployment Script - Fixed Version

echo "🚀 Deploying Inflan Backend..."

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./deploy-fixed.sh /path/to/inflan-api-key-pair.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
#!/bin/bash

echo "📦 Deploying latest changes..."

# Stop all dotnet processes completely
echo "1. Stopping all dotnet processes..."
sudo pkill -f dotnet || true
sleep 5
sudo pkill -9 -f dotnet 2>/dev/null || true
sleep 2

echo "2. Verifying processes are stopped..."
ps aux | grep dotnet | grep -v grep || echo "✅ No dotnet processes running"

# Pull latest code
echo "3. Pulling latest code..."
cd /home/ec2-user/inflat-api-server
git pull origin master

# Build
echo "4. Building application..."
dotnet build -c Release

# Start API in background (let it choose its default port)
echo "5. Starting API..."
cd /home/ec2-user/inflat-api-server
nohup dotnet run > app.log 2>&1 &
API_PID=$!
echo "API started with PID: $API_PID"

# Wait for startup
echo "6. Waiting for API to start (checking every 5 seconds)..."
for i in {1..12}; do
    sleep 5
    if tail -5 app.log | grep -q "Now listening on"; then
        echo "✅ API started!"
        break
    fi
    echo "Still waiting... ($((i*5))s)"
done

# Get the actual port
API_PORT=$(tail -20 app.log | grep "Now listening on" | grep -o '[0-9]*' | tail -1)
echo "7. API is running on port: $API_PORT"

# Test local connection
echo "8. Testing local connection..."
if curl -s "http://localhost:${API_PORT}/api/auth/getUser/1" > /dev/null; then
    echo "✅ API responding locally on port $API_PORT"
else
    echo "❌ API not responding locally"
    echo "Recent logs:"
    tail -20 app.log
fi

# Fix nginx if port mismatch
echo "9. Checking nginx configuration..."
NGINX_PORT=$(sudo grep -r "proxy_pass" /etc/nginx/ 2>/dev/null | grep -o ":[0-9]*" | grep -o "[0-9]*" | head -1)
echo "Nginx expects port: $NGINX_PORT"

if [ "$API_PORT" != "$NGINX_PORT" ]; then
    echo "⚠️  Port mismatch! API on $API_PORT but nginx expects $NGINX_PORT"
    echo "Updating nginx configuration..."
    sudo find /etc/nginx -name "*.conf" -exec sed -i "s/proxy_pass http:\/\/localhost:[0-9]*/proxy_pass http:\/\/localhost:$API_PORT/g" {} \;
    sudo find /etc/nginx -name "*.conf" -exec sed -i "s/proxy_pass http:\/\/127.0.0.1:[0-9]*/proxy_pass http:\/\/127.0.0.1:$API_PORT/g" {} \;
    echo "Reloading nginx..."
    sudo nginx -t && sudo systemctl reload nginx
fi

# Test domain
echo "10. Testing domain access..."
sleep 3
if curl -s -L "https://api.inflan.com/api/auth/getUser/1" > /dev/null; then
    echo "✅ Domain working!"
else
    echo "❌ Domain not working, restarting nginx..."
    sudo systemctl restart nginx
    sleep 5
    if curl -s -L "https://api.inflan.com/api/auth/getUser/1" > /dev/null; then
        echo "✅ Fixed after nginx restart!"
    else
        echo "❌ Still having issues"
        echo "Nginx error log:"
        sudo tail -10 /var/log/nginx/error.log
    fi
fi

echo ""
echo "=== DEPLOYMENT COMPLETE ==="
echo "API Port: $API_PORT"
echo "API PID: $API_PID"
echo "Domain: https://api.inflan.com"
echo ""
echo "Testing influencer API with real data..."
curl -s "https://api.inflan.com/swagger/v1/swagger.json" | grep -o '"youTube[^"]*"' | head -3 && echo "✅ YouTube fields found" || echo "❌ Issue with Swagger"
echo ""
echo "Use 'tail -f app.log' to monitor logs"
EOF

echo "✅ Deployment complete!"