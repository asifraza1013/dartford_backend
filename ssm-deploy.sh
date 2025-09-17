#!/bin/bash

# Inflan Backend SSM Deployment Script
# Uses AWS Systems Manager to deploy without SSH

echo "🚀 Deploying Inflan Backend via AWS SSM..."

# Configuration
INSTANCE_ID="i-067ec62edc0021565"
REGION="eu-west-2"

# First, enable SSM on the instance if needed
echo "Checking SSM agent status..."

# Send deployment commands
aws ssm send-command \
    --instance-ids "$INSTANCE_ID" \
    --document-name "AWS-RunShellScript" \
    --parameters 'commands=[
        "#!/bin/bash",
        "echo \"📦 Starting Inflan Backend Deployment...\"",
        "",
        "# Stop and remove old containers",
        "echo \"Stopping old containers...\"",
        "sudo docker stop inflan_api 2>/dev/null || true",
        "sudo docker stop postgres 2>/dev/null || true",
        "sudo docker rm inflan_api 2>/dev/null || true",
        "sudo docker rm postgres 2>/dev/null || true",
        "",
        "# Update or clone repository",
        "cd /home/ec2-user",
        "if [ -d \"dartford_backend\" ]; then",
        "    echo \"Updating existing repository...\"",
        "    cd dartford_backend",
        "    git pull origin master",
        "else",
        "    echo \"Cloning repository...\"",
        "    git clone https://github.com/asifraza1013/dartford_backend.git",
        "    cd dartford_backend",
        "fi",
        "",
        "# Build Docker image",
        "echo \"Building Docker image...\"",
        "sudo docker build -t inflan_api .",
        "",
        "# Start PostgreSQL",
        "echo \"Starting PostgreSQL...\"",
        "sudo docker run -d --name postgres --restart unless-stopped -e POSTGRES_DB=inflan_db -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres123 -p 5432:5432 postgres:15-alpine",
        "",
        "# Wait for PostgreSQL",
        "echo \"Waiting for PostgreSQL to start...\"",
        "sleep 10",
        "",
        "# Start API server",
        "echo \"Starting API server...\"",
        "sudo docker run -d --name inflan_api --restart unless-stopped -p 10000:8080 -e ASPNETCORE_ENVIRONMENT=Production -e \"ConnectionStrings__DefaultConnection=Host=host.docker.internal;Database=inflan_db;Username=postgres;Password=postgres123\" -e \"Jwt__Key=YourProductionSecretKeyForJWTTokenGenerationShouldBeAtLeast32Characters!\" --add-host host.docker.internal:host-gateway -v /home/ec2-user/dartford_backend/wwwroot:/app/wwwroot inflan_api",
        "",
        "# Wait for API to start",
        "sleep 10",
        "",
        "# Check deployment status",
        "echo \"\"",
        "echo \"✅ Deployment Status:\"",
        "sudo docker ps",
        "echo \"\"",
        "echo \"Testing API endpoints...\"",
        "curl -f http://localhost:10000/swagger/index.html > /dev/null && echo \"✅ Swagger UI: OK\" || echo \"❌ Swagger UI: Failed\"",
        "curl -f http://localhost:10000/api/auth/getUser/1 > /dev/null && echo \"✅ API Endpoint: OK\" || echo \"❌ API Endpoint: Failed\"",
        "echo \"\"",
        "echo \"🎉 Deployment complete!\"",
        "echo \"API: http://13.40.44.150:10000\"",
        "echo \"Swagger: http://13.40.44.150:10000/swagger\""
    ]' \
    --region "$REGION" \
    --output table

echo ""
echo "✅ Deployment command sent!"
echo "Note: SSM might not be enabled on this instance."
echo "If deployment fails, you'll need to:"
echo "1. Install SSM agent on the EC2 instance"
echo "2. Or use SSH with the inflan-api-key-pair.pem key"
echo ""
echo "🔗 API will be available at: http://13.40.44.150:10000"
echo "📊 Swagger UI: http://13.40.44.150:10000/swagger"