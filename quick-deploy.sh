#!/bin/bash

KEY_PATH="$1"

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "=== FORCE DEPLOYING LATEST CODE ==="

cd /home/ec2-user/dartford_backend

echo "1. Current git status:"
git log --oneline -1
git status

echo "2. Force pull latest code:"
git fetch origin
git reset --hard origin/master

echo "3. Check if YouTube field exists in code:"
grep -n "public string? YouTube" Models/Influencer.cs
grep -n "YouTubeFollower" Models/Influencer.cs

echo "4. Stop all processes:"
pkill -f "dotnet" || true
sleep 3

echo "5. Clean rebuild:"
rm -rf bin/ obj/
dotnet restore
dotnet build -c Release

echo "6. Start with environment variable pointing to localhost:"
export ConnectionStrings__DefaultConnection="Host=localhost;Database=inflan_db;Username=postgres;Password=postgres123"
nohup dotnet run --urls="http://0.0.0.0:10000" > app.log 2>&1 &

sleep 15

echo "7. Test new API:"
if curl -s http://localhost:10000/api/auth/getUser/1 > /dev/null; then
    echo "✅ API is running!"
    echo "Checking Swagger for YouTube fields:"
    curl -s http://localhost:10000/swagger/v1/swagger.json | grep -A 3 -B 3 "youTube"
else
    echo "❌ API failed. Logs:"
    tail -20 app.log
fi
EOF