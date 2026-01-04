using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.Utils;
using Microsoft.Extensions.Logging;

namespace inflan_api.Services.Payment;

public class MilestoneService : IMilestoneService
{
    private readonly IPaymentMilestoneRepository _milestoneRepo;
    private readonly ICampaignRepository _campaignRepo;
    private readonly IPlanRepository _planRepo;
    private readonly IPlatformSettingsService _settingsService;
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly ILogger<MilestoneService> _logger;

    public MilestoneService(
        IPaymentMilestoneRepository milestoneRepo,
        ICampaignRepository campaignRepo,
        IPlanRepository planRepo,
        IPlatformSettingsService settingsService,
        IPaymentMethodRepository paymentMethodRepo,
        ILogger<MilestoneService> logger)
    {
        _milestoneRepo = milestoneRepo;
        _campaignRepo = campaignRepo;
        _planRepo = planRepo;
        _settingsService = settingsService;
        _paymentMethodRepo = paymentMethodRepo;
        _logger = logger;
    }

    public async Task<List<PaymentMilestone>> CreateMilestonesForCampaignAsync(int campaignId)
    {
        var campaign = await _campaignRepo.GetById(campaignId);
        if (campaign == null)
            throw new ArgumentException($"Campaign {campaignId} not found");

        var plan = await _planRepo.GetById(campaign.PlanId);
        if (plan == null)
            throw new ArgumentException($"Plan {campaign.PlanId} not found");

        // Get platform fee percentage
        var brandFeePercent = await _settingsService.GetBrandPlatformFeePercentAsync();

        // Calculate total amount in pence
        var totalAmountInPence = (long)(plan.Price * 100); // Convert to pence
        var numberOfMonths = plan.NumberOfMonths > 0 ? plan.NumberOfMonths : 1;

        // Calculate milestone amount
        var milestoneAmount = totalAmountInPence / numberOfMonths;
        var remainder = totalAmountInPence % numberOfMonths;

        // Calculate platform fee per milestone
        var platformFeePerMilestone = (long)(milestoneAmount * brandFeePercent / 100);

        var milestones = new List<PaymentMilestone>();
        var startDate = DateTime.UtcNow;

        for (int i = 1; i <= numberOfMonths; i++)
        {
            var amount = milestoneAmount;
            // Add any remainder to the last milestone
            if (i == numberOfMonths)
                amount += remainder;

            var milestone = new PaymentMilestone
            {
                CampaignId = campaignId,
                MilestoneNumber = i,
                AmountInPence = amount,
                PlatformFeeInPence = platformFeePerMilestone,
                DueDate = i == 1 ? startDate : startDate.AddMonths(i - 1),
                Status = (int)MilestoneStatus.PENDING
            };

            milestones.Add(milestone);
        }

        var createdMilestones = await _milestoneRepo.CreateBulkAsync(milestones);

        // Update campaign total amount
        campaign.TotalAmountInPence = totalAmountInPence;
        await _campaignRepo.Update(campaign);

        _logger.LogInformation("Created {Count} milestones for campaign {CampaignId}", milestones.Count, campaignId);

        return createdMilestones;
    }

    public async Task<List<PaymentMilestone>> GetCampaignMilestonesAsync(int campaignId)
    {
        return await _milestoneRepo.GetByCampaignIdAsync(campaignId);
    }

    public async Task<PaymentMilestone?> GetMilestoneAsync(int milestoneId)
    {
        return await _milestoneRepo.GetByIdAsync(milestoneId);
    }

    public async Task<PaymentMilestone> MarkMilestoneAsPaidAsync(int milestoneId, int transactionId)
    {
        var milestone = await _milestoneRepo.GetByIdAsync(milestoneId);
        if (milestone == null)
            throw new ArgumentException($"Milestone {milestoneId} not found");

        milestone.Status = (int)MilestoneStatus.PAID;
        milestone.PaidAt = DateTime.UtcNow;
        milestone.TransactionId = transactionId;

        var updated = await _milestoneRepo.UpdateAsync(milestone);

        // Update campaign paid amount
        var campaign = await _campaignRepo.GetById(milestone.CampaignId);
        if (campaign != null)
        {
            campaign.PaidAmountInPence += milestone.AmountInPence;
            await _campaignRepo.Update(campaign);
        }

        _logger.LogInformation("Marked milestone {MilestoneId} as paid with transaction {TransactionId}",
            milestoneId, transactionId);

        return updated;
    }

    public async Task<List<PaymentMilestone>> GetDueMilestonesAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _milestoneRepo.GetPendingMilestonesAsync(today);
    }

    public async Task MarkOverdueMilestonesAsync()
    {
        var overdueMilestones = await _milestoneRepo.GetOverdueMilestonesAsync();

        foreach (var milestone in overdueMilestones)
        {
            milestone.Status = (int)MilestoneStatus.OVERDUE;
            await _milestoneRepo.UpdateAsync(milestone);
        }

        if (overdueMilestones.Any())
        {
            _logger.LogInformation("Marked {Count} milestones as overdue", overdueMilestones.Count);
        }
    }

    public async Task CancelCampaignMilestonesAsync(int campaignId)
    {
        var milestones = await _milestoneRepo.GetByCampaignIdAsync(campaignId);

        foreach (var milestone in milestones)
        {
            if (milestone.Status == (int)MilestoneStatus.PENDING ||
                milestone.Status == (int)MilestoneStatus.OVERDUE)
            {
                milestone.Status = (int)MilestoneStatus.CANCELLED;
                await _milestoneRepo.UpdateAsync(milestone);
            }
        }

        _logger.LogInformation("Cancelled pending milestones for campaign {CampaignId}", campaignId);
    }

    public async Task<List<CampaignWithMilestonesDto>> GetBrandCampaignsWithMilestonesAsync(int brandId)
    {
        var campaigns = await _campaignRepo.GetCampaignsByBrandId(brandId);
        var result = new List<CampaignWithMilestonesDto>();

        foreach (var campaign in campaigns)
        {
            var milestones = await _milestoneRepo.GetByCampaignIdAsync(campaign.Id);

            result.Add(new CampaignWithMilestonesDto
            {
                CampaignId = campaign.Id,
                ProjectName = campaign.ProjectName,
                InfluencerName = campaign.Influencer?.Name,
                Milestones = milestones.Select(m => new MilestoneDto
                {
                    Id = m.Id,
                    CampaignId = m.CampaignId,
                    MilestoneNumber = m.MilestoneNumber,
                    AmountInPence = m.AmountInPence,
                    PlatformFeeInPence = m.PlatformFeeInPence,
                    DueDate = m.DueDate,
                    Status = m.Status,
                    PaidAt = m.PaidAt
                }).ToList()
            });
        }

        return result;
    }

    public async Task<PaymentMilestone> CreateMilestoneAsync(CreateMilestoneDto request)
    {
        var campaign = await _campaignRepo.GetById(request.CampaignId);
        if (campaign == null)
            throw new ArgumentException($"Campaign {request.CampaignId} not found");

        // Get platform fee percentage
        var brandFeePercent = await _settingsService.GetBrandPlatformFeePercentAsync();
        var platformFee = (long)(request.AmountInPence * brandFeePercent / 100);

        // Ensure DueDate is UTC for PostgreSQL
        var dueDate = request.DueDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.DueDate, DateTimeKind.Utc)
            : request.DueDate.ToUniversalTime();

        var milestone = new PaymentMilestone
        {
            CampaignId = request.CampaignId,
            MilestoneNumber = request.MilestoneNumber,
            Title = request.Title,
            Description = request.Description,
            AmountInPence = request.AmountInPence,
            PlatformFeeInPence = platformFee,
            DueDate = dueDate,
            Status = (int)MilestoneStatus.PENDING,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _milestoneRepo.CreateAsync(milestone);

        _logger.LogInformation("Created milestone {MilestoneNumber} for campaign {CampaignId}",
            request.MilestoneNumber, request.CampaignId);

        return created;
    }

    public async Task<PaymentMilestone> UpdateMilestoneAsync(int milestoneId, UpdateMilestoneDto request)
    {
        var milestone = await _milestoneRepo.GetByIdAsync(milestoneId);
        if (milestone == null)
            throw new ArgumentException($"Milestone {milestoneId} not found");

        if (milestone.Status == (int)MilestoneStatus.PAID)
            throw new InvalidOperationException("Cannot update a paid milestone");

        if (request.Title != null)
            milestone.Title = request.Title;

        if (request.Description != null)
            milestone.Description = request.Description;

        if (request.AmountInPence.HasValue)
        {
            milestone.AmountInPence = request.AmountInPence.Value;
            // Recalculate platform fee
            var brandFeePercent = await _settingsService.GetBrandPlatformFeePercentAsync();
            milestone.PlatformFeeInPence = (long)(request.AmountInPence.Value * brandFeePercent / 100);
        }

        if (request.DueDate.HasValue)
        {
            // Ensure DueDate is UTC for PostgreSQL
            var dueDate = request.DueDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc)
                : request.DueDate.Value.ToUniversalTime();
            milestone.DueDate = dueDate;
        }

        milestone.UpdatedAt = DateTime.UtcNow;

        var updated = await _milestoneRepo.UpdateAsync(milestone);

        _logger.LogInformation("Updated milestone {MilestoneId}", milestoneId);

        return updated;
    }

    public async Task DeleteMilestoneAsync(int milestoneId)
    {
        var milestone = await _milestoneRepo.GetByIdAsync(milestoneId);
        if (milestone == null)
            throw new ArgumentException($"Milestone {milestoneId} not found");

        if (milestone.Status == (int)MilestoneStatus.PAID)
            throw new InvalidOperationException("Cannot delete a paid milestone");

        await _milestoneRepo.DeleteAsync(milestoneId);

        _logger.LogInformation("Deleted milestone {MilestoneId}", milestoneId);
    }

    public async Task UpdateCampaignPaymentConfigAsync(int campaignId, int paymentType, bool isAutoPayEnabled, int? brandUserId = null)
    {
        var campaign = await _campaignRepo.GetById(campaignId);
        if (campaign == null)
            throw new ArgumentException($"Campaign {campaignId} not found");

        // If enabling auto-pay, verify the brand has a saved payment method
        if (isAutoPayEnabled)
        {
            // Get the brand user ID from the campaign if not provided
            var userId = brandUserId ?? campaign.BrandId;

            var paymentMethods = await _paymentMethodRepo.GetByUserIdAsync(userId);
            if (paymentMethods == null || !paymentMethods.Any())
            {
                throw new InvalidOperationException("Cannot enable auto-pay without a saved payment method. Please save a card first by paying a milestone.");
            }

            _logger.LogInformation("Brand {UserId} has {Count} saved payment methods, allowing auto-pay",
                userId, paymentMethods.Count);
        }

        campaign.PaymentType = paymentType;
        campaign.IsRecurringEnabled = isAutoPayEnabled;

        await _campaignRepo.Update(campaign);

        _logger.LogInformation("Updated payment config for campaign {CampaignId}: PaymentType={PaymentType}, AutoPay={AutoPay}",
            campaignId, paymentType, isAutoPayEnabled);
    }
}
