#!/bin/bash

# Inflan Backend Deployment Script
# Simple deployment - pull latest code and restart

echo "🚀 Deploying Inflan Backend..."

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./deploy.sh /path/to/inflan-api-key-pair.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@18.171.165.48 << 'EOF'
#!/bin/bash

echo "📦 Deploying latest changes..."

# Navigate to the CORRECT directory (inflat-api-server is the active one)
cd /home/ec2-user/inflat-api-server

# Stop existing processes more reliably
echo "Stopping current API..."
pkill -f "dotnet.*inflan_api.dll" || true
sleep 3

# Force kill if still running
pkill -9 -f "dotnet.*inflan_api.dll" 2>/dev/null || true
sleep 2

# Kill any process holding port 8080
echo "Ensuring port 8080 is free..."
fuser -k 8080/tcp 2>/dev/null || true
sleep 2

# Verify no dotnet processes are running
if pgrep -f "dotnet.*inflan_api.dll" > /dev/null; then
    echo "WARNING: Dotnet processes still running, force killing all..."
    pkill -9 -f dotnet 2>/dev/null || true
    sleep 3
fi

# Pull latest code
echo "Pulling latest code from master..."
git pull origin master

# Restore and build
echo "Building application..."
dotnet restore
dotnet build -c Release

# Run database migrations
echo "Running database migrations..."
ConnectionStrings__DefaultConnection="Host=localhost;Database=inflan_db;Username=postgres;Password=postgres123" dotnet ef database update

# Clear old logs
echo "Clearing old logs..."
> app.log

# Start application in Production mode with ALL required environment variables
# Note: Using sh -c to ensure environment variables are passed correctly to dotnet
echo "Starting API in Production mode with database connection..."
nohup sh -c 'PORT=8080 ConnectionStrings__DefaultConnection="Host=localhost;Database=inflan_db;Username=postgres;Password=postgres123" dotnet run --environment Production' > app.log 2>&1 &

# Wait for API to fully start and find the actual dotnet process
echo "Waiting for API to start (checking every 5 seconds, max 60 seconds)..."
NEW_PID=""
for i in {1..12}; do
    sleep 5

    # Try to find the actual dotnet process running inflan_api.dll
    NEW_PID=$(pgrep -f "dotnet.*inflan_api.dll" | head -1)

    if [ -n "$NEW_PID" ] && (grep -q "Application started" app.log 2>/dev/null || netstat -tuln | grep -q ":8080 "); then
        echo "✓ API started successfully after $((i*5)) seconds with PID: $NEW_PID"
        break
    fi

    if grep -q "Failed to bind" app.log 2>/dev/null; then
        echo "✗ ERROR: Failed to bind to port 8080"
        tail -20 app.log
        exit 1
    fi
    echo "Still waiting... ($i/12)"
done

# Kill any duplicate process on port 10000 (sometimes dotnet spawns a second process)
echo "Cleaning up any duplicate processes..."
pkill -9 -f "urls=http://0.0.0.0:10000" 2>/dev/null || true
sleep 2

# Verify the correct process is running
if [ -n "$NEW_PID" ] && ps -p $NEW_PID > /dev/null 2>&1; then
    echo "✓ Process $NEW_PID is running and verified"
    echo "Process details:"
    ps -p $NEW_PID -o pid,etime,cmd
else
    echo "✗ WARNING: Could not verify API process is running!"
    echo "Checking all dotnet processes:"
    ps aux | grep dotnet | grep -v grep
fi

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