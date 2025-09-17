#!/bin/bash

# Inflan Backend Deployment Script
# Deploys to EC2 instance using SSH

echo "🚀 Deploying Inflan Backend..."

# Configuration
INSTANCE_IP="13.40.44.150"
KEY_PATH="$1"

# Check if SSH key path provided
if [ -z "$KEY_PATH" ]; then
    echo "❌ Error: Please provide the path to your SSH key file"
    echo "Usage: ./deploy.sh /path/to/inflan-api-key-pair.pem"
    echo ""
    echo "Alternative: Try using ./ssm-deploy.sh if SSM is enabled"
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

# Stop and remove old containers
echo "Stopping old containers..."
sudo docker stop inflan_api 2>/dev/null || true
sudo docker stop postgres 2>/dev/null || true
sudo docker rm inflan_api 2>/dev/null || true
sudo docker rm postgres 2>/dev/null || true

# Update or clone repository
cd /home/ec2-user
if [ -d "dartford_backend" ]; then
    echo "Updating existing repository..."
    cd dartford_backend
    git pull origin master
else
    echo "Cloning repository..."
    git clone https://github.com/asifraza1013/dartford_backend.git
    cd dartford_backend
fi

# Build Docker image
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

# Wait for PostgreSQL
echo "Waiting for PostgreSQL to start..."
sleep 10

# Start API server
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

# Wait for API to start
sleep 10

# Check deployment status
echo ""
echo "✅ Deployment Status:"
sudo docker ps
echo ""
echo "Testing API endpoints..."
curl -f http://localhost:10000/swagger/index.html > /dev/null && echo "✅ Swagger UI: OK" || echo "❌ Swagger UI: Failed"
curl -f http://localhost:10000/api/auth/getUser/1 > /dev/null && echo "✅ API Endpoint: OK" || echo "❌ API Endpoint: Failed"

echo ""
echo "🎉 Deployment complete!"
EOF

echo ""
echo "✅ Deployment finished!"
echo "🔗 API URL: http://$INSTANCE_IP:10000"
echo "📊 Swagger: http://$INSTANCE_IP:10000/swagger"
echo ""
echo "Next steps:"
echo "1. Configure subdomain api.inflan.com to point to $INSTANCE_IP"
echo "2. Set up SSL certificate for HTTPS"