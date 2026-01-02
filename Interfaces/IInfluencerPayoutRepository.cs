using inflan_api.DTOs;
using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface IInfluencerPayoutRepository
{
    Task<InfluencerPayout?> GetByIdAsync(int id);
    Task<List<InfluencerPayout>> GetByCampaignIdAsync(int campaignId);
    Task<List<InfluencerPayout>> GetByInfluencerIdAsync(int influencerId, int page = 1, int pageSize = 20);
    Task<List<InfluencerPayout>> GetPendingByInfluencerIdAsync(int influencerId);
    Task<(List<InfluencerPayout> Items, int TotalCount)> GetByInfluencerIdFilteredAsync(int influencerId, PaymentFilterDto filter);
    Task<InfluencerPayout> CreateAsync(InfluencerPayout payout);
    Task<InfluencerPayout> UpdateAsync(InfluencerPayout payout);
    Task<long> GetTotalPendingByInfluencerIdAsync(int influencerId);
    Task<long> GetTotalReleasedByInfluencerIdAsync(int influencerId);
    Task<long> GetTotalReleasedByInfluencerIdAsync(int influencerId, string currency);
}
