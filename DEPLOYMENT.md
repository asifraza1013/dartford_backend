# Inflan API Deployment Guide

## Current Status

✅ **API URL:** https://api.inflan.com  
✅ **Swagger UI:** https://api.inflan.com/swagger  
✅ **SSL:** Enabled with Let's Encrypt certificate  
✅ **Auto-renewal:** Certificate renews automatically  

## Infrastructure

- **EC2 Instance:** i-067ec62edc0021565
- **IP Address:** 13.40.44.150
- **Region:** eu-west-2
- **Key Pair:** inflan-api-key-pair

## Deployment

### Simple deployment process:

1. **Make your changes locally**
2. **Commit and push to GitHub:**
   ```bash
   git add .
   git commit -m "Your changes"
   git push origin master
   ```
3. **Deploy to production:**
   ```bash
   ./deploy.sh /path/to/inflan-api-key-pair.pem
   ```

That's it! The script will:
- Stop the current API
- Pull latest code from GitHub
- Build the application
- Start the API
- Verify deployment

## Server Details

- **Application:** Running with `dotnet run` on port 10000
- **Web Server:** Nginx reverse proxy with SSL
- **Database:** PostgreSQL
- **No Docker:** Application runs directly on the server

## Monitoring

### Check API status:
```bash
ssh -i your-key.pem ec2-user@13.40.44.150
tail -f /home/ec2-user/dartford_backend/app.log
```

### Check if API is running:
```bash
ps aux | grep dotnet
```

### Restart API manually if needed:
```bash
cd /home/ec2-user/dartford_backend
pkill -f "dotnet run"
nohup dotnet run --urls="http://0.0.0.0:10000" --environment="Production" > app.log 2>&1 &
```

## Troubleshooting

If deployment fails:
1. SSH into the server
2. Check logs: `tail -f /home/ec2-user/dartford_backend/app.log`
3. Check if process is running: `ps aux | grep dotnet`
4. Try manual restart using commands above