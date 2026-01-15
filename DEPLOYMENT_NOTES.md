# Deployment Notes

## Production Server Configuration Required

After deploying the latest code to production, you need to update the `appsettings.json` file on your production server to add the `MilestonePayment` configuration section.

### Steps:

1. SSH into your production server:
   ```bash
   ssh your-server
   ```

2. Navigate to your application directory and edit appsettings.json:
   ```bash
   cd /path/to/your/app
   nano appsettings.json
   ```

3. Add the following section after the `FollowerSync` configuration:
   ```json
   "MilestonePayment": {
     "Enabled": true,
     "IntervalHours": 24,
     "DelayBetweenPaymentsMs": 2000
   },
   ```

4. Save the file (Ctrl+X, then Y, then Enter in nano)

5. Restart your application (the deploy script will handle this)

### What This Fix Does:

This update fixes the `TaskCanceledException` that was causing the `MilestonePaymentBackgroundService` to crash and stop the host. The changes include:

1. **Better error handling**: Wrapped the initial milestone processing in try-catch to prevent crashes
2. **Fixed Timer callback**: Changed from async lambda to fire-and-forget Task.Run pattern
3. **Graceful shutdown**: Added proper handling of TaskCanceledException during service shutdown

### Verification:

After deployment and restart, check the application logs to confirm:
- The service starts successfully
- No `TaskCanceledException` errors
- The admin APIs are visible in Swagger UI at `https://api.inflan.com/swagger`

### Admin APIs Available:

Once deployed, you should see these new endpoints in Swagger:
- GET /api/admin/dashboard/stats
- GET /api/admin/dashboard/campaign-breakdown
- GET /api/admin/dashboard/payment-volume
- GET /api/admin/dashboard/export
- GET /api/admin/users
- GET /api/admin/users/{id}
- PATCH /api/admin/users/{id}/status
- GET /api/admin/campaigns
- GET /api/admin/commission/stats
- GET /api/admin/commission/report
- GET /api/admin/withdrawals
