using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface ICampaignService
{
    Task<IEnumerable<Campaign>> GetAllCampaigns();
    Task<Campaign?> GetCampaignById(int id);
    Task<Campaign?> CreateCampaign(Campaign campaign);
    Task<bool> UpdateCampaign(int id, Campaign campaign);
    Task<bool> DeleteCampaign(int id);
    Task<IEnumerable<Campaign>> GetCampaignsByInfluencerId(int influencerId);

    Task<IEnumerable<Campaign>> GetCampaignsByInfluencerAndStatus(int influencerId, int campaignStatus);
    Task<List<string>> SaveCampaignDocumentsAsync(List<IFormFile> files);

}