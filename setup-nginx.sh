#!/bin/bash

# Setup nginx reverse proxy for api.inflan.com
echo "🌐 Setting up nginx for api.inflan.com..."

INSTANCE_IP="13.40.44.150"
KEY_PATH="$1"

if [ -z "$KEY_PATH" ]; then
    echo "Usage: ./setup-nginx.sh /path/to/inflan-api-key-pair.pem"
    exit 1
fi

ssh -o StrictHostKeyChecking=no -i "$KEY_PATH" ec2-user@$INSTANCE_IP << 'EOF'
#!/bin/bash

echo "Installing nginx..."
sudo yum update -y
sudo yum install nginx -y

# Create nginx configuration
echo "Creating nginx configuration for api.inflan.com..."
sudo tee /etc/nginx/conf.d/api.inflan.conf > /dev/null << 'NGINX_CONF'
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
NGINX_CONF

# Test nginx configuration
echo "Testing nginx configuration..."
sudo nginx -t

if [ $? -eq 0 ]; then
    # Start and enable nginx
    echo "Starting nginx..."
    sudo systemctl start nginx
    sudo systemctl enable nginx
    
    echo ""
    echo "✅ Nginx setup complete!"
    echo ""
    echo "Your API is now accessible at:"
    echo "🔗 http://api.inflan.com"
    echo "📊 http://api.inflan.com/swagger"
    echo ""
    echo "To get SSL certificate, run:"
    echo "sudo yum install certbot python3-certbot-nginx -y"
    echo "sudo certbot --nginx -d api.inflan.com"
else
    echo "❌ Nginx configuration test failed"
    exit 1
fi
EOF

echo ""
echo "✅ Nginx setup finished!"
echo "Test your subdomain: http://api.inflan.com"