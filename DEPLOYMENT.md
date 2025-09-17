# Inflan API Deployment Guide

## Current Status

✅ **API is running and accessible at:** http://13.40.44.150:10000  
✅ **Swagger UI:** http://13.40.44.150:10000/swagger  
✅ **Database:** PostgreSQL is running in Docker container  
✅ **Security Group:** Ports 80, 443, and 10000 are open  

## Infrastructure Details

- **EC2 Instance ID:** i-067ec62edc0021565
- **Instance IP:** 13.40.44.150
- **Region:** eu-west-2
- **Key Pair:** inflan-api-key-pair
- **Security Group:** sg-0b92bd396151da397

## Deployment Scripts

### 1. deploy.sh (Main deployment script)
```bash
./deploy.sh /path/to/inflan-api-key-pair.pem
```
This script uses SSH to connect to the EC2 instance and:
- Pulls latest code from GitHub
- Builds Docker image
- Restarts PostgreSQL and API containers
- Verifies deployment

### 2. ssm-deploy.sh (Alternative using AWS SSM)
```bash
./ssm-deploy.sh
```
Note: SSM agent needs to be installed on the EC2 instance for this to work.

### 3. setup-subdomain.sh (Run on EC2 instance)
This script sets up nginx reverse proxy for the subdomain.

## Setting Up api.inflan.com Subdomain

### Step 1: Add DNS Record
1. Go to your DNS provider (where inflan.com is managed)
2. Add an A record:
   - Name: `api`
   - Type: `A`
   - Value: `13.40.44.150`
   - TTL: 300 (5 minutes)

### Step 2: Install Nginx on EC2 (if not done)
SSH into the instance and run:
```bash
sudo yum update -y
sudo amazon-linux-extras install nginx1 -y
sudo yum install certbot python3-certbot-nginx -y
```

### Step 3: Configure Nginx
The setup-subdomain.sh script will create the nginx configuration automatically.

### Step 4: Get SSL Certificate
Once DNS is propagated (check with `nslookup api.inflan.com`):
```bash
sudo certbot --nginx -d api.inflan.com --email your-email@example.com --agree-tos
```

## Docker Configuration

### Current Docker Setup:
- **PostgreSQL:** Running on port 5432
- **API:** Running on port 8080 (mapped to 10000 on host)
- **Volumes:** wwwroot directory is mounted for file uploads

### Environment Variables:
- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection
- `Jwt__Key` - JWT secret key

## Deployment Workflow

1. **Make changes locally**
2. **Commit and push to GitHub**
   ```bash
   git add .
   git commit -m "Your changes"
   git push origin master
   ```

3. **Deploy to production**
   ```bash
   ./deploy.sh /path/to/your-key.pem
   ```

4. **Verify deployment**
   - Check API: http://13.40.44.150:10000/api/auth/getUser/1
   - Check Swagger: http://13.40.44.150:10000/swagger

## Database Access

PostgreSQL is running in Docker:
- Host: localhost (from within containers: host.docker.internal)
- Port: 5432
- Database: inflan_db
- Username: postgres
- Password: postgres123

To connect from local machine:
```bash
psql -h 13.40.44.150 -p 5432 -U postgres -d inflan_db
```

## Monitoring and Logs

View API logs:
```bash
ssh -i your-key.pem ec2-user@13.40.44.150
sudo docker logs inflan_api --tail 100 -f
```

View PostgreSQL logs:
```bash
sudo docker logs postgres --tail 100 -f
```

## Troubleshooting

### API not responding:
1. Check if containers are running: `sudo docker ps`
2. Check API logs: `sudo docker logs inflan_api`
3. Restart containers: `sudo docker restart inflan_api postgres`

### Database connection issues:
1. Ensure PostgreSQL is running: `sudo docker ps | grep postgres`
2. Check connection string in environment variables
3. Verify network connectivity between containers

### SSL Certificate issues:
1. Ensure DNS is properly configured: `nslookup api.inflan.com`
2. Check nginx configuration: `sudo nginx -t`
3. Renew certificate: `sudo certbot renew`

## Security Considerations

⚠️ **Important:**
1. Change the default PostgreSQL password in production
2. Update JWT secret key to a secure value
3. Remove hardcoded credentials from deploy scripts
4. Use environment variables or AWS Secrets Manager
5. Enable HTTPS once subdomain is configured
6. Consider using AWS RDS for production database

## Next Steps

1. ✅ API is running on port 10000
2. ✅ Security group allows ports 80, 443, 10000
3. ⏳ Configure DNS A record for api.inflan.com
4. ⏳ Set up SSL certificate with Let's Encrypt
5. ⏳ Update frontend to use https://api.inflan.com
6. ⏳ Set up monitoring and alerts
7. ⏳ Configure automated backups