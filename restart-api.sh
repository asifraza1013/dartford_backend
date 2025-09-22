#!/bin/bash

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./restart-api.sh /path/to/key.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "=== RESTARTING API SERVER ==="

echo "1. Stopping all dotnet processes:"
pkill -f dotnet || true
sleep 5

echo ""
echo "2. Checking if any dotnet processes are still running:"
ps aux | grep dotnet | grep -v grep || echo "No dotnet processes running"

echo ""
echo "3. Force kill if needed:"
pkill -9 -f dotnet 2>/dev/null || true
sleep 2

echo ""
echo "4. Starting API server:"
cd /home/ec2-user/inflat-api-server
nohup dotnet run > app.log 2>&1 &

echo ""
echo "5. Waiting for API to start (30 seconds):"
sleep 30

echo ""
echo "6. Checking if API is running:"
ps aux | grep dotnet | grep -v grep && echo "✅ API process is running" || echo "❌ API process not found"

echo ""
echo "7. Checking what port API is listening on:"
tail -10 app.log | grep "Now listening" || echo "No listening port found in logs"

echo ""
echo "8. Testing API locally:"
curl -s http://localhost:8080/api/auth/getUser/1 > /dev/null && echo "✅ API working on 8080" || echo "❌ API not working on 8080"

echo ""
echo "9. Testing domain:"
curl -s https://api.inflan.com/api/auth/getUser/1 > /dev/null && echo "✅ Domain working" || echo "❌ Domain showing bad gateway"

echo ""
echo "10. Recent API logs:"
tail -20 app.log

echo ""
echo "11. If domain still shows bad gateway, restarting nginx:"
if ! curl -s https://api.inflan.com/api/auth/getUser/1 > /dev/null; then
    echo "Restarting nginx..."
    sudo systemctl restart nginx
    sleep 5
    curl -s https://api.inflan.com/api/auth/getUser/1 > /dev/null && echo "✅ Fixed after nginx restart" || echo "❌ Still not working"
fi

echo ""
echo "✅ API restart complete!"
echo "Domain: https://api.inflan.com"
echo "Swagger: https://api.inflan.com/swagger"
EOF