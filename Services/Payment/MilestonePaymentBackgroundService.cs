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

        // Run immediately on startup
        try
        {
            await ProcessDueMilestonesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial milestone auto-payment processing");
        }

        // Use configurable interval from settings
        var intervalTimeSpan = TimeSpan.FromHours(_config.IntervalHours);
        var nextRun = DateTime.UtcNow.Add(intervalTimeSpan);

        _logger.LogInformation("Next auto-payment run scheduled for {NextRun} (in {Hours} hours, {Minutes} minutes)",
            nextRun, intervalTimeSpan.TotalHours, intervalTimeSpan.TotalMinutes);

        // Set up timer based on configured interval
        _timer = new Timer(
            _ =>
            {
                // Fire and forget - run in background without blocking
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessDueMilestonesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in timer-triggered milestone processing");
                    }
                });
            },
            null,
            intervalTimeSpan, // First run after configured interval
            intervalTimeSpan // Then repeat at the same interval
        );

        // Keep the service running
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Milestone auto-payment background service is stopping");
        }
    }

    public async Task ProcessDueMilestonesAsync()
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
                    // Get milestones due TODAY (pending milestones where due date is today)
                    var milestones = await milestoneRepo.GetByCampaignIdAsync(campaign.Id);
                    var today = DateTime.UtcNow.Date;
                    var dueMilestones = milestones
                        .Where(m => m.Status == (int)MilestoneStatus.PENDING ||
                                   m.Status == (int)MilestoneStatus.OVERDUE)
                        .Where(m => m.DueDate.Date == today) // Only process milestones due TODAY
                        .OrderBy(m => m.DueDate)
                        .ToList();

                    if (!dueMilestones.Any())
                        continue;

                    // Determine brand's location and payment gateway
                    var brand = await userRepo.GetById(campaign.BrandId);
                    var brandLocation = brand?.Location?.ToUpper() ?? "NG";
                    var brandCurrency = brand?.Currency?.ToUpper() ?? "NGN";
                    var isUKBrand = brandLocation == "GB" || brandLocation == "UK";
                    var gateway = isUKBrand ? "stripe" : "paystack";

                    // Get the brand's payment methods
                    var paymentMethods = await paymentMethodRepo.GetByUserIdAsync(campaign.BrandId);

                    // Find appropriate payment method based on location
                    PaymentMethod? defaultPaymentMethod = null;

                    if (isUKBrand)
                    {
                        // For UK brands: Look for Stripe payment method
                        defaultPaymentMethod = paymentMethods
                            .Where(pm => pm.Gateway == "stripe" &&
                                        pm.IsReusable &&
                                        !string.IsNullOrEmpty(pm.StripePaymentMethodId) &&
                                        !string.IsNullOrEmpty(pm.StripeCustomerId))
                            .OrderByDescending(pm => pm.IsDefault)
                            .ThenByDescending(pm => pm.CreatedAt)
                            .FirstOrDefault();
                    }
                    else
                    {
                        // For Nigerian brands: Look for Paystack payment method
                        defaultPaymentMethod = paymentMethods
                            .Where(pm => pm.Gateway == "paystack" &&
                                        pm.IsReusable &&
                                        !string.IsNullOrEmpty(pm.AuthorizationCode))
                            .OrderByDescending(pm => pm.IsDefault)
                            .ThenByDescending(pm => pm.CreatedAt)
                            .FirstOrDefault();
                    }

                    if (defaultPaymentMethod == null)
                    {
                        _logger.LogWarning("No saved {Gateway} payment method for brand {BrandId} - sending reminder for campaign {CampaignId}",
                            gateway, campaign.BrandId, campaign.Id);

                        // Notify brand to add payment method or pay manually
                        foreach (var milestone in dueMilestones)
                        {
                            var formattedAmount = FormatAmount(milestone.AmountInPence, brandCurrency);
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

                    // Process each due milestone with auto-charge (Stripe or Paystack)
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
    public double IntervalHours { get; set; } = 24;

    /// <summary>
    /// Delay between processing each payment to avoid rate limiting (in milliseconds)
    /// </summary>
    public int DelayBetweenPaymentsMs { get; set; } = 2000;
}
