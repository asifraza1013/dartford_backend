#!/bin/bash

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./fix-social-blade-config.sh /path/to/key.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "=== FIXING SOCIAL BLADE CONFIGURATION ==="

echo "1. Checking current server configuration files:"
cd /home/ec2-user/inflat-api-server

echo "appsettings.json:"
cat appsettings.json | grep -A 10 "SocialBlade" || echo "No SocialBlade config in appsettings.json"

echo ""
echo "appsettings.Development.json:"
cat appsettings.Development.json | grep -A 10 "SocialBlade" || echo "No SocialBlade config in appsettings.Development.json"

echo ""
echo "appsettings.Production.json:"
cat appsettings.Production.json 2>/dev/null | grep -A 10 "SocialBlade" || echo "No appsettings.Production.json file"

echo ""
echo "2. Adding Social Blade configuration to appsettings.json:"
# Backup current config
cp appsettings.json appsettings.json.backup

# Add Social Blade config to appsettings.json
cat > appsettings.json << 'JSON'
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=inflan_db;Username=postgres;Password=postgres123"
  },
  "Jwt": {
    "Key": "YourSecretKeyForJWTTokenGenerationShouldBeAtLeast32Characters!"
  },
  "SocialBlade": {
    "ApiKey": "cli_bf3eb6b244fab365c2b43037",
    "ApiSecret": "dcbafc55c0790d9f3e00a7385e7947cd9679ab1c8aaeaa03a7bd1ae98141c762",
    "BaseUrl": "https://matrix.sbapis.com/b",
    "TestUsername": "rick",
    "UseTestMode": false,
    "TimeoutSeconds": 30,
    "RetryCount": 3,
    "RetryDelayMs": 1000
  },
  "FollowerSync": {
    "Enabled": false,
    "DayOfWeek": 0,
    "HourUtc": 2,
    "DelayBetweenSyncsMs": 1000
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
JSON

echo ""
echo "3. Verifying configuration was added:"
cat appsettings.json | grep -A 10 "SocialBlade"

echo ""
echo "4. Restarting API with new configuration:"
pkill -f dotnet || true
sleep 3
nohup dotnet run > app.log 2>&1 &

echo ""
echo "5. Waiting for API to start with new config:"
sleep 25

echo ""
echo "6. Testing the API now with Social Blade config:"
TOKEN="eyJhbGciOiJodHRwOi8vd3d3LnczLm9yZy8yMDAxLzA0L3htbGRzaWctbW9yZSNobWFjLXNoYTI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjMiLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9lbWFpbGFkZHJlc3MiOiJhc2lmLnJhemExMDEzKzJAZ21haWwuY29tIiwiVXNlclR5cGUiOiIzIiwibmJmIjoxNzU4NTQ5NzYxLCJleHAiOjE3NjExNDE3NjF9.emcVG5kLaHkNGnuNQXSKmYGBKyprx-N6VQsnuiP37ms"

curl -s -X POST "http://localhost:8080/api/Influencer/createNewInfluencer" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "youTube": "imranriazkhan1",
    "instagram": "ch.zulqarnain25",
    "facebook": "zulqarnainsikandar09",
    "tikTok": "ch.zulqarnain25",
    "bio": "Testing with Social Blade config"
  }' | jq '.' 2>/dev/null || cat

echo ""
echo "7. Testing on domain:"
curl -s -X POST "https://api.inflan.com/api/Influencer/createNewInfluencer" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "youTube": "imranriazkhan1",
    "instagram": "ch.zulqarnain25",
    "facebook": "zulqarnainsikandar09",
    "tikTok": "ch.zulqarnain25",
    "bio": "Final test"
  }' | jq '.' 2>/dev/null || cat

echo ""
echo "✅ Social Blade configuration added! The API should now return real follower counts."
EOF