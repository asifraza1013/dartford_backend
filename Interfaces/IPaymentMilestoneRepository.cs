using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface IPaymentMilestoneRepository
{
    Task<PaymentMilestone?> GetByIdAsync(int id);
    Task<List<PaymentMilestone>> GetByCampaignIdAsync(int campaignId);
    Task<List<PaymentMilestone>> GetPendingMilestonesAsync(DateTime dueDate);
    Task<List<PaymentMilestone>> GetOverdueMilestonesAsync();
    Task<List<PaymentMilestone>> GetUpcomingByInfluencerIdAsync(int influencerId);
    Task<PaymentMilestone> CreateAsync(PaymentMilestone milestone);
    Task<List<PaymentMilestone>> CreateBulkAsync(List<PaymentMilestone> milestones);
    Task<PaymentMilestone> UpdateAsync(PaymentMilestone milestone);
    Task DeleteAsync(int id);
    Task DeleteByCampaignIdAsync(int campaignId);
}
