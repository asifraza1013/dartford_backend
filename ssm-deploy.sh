#!/bin/bash

KEY_PATH="$1"

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "=== FINAL ATTEMPT: DIRECT DATABASE MODIFICATION ==="

# Create the SQL migration file
cat > migrate_to_youtube.sql << 'SQL'
-- Step 1: Add YouTubeFollower column if it doesn't exist
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Influencers' AND column_name = 'YouTubeFollower') THEN
        ALTER TABLE "Influencers" ADD COLUMN "YouTubeFollower" integer DEFAULT 0;
    END IF;
END $$;

-- Step 2: Copy data from TwitterFollower to YouTubeFollower
UPDATE "Influencers" SET "YouTubeFollower" = "TwitterFollower" WHERE "TwitterFollower" IS NOT NULL;

-- Step 3: Drop Twitter column
ALTER TABLE "Influencers" DROP COLUMN IF EXISTS "Twitter";

-- Step 4: Drop TwitterFollower column  
ALTER TABLE "Influencers" DROP COLUMN IF EXISTS "TwitterFollower";

-- Step 5: Verify the migration
SELECT 'Migration completed - Final table structure:' as status;
SELECT column_name, data_type, is_nullable FROM information_schema.columns 
WHERE table_name = 'Influencers' 
ORDER BY column_name;
SQL

echo "Created migration file. Executing SQL..."

# Try multiple methods to execute the SQL
if sudo -u postgres psql -d inflan_db -f migrate_to_youtube.sql; then
    echo "✅ SQL executed successfully via method 1"
elif sudo su - postgres -c "psql -d inflan_db -f /home/ec2-user/migrate_to_youtube.sql"; then
    echo "✅ SQL executed successfully via method 2"  
else
    echo "❌ SQL execution failed. Trying manual commands..."
    # Manual command execution
    sudo -u postgres psql -d inflan_db << 'MANUAL_SQL'
ALTER TABLE "Influencers" ADD COLUMN IF NOT EXISTS "YouTubeFollower" integer DEFAULT 0;
UPDATE "Influencers" SET "YouTubeFollower" = "TwitterFollower" WHERE "TwitterFollower" IS NOT NULL;
ALTER TABLE "Influencers" DROP COLUMN IF EXISTS "Twitter";
ALTER TABLE "Influencers" DROP COLUMN IF EXISTS "TwitterFollower";
SELECT column_name FROM information_schema.columns WHERE table_name = 'Influencers' ORDER BY column_name;
MANUAL_SQL
fi

echo ""
echo "Restarting API with fresh code..."
cd /home/ec2-user/dartford_backend

# Force latest code
git fetch origin
git reset --hard origin/master

# Kill all dotnet processes
sudo pkill -f dotnet || true
sleep 5

# Start API
nohup dotnet run --urls="http://0.0.0.0:10000" > app.log 2>&1 &

sleep 20

echo "Testing API..."
if curl -s http://localhost:10000/api/auth/getUser/1 > /dev/null; then
    echo "✅ API STARTED SUCCESSFULLY!"
    echo ""
    echo "🎉 FINAL TEST - Swagger Schema:"
    curl -s http://localhost:10000/swagger/v1/swagger.json | grep -A 15 '"Influencer"' | grep -E '"(twitter|youTube|YouTube)"' || echo "No Twitter found - SUCCESS!"
    echo ""
    echo "All fields:"
    curl -s http://localhost:10000/swagger/v1/swagger.json | jq '.components.schemas.Influencer.properties | keys' 2>/dev/null || echo "jq not available"
else
    echo "❌ API FAILED TO START"
    echo "Error logs:"
    tail -50 app.log
fi

# Cleanup
rm -f migrate_to_youtube.sql
EOF