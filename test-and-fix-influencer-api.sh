#!/bin/bash

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./test-and-fix-influencer-api.sh /path/to/key.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "=== TESTING AND FIXING INFLUENCER API ==="

TOKEN="eyJhbGciOiJodHRwOi8vd3d3LnczLm9yZy8yMDAxLzA0L3htbGRzaWctbW9yZSNobWFjLXNoYTI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjMiLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9lbWFpbGFkZHJlc3MiOiJhc2lmLnJhemExMDEzKzJAZ21haWwuY29tIiwiVXNlclR5cGUiOiIzIiwibmJmIjoxNzU4NTQ5NzYxLCJleHAiOjE3NjExNDE3NjF9.emcVG5kLaHkNGnuNQXSKmYGBKyprx-N6VQsnuiP37ms"

echo "1. Checking if API is running:"
cd /home/ec2-user/inflat-api-server
ps aux | grep dotnet | grep -v grep && echo "✅ API is running" || echo "❌ API not running"

echo ""
echo "2. Starting/Restarting API if needed:"
pkill -f dotnet || true
sleep 3
nohup dotnet run > app.log 2>&1 &
sleep 25

echo ""
echo "3. Checking API startup logs:"
tail -15 app.log

echo ""
echo "4. Testing basic API health:"
curl -s http://localhost:8080/api/auth/getUser/1 > /dev/null && echo "✅ API responding" || echo "❌ API not responding"

echo ""
echo "5. Testing createNewInfluencer API locally:"
echo "Making request to: http://localhost:8080/api/Influencer/createNewInfluencer"

RESPONSE=$(curl -s -w "\nHTTP_STATUS:%{http_code}" -X POST "http://localhost:8080/api/Influencer/createNewInfluencer" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "youTube": "ZulqarnainSikandar25",
    "instagram": "ch.zulqarnain25",
    "facebook": "zulqarnainsikandar09",
    "tikTok": "ch.zulqarnain25",
    "bio": "Testing API locally"
  }')

echo "Response:"
echo "$RESPONSE"

HTTP_STATUS=$(echo "$RESPONSE" | grep "HTTP_STATUS" | cut -d: -f2)
echo ""
echo "HTTP Status: $HTTP_STATUS"

if [ "$HTTP_STATUS" = "200" ] || [ "$HTTP_STATUS" = "201" ]; then
    echo "✅ API call successful!"
    
    # Extract follower counts from response
    YOUTUBE_FOLLOWERS=$(echo "$RESPONSE" | grep -o '"youTubeFollower":[0-9]*' | cut -d: -f2)
    TIKTOK_FOLLOWERS=$(echo "$RESPONSE" | grep -o '"tikTokFollower":[0-9]*' | cut -d: -f2)
    FACEBOOK_FOLLOWERS=$(echo "$RESPONSE" | grep -o '"facebookFollower":[0-9]*' | cut -d: -f2)
    
    echo ""
    echo "Follower counts extracted:"
    echo "YouTube: $YOUTUBE_FOLLOWERS"
    echo "TikTok: $TIKTOK_FOLLOWERS"
    echo "Facebook: $FACEBOOK_FOLLOWERS"
    
    if [ "$YOUTUBE_FOLLOWERS" != "0" ] && [ "$TIKTOK_FOLLOWERS" != "0" ] && [ "$FACEBOOK_FOLLOWERS" != "0" ]; then
        echo "✅ Social Blade integration working - got real follower counts!"
    else
        echo "❌ Still getting 0 follower counts - Social Blade integration issue"
        echo ""
        echo "6. Checking Social Blade API logs:"
        tail -30 app.log | grep -i "social\|blade\|matrix\|clientid\|token"
    fi
else
    echo "❌ API call failed with status: $HTTP_STATUS"
    echo ""
    echo "6. Checking recent error logs:"
    tail -30 app.log | grep -i "error\|exception\|fail"
fi

echo ""
echo "7. Testing on domain:"
DOMAIN_RESPONSE=$(curl -s -w "\nHTTP_STATUS:%{http_code}" -X POST "https://api.inflan.com/api/Influencer/createNewInfluencer" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "youTube": "imranriazkhan1",
    "instagram": "ch.zulqarnain25",
    "facebook": "zulqarnainsikandar09",
    "tikTok": "ch.zulqarnain25",
    "bio": "Testing API on domain"
  }')

DOMAIN_STATUS=$(echo "$DOMAIN_RESPONSE" | grep "HTTP_STATUS" | cut -d: -f2)
echo "Domain HTTP Status: $DOMAIN_STATUS"

if [ "$DOMAIN_STATUS" = "200" ] || [ "$DOMAIN_STATUS" = "201" ]; then
    echo "✅ Domain API working!"
else
    echo "❌ Domain API failed"
    echo "Domain Response:"
    echo "$DOMAIN_RESPONSE"
fi

echo ""
echo "8. Final verification - testing Social Blade API directly:"
echo "Testing YouTube API directly:"
curl -s -H "clientid: cli_bf3eb6b244fab365c2b43037" \
     -H "token: dcbafc55c0790d9f3e00a7385e7947cd9679ab1c8aaeaa03a7bd1ae98141c762" \
     "https://matrix.sbapis.com/b/youtube/statistics?query=imranriazkhan1&history=default&allow-stale=false" | head -20

echo ""
echo ""
echo "=== SUMMARY ==="
echo "Local API Status: $HTTP_STATUS"
echo "Domain API Status: $DOMAIN_STATUS"
echo "Social Blade: $([ "$YOUTUBE_FOLLOWERS" != "0" ] && echo "Working" || echo "Not working")"
echo ""
echo "API Endpoint: https://api.inflan.com/api/Influencer/createNewInfluencer"
EOF