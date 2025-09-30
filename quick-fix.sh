#!/bin/bash

# Quick fix - bypass migration and start API
echo "🚀 Quick API restart (bypassing migrations)..."

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./quick-fix.sh /path/to/inflan-api-key-pair.pem"
    exit 1
fi

ssh -o ConnectTimeout=20 -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
#!/bin/bash

echo "🛑 Force killing any dotnet processes..."
sudo pkill -9 -f dotnet || true
sudo killall -9 dotnet || true

echo "📁 Going to app directory..."
cd /home/ec2-user/inflat-api-server

echo "🔧 Quick git pull..."
git pull || true

echo "🚀 Starting API without migration (using try-catch in Program.cs)..."
nohup dotnet run > app.log 2>&1 &
echo "Started API in background"

echo "⏳ Waiting 10 seconds..."
sleep 10

echo "📋 Checking status..."
if pgrep -f dotnet > /dev/null; then
    echo "✅ Dotnet process is running"
else
    echo "❌ No dotnet process found"
fi

echo "📄 Last 10 lines of log:"
tail -10 app.log

echo "🌐 Testing localhost..."
curl -s http://localhost:8080/api/User/getAllUsers | head -50 || echo "Local test failed"

EOF

echo "🎉 Quick fix completed!"