using dartford_api.Interfaces;
using dartford_api.Models;

namespace dartford_api.Services
{
    public class InfluencerService : IInfluencerService
    {
        private readonly IInfluencerRepository _influencerRepository;

        public InfluencerService(IInfluencerRepository influencerRepository)
        {
            _influencerRepository = influencerRepository;
        }

        public async Task<IEnumerable<Influencer>> GetAllInfluencers()
        {
            return await _influencerRepository.GetAll();
        }

        public async Task<Influencer?> GetInfluencerById(int id)
        {
            return await _influencerRepository.GetById(id);
        }

        public async Task<Influencer> CreateInfluencer(Influencer influencer)
        {
            return await _influencerRepository.Create(influencer);
        }

        public async Task<bool> UpdateInfluencer(int id, Influencer influencer)
        {
            var existingInfluencer = await _influencerRepository.GetById(id);
            if (existingInfluencer == null) return false;

            existingInfluencer.Twitter = influencer.Twitter ?? existingInfluencer.Twitter;
            existingInfluencer.Instagram = influencer.Instagram ?? existingInfluencer.Instagram;
            existingInfluencer.Facebook = influencer.Facebook ?? existingInfluencer.Facebook;
            existingInfluencer.TikTok = influencer.TikTok ?? existingInfluencer.TikTok;
            existingInfluencer.TwitterFollower = influencer.TwitterFollower ?? existingInfluencer.TwitterFollower;
            existingInfluencer.InstagramFollower = influencer.InstagramFollower ?? existingInfluencer.InstagramFollower;
            existingInfluencer.FacebookFollower = influencer.FacebookFollower ?? existingInfluencer.FacebookFollower;
            existingInfluencer.TikTokFollower = influencer.TikTokFollower ?? existingInfluencer.TikTokFollower;
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
