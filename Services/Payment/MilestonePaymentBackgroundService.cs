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
/// Background service that automatically processes due milestone payments
/// Runs daily to check for milestones that are due and have auto-pay enabled
/// </summary>
public class MilestonePaymentBackgroundService : BackgroundService
{
    private readonly ILogger<MilestonePaymentBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MilestonePaymentConfig _config;
    private Timer? _timer;

    public MilestonePaymentBackgroundService(
        ILogger<MilestonePaymentBackgroundService> logger,
        IServiceProvider serviceProvider,
        IOptions<MilestonePaymentConfig> config)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Milestone auto-payment background service is disabled");
            return;
        }

        _logger.LogInformation("Milestone auto-payment background service is starting");

        // Run immediately on startup, then run daily
        await ProcessDueMilestonesAsync();

        // Set up daily timer
        _timer = new Timer(
            async _ => await ProcessDueMilestonesAsync(),
            null,
            TimeSpan.FromHours(_config.IntervalHours),
            TimeSpan.FromHours(_config.IntervalHours)
        );

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessDueMilestonesAsync()
    {
        _logger.LogInformation("Starting milestone auto-payment check at {Time}", DateTime.UtcNow);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var milestoneRepo = scope.ServiceProvider.GetRequiredService<IPaymentMilestoneRepository>();
            var campaignRepo = scope.ServiceProvider.GetRequiredService<ICampaignRepository>();
            var paymentMethodRepo = scope.ServiceProvider.GetRequiredService<IPaymentMethodRepository>();
            var paymentOrchestrator = scope.ServiceProvider.GetRequiredService<IPaymentOrchestrator>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            // Get all campaigns with auto-pay enabled
            var campaigns = await campaignRepo.GetAllWithAutoPay();
            var processedCount = 0;
            var errorCount = 0;
            var reminderCount = 0;

            foreach (var campaign in campaigns)
            {
                try
                {
                    // Get due milestones for this campaign (pending milestones where due date has passed)
                    var milestones = await milestoneRepo.GetByCampaignIdAsync(campaign.Id);
                    var dueMilestones = milestones
                        .Where(m => m.Status == (int)MilestoneStatus.PENDING ||
                                   m.Status == (int)MilestoneStatus.OVERDUE)
                        .Where(m => m.DueDate <= DateTime.UtcNow)
                        .OrderBy(m => m.DueDate)
                        .ToList();

                    if (!dueMilestones.Any())
                        continue;

                    // Determine brand's currency based on location
                    var brand = await userRepo.GetById(campaign.BrandId);
                    var brandLocation = brand?.Location?.ToUpper() ?? "NG";
                    var isUKBrand = brandLocation == "GB" || brandLocation == "UK";

                    // For UK brands (TrueLayer/GBP): Send payment reminder instead of auto-charging
                    // TrueLayer is Open Banking and requires user authentication for each payment (SCA)
                    if (isUKBrand)
                    {
                        foreach (var milestone in dueMilestones)
                        {
                            var formattedAmount = FormatAmount(milestone.AmountInPence, "GBP");
                            await notificationService.CreateNotificationAsync(new CreateNotificationRequest
                            {
                                UserId = campaign.BrandId,
                                Type = NotificationType.Payment,
                                Title = milestone.Status == (int)MilestoneStatus.OVERDUE
                                    ? "Overdue Payment Reminder"
                                    : "Payment Due Today",
                                Message = $"Milestone {milestone.MilestoneNumber} ({formattedAmount}) for campaign \"{campaign.ProjectName}\" is due. " +
                                    "Please complete the payment through your dashboard.",
                                ReferenceId = campaign.Id,
                                ReferenceType = "campaign"
                            });
                            reminderCount++;
                        }
                        _logger.LogInformation("Sent {Count} payment reminders to UK brand {BrandId} for campaign {CampaignId}",
                            dueMilestones.Count, campaign.BrandId, campaign.Id);
                        continue;
                    }

                    // For Nigerian brands (Paystack/NGN): Auto-charge using saved card
                    // Get the brand's default payment method (Paystack authorization)
                    var paymentMethods = await paymentMethodRepo.GetByUserIdAsync(campaign.BrandId);
                    var defaultPaymentMethod = paymentMethods
                        .Where(pm => pm.Gateway == "paystack" &&
                                    pm.IsReusable &&
                                    !string.IsNullOrEmpty(pm.AuthorizationCode))
                        .OrderByDescending(pm => pm.IsDefault)
                        .ThenByDescending(pm => pm.CreatedAt)
                        .FirstOrDefault();

                    if (defaultPaymentMethod == null)
                    {
                        _logger.LogWarning("No saved Paystack payment method for brand {BrandId} - sending reminder for campaign {CampaignId}",
                            campaign.BrandId, campaign.Id);

                        // Notify brand to add payment method or pay manually
                        foreach (var milestone in dueMilestones)
                        {
                            var formattedAmount = FormatAmount(milestone.AmountInPence, "NGN");
                            await notificationService.CreateNotificationAsync(new CreateNotificationRequest
                            {
                                UserId = campaign.BrandId,
                                Type = NotificationType.Payment,
                                Title = milestone.Status == (int)MilestoneStatus.OVERDUE
                                    ? "Overdue Payment - Action Required"
                                    : "Payment Due - Save Card for Auto-Pay",
                                Message = $"Milestone {milestone.MilestoneNumber} ({formattedAmount}) for campaign \"{campaign.ProjectName}\" is due. " +
                                    "Save your card to enable automatic payments, or pay manually through your dashboard.",
                                ReferenceId = campaign.Id,
                                ReferenceType = "campaign"
                            });
                            reminderCount++;
                        }
                        continue;
                    }

                    // Process each due milestone with Paystack auto-charge
                    foreach (var milestone in dueMilestones)
                    {
                        try
                        {
                            _logger.LogInformation("Auto-paying milestone {MilestoneId} for campaign {CampaignId}",
                                milestone.Id, campaign.Id);

                            var result = await paymentOrchestrator.ChargeRecurringPaymentAsync(
                                milestone.Id,
                                defaultPaymentMethod.Id);

                            if (result.Success)
                            {
                                processedCount++;
                                _logger.LogInformation("Successfully auto-paid milestone {MilestoneId}", milestone.Id);

                                // Notify brand of successful payment
                                await notificationService.CreateNotificationAsync(new CreateNotificationRequest
                                {
                                    UserId = campaign.BrandId,
                                    Type = NotificationType.Payment,
                                    Title = "Auto-Payment Successful",
                                    Message = $"Milestone {milestone.MilestoneNumber} for campaign \"{campaign.ProjectName}\" has been automatically paid.",
                                    ReferenceId = campaign.Id,
                                    ReferenceType = "campaign"
                                });
                            }
                            else
                            {
                                errorCount++;
                                _logger.LogWarning("Failed to auto-pay milestone {MilestoneId}: {Error}",
                                    milestone.Id, result.ErrorMessage);

                                // Notify brand of failed payment
                                await notificationService.CreateNotificationAsync(new CreateNotificationRequest
                                {
                                    UserId = campaign.BrandId,
                                    Type = NotificationType.Payment,
                                    Title = "Auto-Payment Failed",
                                    Message = $"Failed to process auto-payment for milestone {milestone.MilestoneNumber} of \"{campaign.ProjectName}\". Error: {result.ErrorMessage}",
                                    ReferenceId = campaign.Id,
                                    ReferenceType = "campaign"
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            _logger.LogError(ex, "Error processing auto-payment for milestone {MilestoneId}", milestone.Id);
                        }

                        // Add delay between payments to avoid rate limiting
                        await Task.Delay(_config.DelayBetweenPaymentsMs);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing campaign {CampaignId} for auto-payment", campaign.Id);
                }
            }

            _logger.LogInformation("Milestone auto-payment check completed. Processed: {Processed}, Errors: {Errors}, Reminders: {Reminders}",
                processedCount, errorCount, reminderCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during milestone auto-payment processing");
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

public class MilestonePaymentConfig
{
    public const string SectionName = "MilestonePayment";

    /// <summary>
    /// Enable/disable the auto-payment background service
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How often to check for due milestones (in hours)
    /// </summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>
    /// Delay between processing each payment to avoid rate limiting (in milliseconds)
    /// </summary>
    public int DelayBetweenPaymentsMs { get; set; } = 2000;
}
