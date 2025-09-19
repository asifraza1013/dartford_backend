#!/bin/bash

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./connect-to-server-db.sh /path/to/inflan-api-key-pair.pem"
    exit 1
fi

echo "=== FINDING DATABASE CONNECTION INFO ==="

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "1. Checking if PostgreSQL is running on this server:"
ps aux | grep postgres | grep -v grep

echo ""
echo "2. Checking Docker containers:"
docker ps | grep postgres || echo "No PostgreSQL Docker containers found"

echo ""
echo "3. Checking for database config files:"
cd /home/ec2-user/dartford_backend
echo "appsettings.json:"
cat appsettings.json | grep -A 5 "ConnectionStrings" || echo "No ConnectionStrings found"

echo ""
echo "appsettings.Development.json:"
cat appsettings.Development.json | grep -A 5 "ConnectionStrings" || echo "No ConnectionStrings found"

echo ""
echo "4. Checking network connections:"
netstat -tlnp | grep :5432 || echo "No PostgreSQL service found on port 5432"

echo ""
echo "5. Checking environment variables:"
env | grep -i postgres || echo "No PostgreSQL environment variables found"
EOF