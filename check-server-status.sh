#!/bin/bash

KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./check-server-status.sh /path/to/inflan-api-key-pair.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@13.40.44.150 << 'EOF'
echo "=== CHECKING SERVER STATUS ==="

echo "1. Git status:"
cd /home/ec2-user/dartford_backend
git log --oneline -3
echo ""

echo "2. Running processes:"
ps aux | grep dotnet | grep -v grep
echo ""

echo "3. Check if YouTube field exists in current code:"
grep -n "YouTube" Models/Influencer.cs || echo "YouTube field NOT found!"
echo ""

echo "4. Check API logs:"
tail -20 app.log
EOF