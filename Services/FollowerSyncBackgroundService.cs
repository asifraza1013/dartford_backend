using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using inflan_api.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace inflan_api.Services
{
    public class FollowerSyncBackgroundService : BackgroundService
    {
        private readonly ILogger<FollowerSyncBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly FollowerSyncConfig _config;
        private Timer? _timer;

        public FollowerSyncBackgroundService(
            ILogger<FollowerSyncBackgroundService> logger,
            IServiceProvider serviceProvider,
            IOptions<FollowerSyncConfig> config)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _config = config.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.Enabled)
            {
                _logger.LogInformation("Follower sync background service is disabled");
                return;
            }

            _logger.LogInformation("Follower sync background service is starting");

            // Calculate time until next sync
            var nextRunTime = CalculateNextRunTime();
            var delay = nextRunTime - DateTime.UtcNow;

            if (delay > TimeSpan.Zero)
            {
                _logger.LogInformation($"Next follower sync scheduled for: {nextRunTime:yyyy-MM-dd HH:mm:ss} UTC");
                await Task.Delay(delay, stoppingToken);
            }

            // Set up weekly timer
            _timer = new Timer(
                async _ => await SyncAllInfluencersAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromDays(7) // Run weekly
            );

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private DateTime CalculateNextRunTime()
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(_config.DayOfWeek - (int)now.DayOfWeek);
            
            if (nextRun <= now)
                nextRun = nextRun.AddDays(7);
            
            return nextRun.AddHours(_config.HourUtc);
        }

        private async Task SyncAllInfluencersAsync()
        {
            _logger.LogInformation("Starting weekly follower sync for all influencers");

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var influencerService = scope.ServiceProvider.GetRequiredService<IInfluencerService>();
                    var followerCountService = scope.ServiceProvider.GetRequiredService<IFollowerCountService>();

                    // Get all influencers
                    var influencers = await influencerService.GetAllInfluencers();
                    var totalInfluencers = influencers.Count();
                    var syncedCount = 0;
                    var errorCount = 0;

                    _logger.LogInformation($"Found {totalInfluencers} influencers to sync");

                    foreach (var influencerModel in influencers)
                    {
                        try
                        {
                            var influencer = await influencerService.GetInfluencerBasicByUserId(influencerModel.UserId);
                            if (influencer == null) continue;

                            // Get updated follower counts
                            var followerResults = await followerCountService.GetAllPlatformFollowersAsync(
                                instagramUsername: influencer.Instagram,
                                youtubeChannelId: influencer.YouTube,
                                tiktokUsername: influencer.TikTok,
                                facebookUsername: influencer.Facebook
                            );

                            // Update follower counts
                            var hasUpdates = false;
                            
                            if (followerResults.ContainsKey("Instagram") && followerResults["Instagram"].Success)
                            {
                                influencer.InstagramFollower = (int)followerResults["Instagram"].Followers;
                                hasUpdates = true;
                            }
                            
                            if (followerResults.ContainsKey("YouTube") && followerResults["YouTube"].Success)
                            {
                                influencer.YouTubeFollower = (int)followerResults["YouTube"].Followers;
                                hasUpdates = true;
                            }
                            
                            if (followerResults.ContainsKey("TikTok") && followerResults["TikTok"].Success)
                            {
                                influencer.TikTokFollower = (int)followerResults["TikTok"].Followers;
                                hasUpdates = true;
                            }
                            
                            if (followerResults.ContainsKey("Facebook") && followerResults["Facebook"].Success)
                            {
                                influencer.FacebookFollower = (int)followerResults["Facebook"].Followers;
                                hasUpdates = true;
                            }
                            
                            if (hasUpdates)
                            {
                                await influencerService.UpdateInfluencer(influencerModel.UserId, influencer);
                                syncedCount++;
                            }

                            // Add delay between requests to avoid rate limiting
                            await Task.Delay(_config.DelayBetweenSyncsMs);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error syncing influencer {influencerModel.UserId}");
                            errorCount++;
                        }
                    }

                    _logger.LogInformation($"Weekly sync completed. Synced: {syncedCount}, Errors: {errorCount}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during weekly follower sync");
            }
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }
    }

    public class FollowerSyncConfig
    {
        public const string SectionName = "FollowerSync";
        
        public bool Enabled { get; set; } = false;
        public int DayOfWeek { get; set; } = 0; // Sunday = 0
        public int HourUtc { get; set; } = 2; // 2 AM UTC
        public int DelayBetweenSyncsMs { get; set; } = 1000; // 1 second between each influencer
    }
}