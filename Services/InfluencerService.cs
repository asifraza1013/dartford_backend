using inflan_api.Interfaces;
using inflan_api.Models;

namespace inflan_api.Services
{
    public class InfluencerService : IInfluencerService
    {
        private readonly IInfluencerRepository _influencerRepository;

        public InfluencerService(IInfluencerRepository influencerRepository)
        {
            _influencerRepository = influencerRepository;
        }
        public int ParseFollowers(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;

            string temp = value.ToUpperInvariant().Replace("M", "").Replace("K", "").Trim();
            double num = 0;
            double.TryParse(temp, out num);

            if (value.Contains("M")) return (int)(num * 1_000_000);
            if (value.Contains("K")) return (int)(num * 1_000);
            return (int)num;
        }
        public async Task<IEnumerable<InfluencerUserModel>> GetAllInfluencers()
        {
            return await _influencerRepository.GetAll();
        }

        public async Task<InfluencerUserModel?> GetInfluencerById(int id)
        {
            return await _influencerRepository.GetById(id);
        }

        public async Task<Influencer> CreateInfluencer(Influencer influencer)
        {
            return await _influencerRepository.Create(influencer);
        }

        public async Task<bool> UpdateInfluencer(int userId, Influencer influencer)
        {
            var existingInfluencer = await _influencerRepository.GetByUserId(userId);
            if (existingInfluencer == null) return false;
            
            existingInfluencer.Bio = influencer.Bio ?? existingInfluencer.Bio;

            await _influencerRepository.Update(existingInfluencer);
            return true;
        }

        public async Task<bool> DeleteInfluencer(int id)
        {
            var existing = await _influencerRepository.GetById(id);
            if (existing == null) return false;

            await _influencerRepository.Delete(id);
            return true;
        }
        public async Task<Influencer?> GetInfluencerByUserId(int userId)
        {
            return await _influencerRepository.GetByUserId(userId);
        }
    }
}
