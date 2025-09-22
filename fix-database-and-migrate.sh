#!/bin/bash

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./fix-database-and-migrate.sh /path/to/key.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "=== FIXING DATABASE CONNECTION AND RUNNING MIGRATION ==="

echo "1. Checking current database setup:"
sudo systemctl status postgresql | head -5

echo ""
echo "2. Testing database connections:"
PGPASSWORD=postgres123 psql -h localhost -U postgres -d inflan_db -c "\dt" 2>/dev/null && echo "✅ postgres user works" || echo "❌ postgres user failed"

echo ""
echo "3. Updating connection string to use postgres user:"
cd /home/ec2-user/inflat-api-server

# Backup current config
cp appsettings.json appsettings.json.backup

# Update connection string to use postgres user (which works)
cat > appsettings.json << 'JSON'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=inflan_db;Username=postgres;Password=postgres123"
  }
}
JSON

echo "Updated connection string:"
cat appsettings.json | grep -A 3 "ConnectionStrings"

echo ""
echo "4. Running Entity Framework migration:"
dotnet ef database update

if [ $? -eq 0 ]; then
    echo "✅ Migration successful!"
    
    echo ""
    echo "5. Checking database tables:"
    PGPASSWORD=postgres123 psql -h localhost -U postgres -d inflan_db -c "\dt"
    
    echo ""
    echo "6. Checking Influencers table structure:"
    PGPASSWORD=postgres123 psql -h localhost -U postgres -d inflan_db -c "\d \"Influencers\""
    
    echo ""
    echo "7. Restarting API:"
    pkill -f dotnet || true
    sleep 3
    nohup dotnet run --urls="http://0.0.0.0:10000" > app.log 2>&1 &
    
    echo "8. Testing API (waiting 20 seconds):"
    sleep 20
    
    if curl -s http://localhost:10000/api/auth/getUser/1 > /dev/null; then
        echo "✅ API IS WORKING!"
        echo ""
        echo "9. Final test - Checking Swagger for YouTube fields:"
        curl -s http://localhost:10000/swagger/v1/swagger.json | grep -o '"youTube[^"]*"' | head -5 || echo "Checking with different pattern..."
        curl -s http://localhost:10000/swagger/v1/swagger.json | grep -o '"YouTube[^"]*"' | head -5 || echo "YouTube field check complete"
    else
        echo "❌ API failed. Last 20 lines of log:"
        tail -20 app.log
    fi
else
    echo "❌ Migration failed"
    exit 1
fi
EOF