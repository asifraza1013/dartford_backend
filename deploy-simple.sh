#!/bin/bash

# Simple deployment script for Inflan Backend (without Docker)
# This script deploys directly using dotnet

echo "🚀 Deploying Inflan Backend (non-Docker)..."

# Configuration
INSTANCE_IP="13.40.44.150"
KEY_PATH="$1"

# Check if SSH key path provided
if [ -z "$KEY_PATH" ]; then
    echo "❌ Error: Please provide the path to your SSH key file"
    echo "Usage: ./deploy-simple.sh /path/to/inflan-api-key-pair.pem"
    exit 1
fi

# Check if key file exists
if [ ! -f "$KEY_PATH" ]; then
    echo "❌ Error: SSH key file not found at $KEY_PATH"
    exit 1
fi

# Set correct permissions for SSH key
chmod 600 "$KEY_PATH"

echo "📦 Connecting to EC2 instance..."

# Deploy via SSH
ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@$INSTANCE_IP << 'EOF'
#!/bin/bash
set -e

echo "📦 Starting deployment on server..."

# Find and stop existing dotnet process
echo "Stopping existing application..."
EXISTING_PID=$(ps aux | grep '[d]otnet run' | grep 'inflan' | awk '{print $2}' || true)
if [ ! -z "$EXISTING_PID" ]; then
    echo "Stopping process $EXISTING_PID..."
    kill $EXISTING_PID || true
    sleep 5
fi

# Kill any remaining processes on port 10000
sudo lsof -ti:10000 | xargs -r kill -9 || true

# Backup current version (optional)
if [ -d "/home/ec2-user/dartford_backend" ]; then
    echo "Backing up current version..."
    mv /home/ec2-user/dartford_backend /home/ec2-user/dartford_backend.backup.$(date +%Y%m%d_%H%M%S)
fi

# Clone latest code
echo "Cloning latest code from GitHub..."
cd /home/ec2-user
git clone https://github.com/asifraza1013/dartford_backend.git
cd dartford_backend

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore

# Build the application
echo "Building application..."
dotnet build -c Release

# Ensure PostgreSQL is running
echo "Checking PostgreSQL..."
if ! systemctl is-active --quiet postgresql; then
    echo "Starting PostgreSQL..."
    sudo systemctl start postgresql
    sudo systemctl enable postgresql
fi

# Run database migrations
echo "Running database migrations..."
export ConnectionStrings__DefaultConnection="Host=localhost;Database=inflan_db;Username=postgres;Password=postgres123"
export ASPNETCORE_ENVIRONMENT="Production"
dotnet ef database update || echo "Migration skipped or failed"

# Start the application in background
echo "Starting application..."
cd /home/ec2-user/dartford_backend
nohup dotnet run \
    --urls="http://0.0.0.0:10000" \
    --environment="Production" \
    > /home/ec2-user/dartford_backend/app.log 2>&1 &

# Save the PID
echo $! > /home/ec2-user/dartford_backend/app.pid

# Wait for application to start
echo "Waiting for application to start..."
sleep 15

# Test the application
echo ""
echo "✅ Testing deployment..."
if curl -f -s http://localhost:10000/api/auth/getUser/1 > /dev/null; then
    echo "✅ API is responding correctly"
else
    echo "❌ API test failed"
fi

if curl -f -s http://localhost:10000/swagger/index.html > /dev/null; then
    echo "✅ Swagger UI is accessible"
else
    echo "❌ Swagger UI not accessible"
fi

echo ""
echo "🎉 Deployment complete!"
echo ""
echo "Logs: tail -f /home/ec2-user/dartford_backend/app.log"
echo "PID: $(cat /home/ec2-user/dartford_backend/app.pid)"
EOF

echo ""
echo "✅ Deployment finished!"
echo "🔗 API URL: http://$INSTANCE_IP:10000"
echo "📊 Swagger: http://$INSTANCE_IP:10000/swagger"
echo ""
echo "Since subdomain is already propagated, you can:"
echo "1. Set up nginx for https://api.inflan.com"
echo "2. Configure SSL certificate"