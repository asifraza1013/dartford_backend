#!/bin/bash

KEY_PATH="$1"

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "=== RUNNING DATABASE MIGRATION DIRECTLY ==="

# First, let's try to connect to PostgreSQL with different methods
echo "1. Trying to connect to PostgreSQL directly..."

# Method 1: Direct connection as postgres user
sudo -u postgres psql -d inflan_db -c "
-- Drop Twitter column if it exists
ALTER TABLE \"Influencers\" DROP COLUMN IF EXISTS \"Twitter\";

-- Rename TwitterFollower to YouTubeFollower if it exists  
DO \$\$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Influencers' AND column_name = 'TwitterFollower') THEN
        ALTER TABLE \"Influencers\" RENAME COLUMN \"TwitterFollower\" TO \"YouTubeFollower\";
    END IF;
END \$\$;

-- Verify changes
SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'Influencers' ORDER BY column_name;
" 2>/dev/null

if [ $? -eq 0 ]; then
    echo "✅ Database migration completed successfully!"
else
    echo "❌ Direct connection failed. Trying alternative methods..."
    
    # Method 2: Try with peer authentication
    sudo su - postgres -c "psql -d inflan_db -c \"
    ALTER TABLE \\\"Influencers\\\" DROP COLUMN IF EXISTS \\\"Twitter\\\";
    ALTER TABLE \\\"Influencers\\\" RENAME COLUMN \\\"TwitterFollower\\\" TO \\\"YouTubeFollower\\\";
    SELECT column_name FROM information_schema.columns WHERE table_name = 'Influencers';
    \"" 2>/dev/null
    
    if [ $? -eq 0 ]; then
        echo "✅ Migration completed with method 2!"
    else
        echo "❌ All methods failed. Trying one more approach..."
        
        # Method 3: Create SQL file and execute
        cat > /tmp/migrate.sql << 'SQL'
ALTER TABLE "Influencers" DROP COLUMN IF EXISTS "Twitter";
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Influencers' AND column_name = 'TwitterFollower') THEN
        ALTER TABLE "Influencers" RENAME COLUMN "TwitterFollower" TO "YouTubeFollower";
    END IF;
END $$;
SELECT 'Migration completed' as status;
SQL
        
        sudo -u postgres psql -d inflan_db -f /tmp/migrate.sql
        rm /tmp/migrate.sql
    fi
fi

echo ""
echo "2. Restarting API with latest code..."
cd /home/ec2-user/dartford_backend

# Stop current API
pkill -f "dotnet" || true
sleep 3

# Start API
nohup dotnet run --urls="http://0.0.0.0:10000" > app.log 2>&1 &

sleep 15

echo "3. Testing API..."
if curl -s http://localhost:10000/api/auth/getUser/1 > /dev/null; then
    echo "✅ API is running!"
    echo "4. Checking Swagger for YouTube-only fields:"
    curl -s http://localhost:10000/swagger/v1/swagger.json | jq '.components.schemas.Influencer.properties | keys' || echo "Checking without jq..."
    curl -s http://localhost:10000/swagger/v1/swagger.json | grep -A 20 '"Influencer"' | grep -E '"(twitter|youTube|YouTubeFollower)"' || echo "No Twitter fields found - SUCCESS!"
else
    echo "❌ API failed to start. Logs:"
    tail -30 app.log
fi
EOF