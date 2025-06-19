using dartford_api.Models;
namespace dartford_api.Interfaces
{
    public interface IInfluencerRepository
    {
        Task<IEnumerable<Influencer>> GetAll();
        Task<Influencer?> GetById(int id);
        Task<Influencer> Create(Influencer influencer);
        Task Update(Influencer influencer);
        Task Delete(int id);
        Task<Influencer?> GetByUserId(int userId);

    }
}
