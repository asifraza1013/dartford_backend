using System.Text.Json;
using inflan_api.Models;

namespace inflan_api.Interfaces
{
    public interface IInfluencerService
    {
        Task<IEnumerable<InfluencerUserModel>> GetAllInfluencers();
        Task<InfluencerUserModel?> GetInfluencerById(int id);
        Task<Influencer> CreateInfluencer(Influencer influencer);
        Task<bool> UpdateInfluencer(int userId, Influencer influencer);
        Task<bool> DeleteInfluencer(int id);
        Task<Influencer?> GetInfluencerByUserId(int userId);
        Task<Influencer?> FindBySocialAccount(string? instagram, string? youtube, string? tiktok, string? facebook);
        int ParseFollowersFromYouTube(JsonElement json);
        int ParseFollowersFromTikTok(JsonElement json);
        int ParseFollowersFromInstagram(JsonElement json);
        int ParseFollowerString(string? value);
        Task<(JsonElement data, string? error)> SafeParseJsonAsync(HttpResponseMessage response, string platform);
    }
}
