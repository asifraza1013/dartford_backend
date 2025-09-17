#!/bin/bash

# Quick deployment script for Inflan Backend
# This script SSHs into the EC2 instance and runs deployment commands

INSTANCE_IP="13.40.44.150"
KEY_NAME="inflan-api-key-pair"

echo "🚀 Quick Deploy to Inflan Backend..."

# Create temporary script
cat > /tmp/deploy-commands.sh << 'EOF'
#!/bin/bash

echo "📦 Deploying Inflan Backend..."

# Stop and remove old containers
echo "Stopping old containers..."
sudo docker stop inflan_api 2>/dev/null || true
sudo docker stop postgres 2>/dev/null || true
sudo docker rm inflan_api 2>/dev/null || true
sudo docker rm postgres 2>/dev/null || true

# Check if code directory exists
if [ -d "/home/ec2-user/dartford_backend" ]; then
    echo "Updating existing code..."
    cd /home/ec2-user/dartford_backend
    git pull origin master
else
    echo "Cloning repository..."
    cd /home/ec2-user
    git clone https://github.com/asifraza1013/dartford_backend.git
    cd dartford_backend
fi

# Build the application
echo "Building Docker image..."
sudo docker build -t inflan_api .

# Start PostgreSQL
echo "Starting PostgreSQL..."
sudo docker run -d \
    --name postgres \
    --restart unless-stopped \
    -e POSTGRES_DB=inflan_db \
    -e POSTGRES_USER=postgres \
    -e POSTGRES_PASSWORD=postgres123 \
    -p 5432:5432 \
    postgres:15-alpine

# Wait for PostgreSQL to be ready
echo "Waiting for PostgreSQL..."
sleep 10

# Start the API
echo "Starting API server..."
sudo docker run -d \
    --name inflan_api \
    --restart unless-stopped \
    -p 10000:8080 \
    -e ASPNETCORE_ENVIRONMENT=Production \
    -e "ConnectionStrings__DefaultConnection=Host=host.docker.internal;Database=inflan_db;Username=postgres;Password=postgres123" \
    -e "Jwt__Key=YourProductionSecretKeyForJWTTokenGenerationShouldBeAtLeast32Characters!" \
    --add-host host.docker.internal:host-gateway \
    -v /home/ec2-user/dartford_backend/wwwroot:/app/wwwroot \
    inflan_api

# Check deployment
sleep 10
echo ""
echo "✅ Deployment status:"
sudo docker ps
echo ""
echo "Testing API..."
curl -f http://localhost:10000/swagger/index.html > /dev/null && echo "✅ Swagger UI is accessible" || echo "❌ Swagger UI not accessible"
curl -f http://localhost:10000/api/auth/getUser/1 > /dev/null && echo "✅ API is responding" || echo "❌ API not responding"
echo ""
echo "🎉 Deployment complete!"
echo "API URL: http://${INSTANCE_IP}:10000"
echo "Swagger: http://${INSTANCE_IP}:10000/swagger"
EOF

echo "Please provide the path to your SSH key file (inflan-api-key-pair.pem):"
echo "Or press Enter to skip SSH deployment and just update the deploy.sh file"