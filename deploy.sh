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

# The API runs under systemd (inflat-api.service), not a raw backgrounded
# process. Previous versions of this script killed the process with
# pkill/fuser and relaunched it with nohup, completely bypassing systemd.
# systemd's Restart=always kept trying to reclaim port 8080 in the
# background and failing forever (hundreds of thousands of restarts over
# time) because the nohup'd process it could never manage held the port.
# Stopping via systemd here, and only via systemd, keeps there being
# exactly one process manager for this service.
echo "Stopping current API (systemd)..."
sudo systemctl stop inflat-api.service

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

# Start via systemd
echo "Starting API (systemd)..."
sudo systemctl start inflat-api.service

# Wait for it to come up and confirm systemd — not a stray process — owns it
echo "Waiting for API to start (checking every 5 seconds, max 60 seconds)..."
STARTED=""
for i in {1..12}; do
    sleep 5
    if sudo systemctl is-active --quiet inflat-api.service && ss -tln | grep -q ":8080 "; then
        STARTED=1
        echo "✓ API started successfully after $((i*5)) seconds"
        break
    fi
    if ! sudo systemctl is-active --quiet inflat-api.service; then
        echo "✗ ERROR: inflat-api.service is not active"
        sudo systemctl status inflat-api.service --no-pager | head -20
        exit 1
    fi
    echo "Still waiting... ($i/12)"
done

if [ -n "$STARTED" ]; then
    echo "✓ Service verified, process details:"
    sudo systemctl status inflat-api.service --no-pager | head -10
else
    echo "✗ WARNING: Could not verify API is listening on 8080!"
    sudo systemctl status inflat-api.service --no-pager | head -20
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
        echo "❌ Still not working. Check logs: sudo journalctl -u inflat-api -f"
    fi
fi

echo ""
echo "Deployment complete. API logs:"
sudo journalctl -u inflat-api.service -n 20 --no-pager
EOF

echo "✅ Done! API: https://api.inflan.com"