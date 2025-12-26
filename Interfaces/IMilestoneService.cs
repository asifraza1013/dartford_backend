using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface IMilestoneService
{
    /// <summary>
    /// Create milestones for a campaign based on the plan's number of months
    /// </summary>
    Task<List<PaymentMilestone>> CreateMilestonesForCampaignAsync(int campaignId);

    /// <summary>
    /// Get all milestones for a campaign
    /// </summary>
    Task<List<PaymentMilestone>> GetCampaignMilestonesAsync(int campaignId);

    /// <summary>
    /// Get a specific milestone
    /// </summary>
    Task<PaymentMilestone?> GetMilestoneAsync(int milestoneId);

    /// <summary>
    /// Mark a milestone as paid
    /// </summary>
    Task<PaymentMilestone> MarkMilestoneAsPaidAsync(int milestoneId, int transactionId);

    /// <summary>
    /// Get milestones due for payment (for recurring payment processing)
    /// </summary>
    Task<List<PaymentMilestone>> GetDueMilestonesAsync();

    /// <summary>
    /// Mark overdue milestones
    /// </summary>
    Task MarkOverdueMilestonesAsync();

    /// <summary>
    /// Cancel all pending milestones for a campaign
    /// </summary>
    Task CancelCampaignMilestonesAsync(int campaignId);

    /// <summary>
    /// Get all campaigns with milestones for a brand
    /// </summary>
    Task<List<CampaignWithMilestonesDto>> GetBrandCampaignsWithMilestonesAsync(int brandId);
}

public class CampaignWithMilestonesDto
{
    public int CampaignId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? InfluencerName { get; set; }
    public List<MilestoneDto> Milestones { get; set; } = new();
}

public class MilestoneDto
{
    public int Id { get; set; }
    public int CampaignId { get; set; }
    public int MilestoneNumber { get; set; }
    public long AmountInPence { get; set; }
    public long PlatformFeeInPence { get; set; }
    public DateTime DueDate { get; set; }
    public int Status { get; set; }
    public DateTime? PaidAt { get; set; }
}
