#!/bin/bash

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./deploy-safe.sh /path/to/key.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "=== SAFE DEPLOYMENT ==="

echo "1. Stopping all dotnet processes:"
sudo pkill -9 -f dotnet || true
sleep 5

echo "2. Verifying all processes stopped:"
ps aux | grep dotnet | grep -v grep || echo "✅ All dotnet processes stopped"

echo "3. Navigating to project and pulling code:"
cd /home/ec2-user/inflat-api-server
git pull origin master

echo "4. Building project:"
dotnet build -c Release

echo "5. Starting API on port 8080 (force the port):"
nohup dotnet run --urls="http://0.0.0.0:8080" > app.log 2>&1 &
API_PID=$!
echo "Started API with PID: $API_PID"

echo "6. Waiting for API startup (30 seconds):"
sleep 30

echo "7. Checking if API is running:"
if ps -p $API_PID > /dev/null 2>&1; then
    echo "✅ API process is running"
else
    echo "❌ API process died. Checking logs:"
    tail -20 app.log
    exit 1
fi

echo "8. Checking API logs:"
tail -10 app.log

echo "9. Testing local API:"
if curl -s http://localhost:8080/api/auth/getUser/1 > /dev/null; then
    echo "✅ API responding locally"
else
    echo "❌ API not responding locally"
    tail -20 app.log
    exit 1
fi

echo "10. Testing HTTPS domain:"
if curl -s https://api.inflan.com/api/auth/getUser/1 > /dev/null; then
    echo "✅ HTTPS domain working!"
else
    echo "❌ HTTPS domain not working. Checking nginx..."
    sudo systemctl status nginx | head -5
    sudo nginx -t
    echo "Restarting nginx..."
    sudo systemctl restart nginx
    sleep 5
    if curl -s https://api.inflan.com/api/auth/getUser/1 > /dev/null; then
        echo "✅ Fixed after nginx restart"
    else
        echo "❌ Still not working"
        echo "Nginx error log:"
        sudo tail -10 /var/log/nginx/error.log
    fi
fi

echo ""
echo "=== DEPLOYMENT COMPLETE ==="
echo "API PID: $API_PID"
echo "Domain: https://api.inflan.com"
echo "Swagger: https://api.inflan.com/swagger"
echo ""
echo "Commands to check status:"
echo "- tail -f app.log (view logs)"
echo "- ps aux | grep dotnet (check processes)"
echo "- curl https://api.inflan.com/api/auth/getUser/1 (test API)"
EOF