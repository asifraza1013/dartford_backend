using inflan_api.DTOs;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace inflan_api.Services.Payment;

/// <summary>
/// Campaign-level counterpart to <see cref="MilestoneReminderBackgroundService"/>: covers the
/// portion of a campaign's balance that isn't tracked by any PaymentMilestone row — i.e.
/// ONE_TIME campaigns (which never have milestones at all) and any gap left by a MILESTONE
/// campaign whose scheduled milestones don't add up to the full total. Uses
/// <see cref="Campaign.CampaignEndDate"/> as the deadline, mirroring the fallback already used
/// for the UI's synthetic "unscheduled payment" row (<see cref="MilestoneService.GetBrandCampaignsWithMilestonesAsync"/>).
///
/// Sends the same 7/3/1-day-before reminders as the milestone service, then on the overdue
/// branch flags the campaign (<see cref="Campaign.IsPaymentOverdue"/>) for dashboard visibility
/// and re-sends an escalating reminder every <see cref="CampaignPaymentDeadlineConfig.EscalationIntervalDays"/>
/// days for as long as the balance remains unpaid.
/// </summary>
public class CampaignPaymentDeadlineBackgroundService : BackgroundService
{
    private readonly ILogger<CampaignPaymentDeadlineBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly CampaignPaymentDeadlineConfig _config;
    private Timer? _timer;

    public CampaignPaymentDeadlineBackgroundService(
        ILogger<CampaignPaymentDeadlineBackgroundService> logger,
        IServiceProvider serviceProvider,
        IOptions<CampaignPaymentDeadlineConfig> config)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Campaign payment-deadline background service is disabled");
            return;
        }

        _logger.LogInformation("Campaign payment-deadline background service is starting");

        try
        {
            await ProcessAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial campaign payment-deadline processing");
        }

        var interval = TimeSpan.FromHours(_config.IntervalHours);
        _logger.LogInformation(
            "Next campaign payment-deadline sweep scheduled in {Hours} hours",
            interval.TotalHours);

        _timer = new Timer(
            _ =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in timer-triggered campaign payment-deadline processing");
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
            _logger.LogInformation("Campaign payment-deadline background service is stopping");
        }
    }

    public async Task ProcessAsync()
    {
        _logger.LogInformation("Starting campaign payment-deadline sweep at {Time}", DateTime.UtcNow);

        using var scope = _serviceProvider.CreateScope();
        var campaignRepo = scope.ServiceProvider.GetRequiredService<ICampaignRepository>();
        var milestoneRepo = scope.ServiceProvider.GetRequiredService<IPaymentMilestoneRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var settingsService = scope.ServiceProvider.GetRequiredService<IPlatformSettingsService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var campaigns = await campaignRepo.GetWithOutstandingBalanceAsync();
        var brandFeePercent = await settingsService.GetBrandPlatformFeePercentAsync();

        int sent = 0;
        int errors = 0;

        foreach (var campaign in campaigns)
        {
            try
            {
                var milestones = await milestoneRepo.GetByCampaignIdAsync(campaign.Id);
                var remaining = Math.Max(0, campaign.TotalAmountInPence - campaign.PaidAmountInPence);
                var coveredByTrackedMilestones = milestones
                    .Where(m => m.Status == (int)MilestoneStatus.PENDING || m.Status == (int)MilestoneStatus.OVERDUE)
                    .Sum(m => m.AmountInPence);
                var uncovered = Math.Max(0, remaining - coveredByTrackedMilestones);

                // Nothing left for this sweep to chase — either fully paid, or the whole
                // balance is already tracked by real milestone rows (MilestoneReminderBackgroundService
                // owns those).
                if (uncovered <= 0) continue;

                var brand = campaign.Brand ?? await userRepo.GetById(campaign.BrandId);
                if (brand == null || string.IsNullOrWhiteSpace(brand.Email)) continue;
                var influencer = campaign.Influencer ?? await userRepo.GetById(campaign.InfluencerId);

                var brandCurrency = brand.Currency?.ToUpper() ?? "NGN";
                var platformFeeInPence = (long)(uncovered * brandFeePercent / 100m);
                var dueDate = campaign.CampaignEndDate.ToDateTime(TimeOnly.MinValue);
                var hoursUntilDue = (dueDate - now).TotalHours;

                // Branch 1: end date has passed. IsPaymentOverdue (computed on the model from
                // this same uncovered balance) already reflects that — just decide whether a
                // notice is due: the first one, or a weekly repeat.
                if (hoursUntilDue <= 0)
                {
                    var dueForNotice = campaign.EndDateLastOverdueNoticeSentAt == null
                        || (now - campaign.EndDateLastOverdueNoticeSentAt.Value).TotalDays >= _config.EscalationIntervalDays;

                    if (dueForNotice)
                    {
                        await DispatchAsync(
                            campaign, brand, influencer, brandCurrency,
                            uncovered, platformFeeInPence, dueDate, daysUntilDue: -1,
                            notificationService, emailService);
                        campaign.EndDateLastOverdueNoticeSentAt = now;
                        await campaignRepo.Update(campaign);
                        sent++;
                    }

                    continue;
                }

                // Branch 2: still ahead of the end date. Pick the tightest unsent reminder
                // tier — EndDateLastReminderDaysSent only ever tightens (7 -> 3 -> 1), so a
                // smaller previously-sent value means this tier is already covered.
                var daysUntilDue = (int)Math.Ceiling(hoursUntilDue / 24.0);
                int? tier = daysUntilDue <= 1 ? 1 : daysUntilDue <= 3 ? 3 : daysUntilDue <= 7 ? 7 : null;
                var alreadyCovered = tier.HasValue
                    && campaign.EndDateLastReminderDaysSent.HasValue
                    && campaign.EndDateLastReminderDaysSent.Value <= tier.Value;

                if (tier.HasValue && !alreadyCovered)
                {
                    await DispatchAsync(
                        campaign, brand, influencer, brandCurrency,
                        uncovered, platformFeeInPence, dueDate, daysUntilDue: tier.Value,
                        notificationService, emailService);
                    campaign.EndDateLastReminderDaysSent = tier.Value;
                    await campaignRepo.Update(campaign);
                    sent++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex,
                    "Error processing payment-deadline reminder for campaign {CampaignId}",
                    campaign.Id);
            }

            if (_config.DelayBetweenSendsMs > 0)
                await Task.Delay(_config.DelayBetweenSendsMs);
        }

        _logger.LogInformation(
            "Campaign payment-deadline sweep complete. Sent={Sent}, Errors={Errors}",
            sent, errors);
    }

    private static async Task DispatchAsync(
        Campaign campaign,
        User brand,
        User? influencer,
        string currency,
        long amountInPence,
        long platformFeeInPence,
        DateTime dueDate,
        int daysUntilDue,
        INotificationService notificationService,
        IEmailService emailService)
    {
        var projectName = campaign.ProjectName ?? $"Campaign #{campaign.Id}";
        var formattedAmount = FormatAmount(amountInPence + platformFeeInPence, currency);

        // ---- Brand: in-app notification + "please pay" email --------------------
        var brandTitle = daysUntilDue <= 0
            ? "Campaign payment overdue"
            : $"Campaign payment due in {daysUntilDue} day{(daysUntilDue == 1 ? "" : "s")}";

        var brandMessage = daysUntilDue <= 0
            ? $"\"{projectName}\" ended on {dueDate:MMM d, yyyy} with {formattedAmount} still outstanding. Please complete the payment."
            : $"\"{projectName}\" ends on {dueDate:MMM d, yyyy} with {formattedAmount} still outstanding.";

        await notificationService.CreateNotificationAsync(new CreateNotificationRequest
        {
            UserId = brand.Id,
            Type = NotificationType.Payment,
            Title = brandTitle,
            Message = brandMessage,
            ReferenceId = campaign.Id,
            ReferenceType = "campaign"
        });

        try
        {
            await emailService.SendMilestoneReminderAsync(
                brand.Email!,
                brand.Name ?? string.Empty,
                campaign.Id,
                projectName,
                milestoneNumber: 0,
                amountInPence,
                platformFeeInPence,
                currency,
                dueDate,
                daysUntilDue);
        }
        catch
        {
            // EmailService logs the underlying error; reminder was already recorded as an
            // in-app notification, so we still consider the window served.
        }

        // ---- Influencer: informational in-app notification + email --------------
        if (influencer != null)
        {
            var infTitle = daysUntilDue <= 0
                ? "Brand payment overdue"
                : $"Brand payment due in {daysUntilDue} day{(daysUntilDue == 1 ? "" : "s")}";

            var infMessage = daysUntilDue <= 0
                ? $"{brand.Name}'s payment for \"{projectName}\" ({formattedAmount}) is overdue. You'll receive your payout once it's completed."
                : $"{brand.Name}'s payment for \"{projectName}\" ({formattedAmount}) is due on {dueDate:MMM d, yyyy}.";

            await notificationService.CreateNotificationAsync(new CreateNotificationRequest
            {
                UserId = influencer.Id,
                Type = NotificationType.Payment,
                Title = infTitle,
                Message = infMessage,
                ReferenceId = campaign.Id,
                ReferenceType = "campaign"
            });

            if (!string.IsNullOrWhiteSpace(influencer.Email))
            {
                try
                {
                    await emailService.SendMilestonePaymentNoticeToInfluencerAsync(
                        influencer.Email!,
                        influencer.Name ?? string.Empty,
                        brand.Name ?? string.Empty,
                        campaign.Id,
                        projectName,
                        milestoneNumber: 0,
                        amountInPence,
                        platformFeeInPence,
                        currency,
                        dueDate,
                        daysUntilDue);
                }
                catch
                {
                    // Best-effort; the in-app notification above already informed the influencer.
                }
            }
        }
    }

    private static string FormatAmount(long amountInPence, string currency)
    {
        var amount = amountInPence / 100.0m;
        return currency.ToUpper() switch
        {
            "GBP" => $"£{amount:N2}",
            "NGN" => $"₦{amount:N2}",
            _ => $"{currency} {amount:N2}"
        };
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }
}

public class CampaignPaymentDeadlineConfig
{
    public const string SectionName = "CampaignPaymentDeadline";

    /// <summary>Enable/disable the sweep.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often to scan outstanding-balance campaigns (in hours).</summary>
    public double IntervalHours { get; set; } = 6;

    /// <summary>Once overdue, how many days between repeat escalation emails.</summary>
    public double EscalationIntervalDays { get; set; } = 7;

    /// <summary>Delay between successive sends to avoid SMTP rate limits.</summary>
    public int DelayBetweenSendsMs { get; set; } = 250;
}
