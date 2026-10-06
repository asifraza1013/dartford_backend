using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface ICampaignRepository
{
    Task<IEnumerable<Campaign>> GetAll();
    Task<Campaign?> GetById(int id);
    Task<Campaign> Create(Campaign campaign);
    Task Update(Campaign campaign);
    Task Delete(int id);
    Task<IEnumerable<Campaign>> GetCampaignsByInfluencerId(int influencerId);
    Task<IEnumerable<Campaign>> GetCampaignsByBrandId(int brandId);
    Task<IEnumerable<Campaign>> GetAllWithAutoPay();

    /// <summary>
    /// Campaigns that are payable (ACTIVE or COMPLETED, not CANCELLED) with
    /// PaidAmountInPence &lt; TotalAmountInPence — candidates for the campaign-level
    /// payment-deadline sweep. Includes both ONE_TIME campaigns (never have milestones)
    /// and MILESTONE campaigns, since the sweep itself filters down to only the
    /// portion of the balance not already covered by a tracked milestone.
    /// </summary>
    Task<IEnumerable<Campaign>> GetWithOutstandingBalanceAsync();
}