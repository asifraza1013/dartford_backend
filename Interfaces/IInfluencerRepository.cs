using inflan_api.Models;

namespace inflan_api.Interfaces
{
    public interface IInfluencerRepository
    {
        Task<IEnumerable<InfluencerUserModel>> GetAll();
        Task<InfluencerUserModel?> GetById(int id);
        Task<Influencer> Create(Influencer influencer);
        Task Update(Influencer influencer);
        Task Delete(int id);
        Task<Influencer?> GetByUserId(int userId);

    }
}
