using System.Text.Json;
using inflan_api.Models;

namespace inflan_api.Interfaces
{
    public interface IInfluencerService
    {
        Task<IEnumerable<InfluencerUserModel>> GetAllInfluencers(string? searchQuery = null, string? followers = null, string? channels = null, string? location = null);
        Task<InfluencerUserModel?> GetInfluencerById(int id);
        Task<InfluencerUserModel?> GetInfluencerByUserId(int userId);
        Task<Influencer?> GetInfluencerBasicByUserId(int userId);
        Task<Influencer> CreateInfluencer(Influencer influencer);
        Task<bool> UpdateInfluencer(int userId, Influencer influencer);
        Task<bool> DeleteInfluencer(int id);
        Task<Influencer?> FindBySocialAccount(string? instagram, string? youtube, string? tiktok, string? facebook);
        int ParseFollowersFromYouTube(JsonElement json);
        int ParseFollowersFromTikTok(JsonElement json);
        int ParseFollowersFromInstagram(JsonElement json);
        int ParseFollowerString(string? value);
        Task<(JsonElement data, string? error)> SafeParseJsonAsync(HttpResponseMessage response, string platform);
    }
}
