﻿using inflan_api.Models;

namespace inflan_api.Interfaces
{
    public interface IInfluencerService
    {
        Task<IEnumerable<Influencer>> GetAllInfluencers();
        Task<Influencer?> GetInfluencerById(int id);
        Task<Influencer> CreateInfluencer(Influencer influencer);
        Task<bool> UpdateInfluencer(int userId, Influencer influencer);
        Task<bool> DeleteInfluencer(int id);
        Task<Influencer?> GetInfluencerByUserId(int userId);

    }
}
