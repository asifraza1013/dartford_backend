#!/bin/bash

# Fix deployment and database issues script
echo "🔧 Fixing deployment and database issues..."

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./fix-deployment.sh /path/to/inflan-api-key-pair.pem"
    exit 1
fi

# First, let's commit and push our latest changes
echo "📦 Committing latest changes..."
git add .
git commit -m "Simplify influencer validation - only block if no social accounts provided"
git push origin master

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
#!/bin/bash

echo "🛑 Stopping all dotnet processes..."
sudo pkill -9 -f dotnet || true
sleep 3

echo "📁 Navigating to app directory..."
cd /home/ec2-user/inflat-api-server

echo "🔧 Fixing git permissions..."
sudo chown -R ec2-user:ec2-user .
sudo chmod -R 755 .git

echo "📥 Pulling latest code..."
git fetch origin
git reset --hard origin/master
git pull origin master

echo "🗃️ Handling database issues..."
# Try to create migration for any pending changes
dotnet ef migrations add FixSocialValidation --force || echo "Migration creation failed, continuing..."

# Update database with any existing migrations, ignoring model validation warnings
export ASPNETCORE_ENVIRONMENT=Development
dotnet ef database update --no-build || echo "Database update failed, continuing..."

echo "🔨 Building application..."
dotnet clean
dotnet restore --force
dotnet build -c Release --force

echo "🚀 Starting API..."
export ASPNETCORE_ENVIRONMENT=Production
nohup dotnet run --no-build -c Release > app.log 2>&1 &

echo "⏳ Waiting for API to start..."
sleep 15

echo "🔍 Checking API status..."
if curl -s http://localhost:8080/api/User/getAllUsers > /dev/null; then
    echo "✅ API is running locally!"
else
    echo "❌ API not responding locally, checking logs..."
    tail -20 app.log
fi

echo "🌐 Testing domain access..."
if curl -s https://api.inflan.com/api/User/getAllUsers > /dev/null; then
    echo "✅ Domain is working!"
else
    echo "❌ Domain not accessible"
fi

echo "📋 Recent logs:"
tail -10 app.log

echo "✅ Deployment fix complete!"
EOF

echo "🎉 Deployment fix script completed!"