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
/// Sweeps PENDING payment milestones and sends pre-due reminders at the
/// configured day windows (default: 7, 3, 1 days before due date) plus an
/// overdue notice when a milestone's due date passes. Each reminder fires
/// once per milestone — tracked via per-window timestamps on the row.
/// </summary>
public class MilestoneReminderBackgroundService : BackgroundService
{
    private readonly ILogger<MilestoneReminderBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MilestoneReminderConfig _config;
    private Timer? _timer;

    public MilestoneReminderBackgroundService(
        ILogger<MilestoneReminderBackgroundService> logger,
        IServiceProvider serviceProvider,
        IOptions<MilestoneReminderConfig> config)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Milestone reminder background service is disabled");
            return;
        }

        _logger.LogInformation("Milestone reminder background service is starting");

        try
        {
            await ProcessRemindersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial milestone reminder processing");
        }

        var interval = TimeSpan.FromHours(_config.IntervalHours);
        _logger.LogInformation(
            "Next milestone reminder sweep scheduled in {Hours} hours",
            interval.TotalHours);

        _timer = new Timer(
            _ =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessRemindersAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in timer-triggered milestone reminder processing");
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
            _logger.LogInformation("Milestone reminder background service is stopping");
        }
    }

    public async Task ProcessRemindersAsync()
    {
        _logger.LogInformation("Starting milestone reminder sweep at {Time}", DateTime.UtcNow);

        using var scope = _serviceProvider.CreateScope();
        var milestoneRepo = scope.ServiceProvider.GetRequiredService<IPaymentMilestoneRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        // Pull PENDING milestones inside the widest reminder window (so we cover
        // every threshold and overdue flips in one query).
        var horizon = now.AddDays(_config.WindowDays);
        var milestones = await milestoneRepo.GetPendingMilestonesAsync(horizon);

        int sent = 0;
        int flipped = 0;
        int errors = 0;

        foreach (var milestone in milestones)
        {
            try
            {
                var campaign = milestone.Campaign;
                if (campaign == null) continue;

                var brand = await userRepo.GetById(campaign.BrandId);
                if (brand == null || string.IsNullOrWhiteSpace(brand.Email)) continue;

                var brandCurrency = brand.Currency?.ToUpper() ?? "NGN";
                var totalAmountInPence = milestone.AmountInPence + milestone.PlatformFeeInPence;

                var dueDate = milestone.DueDate;
                var hoursUntilDue = (dueDate - now).TotalHours;

                // Branch 1: due date has passed — flip to OVERDUE and emit a single
                // overdue notice (only if we haven't already sent one).
                if (hoursUntilDue <= 0)
                {
                    if (milestone.Status == (int)MilestoneStatus.PENDING)
                    {
                        milestone.Status = (int)MilestoneStatus.OVERDUE;
                        flipped++;
                    }

                    if (milestone.OverdueNoticeSentAt == null)
                    {
                        await DispatchAsync(
                            milestone, campaign, brand, brandCurrency,
                            totalAmountInPence, daysUntilDue: -1,
                            notificationService, emailService);
                        milestone.OverdueNoticeSentAt = now;
                        sent++;
                    }

                    await milestoneRepo.UpdateAsync(milestone);
                    continue;
                }

                // Branch 2: still pending. Pick the tightest unsent reminder window
                // that the milestone currently sits in.
                var daysUntilDue = (int)Math.Ceiling(hoursUntilDue / 24.0);

                if (daysUntilDue <= 1 && milestone.Reminder1DaySentAt == null)
                {
                    await DispatchAsync(
                        milestone, campaign, brand, brandCurrency,
                        totalAmountInPence, daysUntilDue: 1,
                        notificationService, emailService);
                    milestone.Reminder1DaySentAt = now;
                    await milestoneRepo.UpdateAsync(milestone);
                    sent++;
                }
                else if (daysUntilDue <= 3 && milestone.Reminder3DaysSentAt == null)
                {
                    await DispatchAsync(
                        milestone, campaign, brand, brandCurrency,
                        totalAmountInPence, daysUntilDue: 3,
                        notificationService, emailService);
                    milestone.Reminder3DaysSentAt = now;
                    await milestoneRepo.UpdateAsync(milestone);
                    sent++;
                }
                else if (daysUntilDue <= 7 && milestone.Reminder7DaysSentAt == null)
                {
                    await DispatchAsync(
                        milestone, campaign, brand, brandCurrency,
                        totalAmountInPence, daysUntilDue: 7,
                        notificationService, emailService);
                    milestone.Reminder7DaysSentAt = now;
                    await milestoneRepo.UpdateAsync(milestone);
                    sent++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex,
                    "Error processing reminder for milestone {MilestoneId}",
                    milestone.Id);
            }

            // Light throttle so we don't hammer SMTP if there's a backlog.
            if (_config.DelayBetweenSendsMs > 0)
                await Task.Delay(_config.DelayBetweenSendsMs);
        }

        _logger.LogInformation(
            "Milestone reminder sweep complete. Sent={Sent}, Flipped={Flipped}, Errors={Errors}",
            sent, flipped, errors);
    }

    private static async Task DispatchAsync(
        PaymentMilestone milestone,
        Campaign campaign,
        User brand,
        string currency,
        long totalAmountInPence,
        int daysUntilDue,
        INotificationService notificationService,
        IEmailService emailService)
    {
        var projectName = campaign.ProjectName ?? $"Campaign #{campaign.Id}";

        // In-app notification.
        var title = daysUntilDue <= 0
            ? "Milestone payment overdue"
            : $"Milestone payment due in {daysUntilDue} day{(daysUntilDue == 1 ? "" : "s")}";

        var formattedAmount = FormatAmount(totalAmountInPence, currency);
        var message = daysUntilDue <= 0
            ? $"Milestone {milestone.MilestoneNumber} ({formattedAmount}) for \"{projectName}\" is overdue. Please complete the payment to keep the campaign active."
            : $"Milestone {milestone.MilestoneNumber} ({formattedAmount}) for \"{projectName}\" is due on {milestone.DueDate:MMM d, yyyy}.";

        await notificationService.CreateNotificationAsync(new CreateNotificationRequest
        {
            UserId = brand.Id,
            Type = NotificationType.Payment,
            Title = title,
            Message = message,
            ReferenceId = campaign.Id,
            ReferenceType = "campaign"
        });

        // Email — best-effort, swallow errors so a failed send doesn't block
        // the rest of the sweep.
        try
        {
            await emailService.SendMilestoneReminderAsync(
                brand.Email!,
                brand.Name ?? string.Empty,
                campaign.Id,
                projectName,
                milestone.MilestoneNumber,
                milestone.AmountInPence,
                milestone.PlatformFeeInPence,
                currency,
                milestone.DueDate,
                daysUntilDue);
        }
        catch
        {
            // EmailService logs the underlying error; reminder was already
            // recorded as an in-app notification, so we still consider the
            // window served and don't retry on the next sweep.
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

public class MilestoneReminderConfig
{
    public const string SectionName = "MilestoneReminder";

    /// <summary>Enable/disable the reminder sweep.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often to scan PENDING milestones (in hours).</summary>
    public double IntervalHours { get; set; } = 6;

    /// <summary>How far in the future to look for upcoming milestones (in days).</summary>
    public int WindowDays { get; set; } = 7;

    /// <summary>Delay between successive sends to avoid SMTP rate limits.</summary>
    public int DelayBetweenSendsMs { get; set; } = 250;
}
