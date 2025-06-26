using dartford_api.Models;

namespace dartford_api.Interfaces;

public interface ICampaignRepository
{
    Task<IEnumerable<Campaign>> GetAll();
    Task<Campaign?> GetById(int id);
    Task<Campaign> Create(Campaign campaign);
    Task Update(Campaign campaign);
    Task Delete(int id);
    Task<IEnumerable<Campaign>> GetCampaignsByInfluencerId(int influencerId);

}