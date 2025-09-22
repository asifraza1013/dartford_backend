#!/bin/bash

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./fix-postgres-auth.sh /path/to/key.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "=== FIXING POSTGRESQL AUTHENTICATION ==="

echo "1. Checking current pg_hba.conf authentication methods:"
sudo cat /var/lib/pgsql/data/pg_hba.conf | grep -E "(local|host)" | grep -v "#"

echo ""
echo "2. Backing up pg_hba.conf:"
sudo cp /var/lib/pgsql/data/pg_hba.conf /var/lib/pgsql/data/pg_hba.conf.backup

echo ""
echo "3. Updating authentication to use md5 (password) instead of ident:"
# Change local connections to use md5 instead of peer/ident
sudo sed -i 's/local   all             all                                     peer/local   all             all                                     md5/g' /var/lib/pgsql/data/pg_hba.conf
sudo sed -i 's/local   all             all                                     ident/local   all             all                                     md5/g' /var/lib/pgsql/data/pg_hba.conf

# Change host connections to use md5 instead of ident
sudo sed -i 's/host    all             all             127.0.0.1\/32            ident/host    all             all             127.0.0.1\/32            md5/g' /var/lib/pgsql/data/pg_hba.conf
sudo sed -i 's/host    all             all             ::1\/128                 ident/host    all             all             ::1\/128                 md5/g' /var/lib/pgsql/data/pg_hba.conf

echo ""
echo "4. Updated pg_hba.conf (showing authentication lines):"
sudo cat /var/lib/pgsql/data/pg_hba.conf | grep -E "(local|host)" | grep -v "#"

echo ""
echo "5. Restarting PostgreSQL to apply changes:"
sudo systemctl restart postgresql
sleep 5

echo ""
echo "6. Checking PostgreSQL status:"
sudo systemctl status postgresql | head -5

echo ""
echo "7. Setting password for postgres user (if needed):"
sudo -u postgres psql << 'PSQL'
ALTER USER postgres WITH PASSWORD 'postgres123';
\q
PSQL

echo ""
echo "8. Testing connection with password:"
PGPASSWORD=postgres123 psql -h localhost -U postgres -d inflan_db -c "\dt" && echo "✅ Password authentication works!" || echo "❌ Still having issues"

echo ""
echo "9. Running Entity Framework migration:"
cd /home/ec2-user/inflat-api-server
dotnet ef database update

if [ $? -eq 0 ]; then
    echo "✅ Migration successful!"
    
    echo ""
    echo "10. Checking migration results:"
    PGPASSWORD=postgres123 psql -h localhost -U postgres -d inflan_db -c "\dt"
    
    echo ""
    echo "11. Checking Influencers table structure:"
    PGPASSWORD=postgres123 psql -h localhost -U postgres -d inflan_db -c "\d \"Influencers\""
    
    echo ""
    echo "12. Restarting API:"
    pkill -f dotnet || true
    sleep 3
    nohup dotnet run --urls="http://0.0.0.0:10000" > app.log 2>&1 &
    
    echo ""
    echo "13. Testing API:"
    sleep 20
    if curl -s http://localhost:10000/api/auth/getUser/1 > /dev/null; then
        echo "✅ API IS WORKING WITH YOUTUBE-ONLY DATABASE!"
    else
        echo "❌ API issue. Recent logs:"
        tail -20 app.log
    fi
else
    echo "❌ Migration failed"
fi
EOF