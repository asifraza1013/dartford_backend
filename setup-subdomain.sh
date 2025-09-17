#!/bin/bash

# Script to set up api.inflan.com subdomain with SSL
# This needs to be run on the EC2 instance

echo "🌐 Setting up api.inflan.com subdomain..."

# Install nginx and certbot if not already installed
echo "Installing nginx and certbot..."
sudo yum update -y
sudo amazon-linux-extras install nginx1 -y
sudo yum install certbot python3-certbot-nginx -y

# Create nginx configuration
echo "Creating nginx configuration..."
sudo tee /etc/nginx/conf.d/api.inflan.conf > /dev/null << 'EOF'
server {
    listen 80;
    server_name api.inflan.com;

    location / {
        proxy_pass http://localhost:10000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        
        # .NET Core specific headers
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Port $server_port;
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
}
EOF

# Test nginx configuration
echo "Testing nginx configuration..."
sudo nginx -t

# Start and enable nginx
echo "Starting nginx..."
sudo systemctl start nginx
sudo systemctl enable nginx

echo ""
echo "✅ Nginx proxy configured!"
echo ""
echo "📋 Next steps:"
echo ""
echo "1. Add DNS A record for api.inflan.com pointing to: 13.40.44.150"
echo "   - Go to your DNS provider (where inflan.com is registered)"
echo "   - Add an A record: api.inflan.com → 13.40.44.150"
echo ""
echo "2. Once DNS is propagated (5-30 minutes), run this to get SSL certificate:"
echo "   sudo certbot --nginx -d api.inflan.com"
echo ""
echo "3. Update security group to allow ports 80 and 443:"
echo "   - AWS Console → EC2 → Security Groups → sg-0b92bd396151da397"
echo "   - Add inbound rules for HTTP (80) and HTTPS (443)"
echo ""
echo "Current status:"
echo "- Nginx proxy: ✅ Configured (HTTP only)"
echo "- SSL certificate: ⏳ Pending (waiting for DNS)"
echo "- API endpoint: http://api.inflan.com (after DNS update)"