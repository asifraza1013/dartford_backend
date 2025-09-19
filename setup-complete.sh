#!/bin/bash

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./setup-complete.sh /path/to/inflan-api-key-pair.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "=== SETTING UP DATABASE CONNECTION ==="

cd /home/ec2-user/dartford_backend

echo "1. Setting correct connection string environment variable:"
export ConnectionStrings__DefaultConnection="Host=localhost;Database=inflan_db;Username=postgres;Password=postgres123"

echo "2. Stopping current API..."
pkill -f "dotnet" || true
sleep 5

echo "3. Running the migration with correct connection string:"
dotnet tool install --global dotnet-ef --version 8.0.0 || true
export PATH="$PATH:/home/ec2-user/.dotnet/tools"

ConnectionStrings__DefaultConnection="Host=localhost;Database=inflan_db;Username=postgres;Password=postgres123" dotnet ef database update

echo "4. Starting API with correct environment variable..."
ConnectionStrings__DefaultConnection="Host=localhost;Database=inflan_db;Username=postgres;Password=postgres123" nohup dotnet run --urls="http://0.0.0.0:10000" > app.log 2>&1 &

sleep 15

echo "5. Testing API..."
if curl -s http://localhost:10000/api/auth/getUser/1 > /dev/null; then
    echo "✅ API is running with correct database connection!"
    
    echo "6. Testing Swagger to see if Twitter fields are removed:"
    curl -s http://localhost:10000/swagger/v1/swagger.json | grep -A 10 -B 5 "youTube\|twitter" || echo "Checking schema..."
else
    echo "❌ API failed to start. Logs:"
    tail -30 app.log
fi
EOF