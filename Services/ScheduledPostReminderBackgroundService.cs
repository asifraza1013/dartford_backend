using inflan_api.DTOs;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace inflan_api.Services;

/// <summary>
/// Sweeps <see cref="ScheduledPost"/> rows whose ScheduledAt is within the
/// configured lead window (default 30 minutes) and dispatches a "post going
/// live soon" reminder to the owning influencer once. Each post is reminded
/// exactly once via the ReminderSentAt column — the next sweep skips any
/// row where it's set, so the user gets a single notification regardless of
/// how often the timer fires.
///
/// Same cadence as <c>MilestonePaymentBackgroundService</c> (5-minute default)
/// so a 30-minute window has at least 5 chances to fire before the post goes
/// live.
/// </summary>
public class ScheduledPostReminderBackgroundService : BackgroundService
{
    private readonly ILogger<ScheduledPostReminderBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ScheduledPostReminderConfig _config;
    private Timer? _timer;

    public ScheduledPostReminderBackgroundService(
        ILogger<ScheduledPostReminderBackgroundService> logger,
        IServiceProvider serviceProvider,
        IOptions<ScheduledPostReminderConfig> config)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Scheduled-post reminder background service is disabled");
            return;
        }

        _logger.LogInformation(
            "Scheduled-post reminder background service is starting (sweep every {Min} min, lead {Lead} min)",
            _config.IntervalMinutes, _config.ReminderLeadMinutes);

        try
        {
            await ProcessUpcomingPostsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial scheduled-post reminder sweep");
        }

        var interval = TimeSpan.FromMinutes(_config.IntervalMinutes);
        _timer = new Timer(
            _ =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessUpcomingPostsAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in timer-triggered scheduled-post reminder sweep");
                    }
                });
            },
            null,
            interval,
            interval);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Scheduled-post reminder background service is stopping");
        }
    }

    public async Task ProcessUpcomingPostsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InflanDBContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var now = DateTime.UtcNow;
        var leadWindow = now.AddMinutes(_config.ReminderLeadMinutes);

        // Posts to remind: still scheduled (status=1), in the future, within
        // the lead window, and not previously reminded. Selecting from the
        // DbContext directly keeps the filter in SQL — the table is small per
        // influencer but we'll still want this to scale across all users.
        var dueReminders = await db.ScheduledPosts
            .Include(sp => sp.Campaign)
            .Where(sp =>
                sp.ReminderSentAt == null &&
                sp.Status == 1 &&
                sp.ScheduledAt > now &&
                sp.ScheduledAt <= leadWindow)
            .ToListAsync();

        if (dueReminders.Count == 0)
        {
            return; // quiet path — most sweeps have nothing to do
        }

        _logger.LogInformation(
            "Scheduled-post reminder sweep: {Count} post(s) inside the {Lead}-min lead window",
            dueReminders.Count, _config.ReminderLeadMinutes);

        int sent = 0;
        int errors = 0;

        foreach (var post in dueReminders)
        {
            try
            {
                var influencer = await userRepo.GetById(post.InfluencerId);
                if (influencer == null)
                {
                    _logger.LogWarning(
                        "Skipping scheduled-post reminder for post {PostId}: influencer {InfluencerId} not found",
                        post.Id, post.InfluencerId);
                    // Mark as sent so we don't keep retrying a dead reference.
                    post.ReminderSentAt = now;
                    continue;
                }

                var minutesUntilLive = Math.Max(0, (int)Math.Round((post.ScheduledAt - now).TotalMinutes));
                var projectName = post.Campaign?.ProjectName;
                var titleSnippet = string.IsNullOrWhiteSpace(post.Title) ? "your scheduled post" : post.Title;

                // In-app notification — always emitted.
                await notificationService.CreateNotificationAsync(new CreateNotificationRequest
                {
                    UserId = post.InfluencerId,
                    Type = NotificationType.CampaignUpdate,
                    Title = $"Going live in {minutesUntilLive} min",
                    Message =
                        $"\"{titleSnippet}\"" +
                        (string.IsNullOrWhiteSpace(projectName) ? "" : $" for \"{projectName}\"") +
                        $" is scheduled at {post.ScheduledAt:HH:mm 'UTC'}.",
                    ReferenceId = post.CampaignId,
                    ReferenceType = "scheduledPost"
                });

                // Email — best effort. A failure here still marks the row as
                // reminded so we don't double-notify the in-app side later.
                if (!string.IsNullOrWhiteSpace(influencer.Email))
                {
                    try
                    {
                        await emailService.SendScheduledPostReminderAsync(
                            influencer.Email,
                            influencer.Name ?? string.Empty,
                            post.CampaignId,
                            projectName,
                            titleSnippet,
                            post.ScheduledAt,
                            minutesUntilLive,
                            post.Platforms);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Email reminder failed for scheduled post {PostId}; in-app notification was still recorded",
                            post.Id);
                    }
                }

                post.ReminderSentAt = now;
                sent++;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex,
                    "Error processing reminder for scheduled post {PostId}",
                    post.Id);
            }

            if (_config.DelayBetweenSendsMs > 0)
                await Task.Delay(_config.DelayBetweenSendsMs);
        }

        // Single SaveChanges for the whole sweep — every post we touched had
        // ReminderSentAt set in-memory above.
        await db.SaveChangesAsync();

        _logger.LogInformation(
            "Scheduled-post reminder sweep complete. Sent={Sent}, Errors={Errors}",
            sent, errors);
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }
}

public class ScheduledPostReminderConfig
{
    public const string SectionName = "ScheduledPostReminder";

    /// <summary>Enable/disable the sweep.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often to look for upcoming posts (in minutes).</summary>
    public double IntervalMinutes { get; set; } = 5;

    /// <summary>How far ahead of ScheduledAt to send the reminder (in minutes).</summary>
    public int ReminderLeadMinutes { get; set; } = 30;

    /// <summary>Throttle between sends to avoid SMTP rate-limits.</summary>
    public int DelayBetweenSendsMs { get; set; } = 200;
}
