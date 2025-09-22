using System.Text.Json;
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
        
        public int ParseFollowerString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            value = value.ToLower().Replace(",", "").Trim();

            double number = 0;

            if (value.EndsWith("m") && double.TryParse(value[..^1], out number))
                return (int)(number * 1_000_000);
            if (value.EndsWith("k") && double.TryParse(value[..^1], out number))
                return (int)(number * 1_000);
            if (double.TryParse(value, out number))
                return (int)number;

            return 0;
        }
        public int ParseFollowersFromYouTube(JsonElement json)
        {
            var count = json.GetProperty("follower_count").GetDouble();
            var unit = json.GetProperty("unit").GetString()?.ToLower();

            int multiplier = unit switch
            {
                "millions" => 1_000_000,
                "thousands" => 1_000,
                _ => 1
            };

            return (int)(count * multiplier);
        }
        public int ParseFollowersFromTikTok(JsonElement tiktokJson)
        {
            if (tiktokJson.TryGetProperty("followers_count", out var followersProp))
            {
                string? followersStr = followersProp.GetString();
                return ParseFollowerString(followersStr);
            }
            return 0;
        }
        
        public int ParseFollowersFromInstagram(JsonElement instaJson)
        {
            if (instaJson.TryGetProperty("influencer", out var influencerObj) &&
                influencerObj.TryGetProperty("followers", out var followersProp))
            {
                string? followersStr = followersProp.GetString();
                return ParseFollowerString(followersStr);
            }
            return 0;
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
        public async Task<(JsonElement data, string? error)> SafeParseJsonAsync(HttpResponseMessage response, string platform)
        {
            try
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                
                if (json.ValueKind == JsonValueKind.Array)
                {
                    if (json.GetArrayLength() == 0)
                        return (default, $"{platform}:Invalid user name. Empty array received..");

                    var firstItem = json[0];
                    if (!firstItem.EnumerateObject().Any())
                        return (default, $"{platform}: Invalid user name. Array contains empty object..");

                    return (firstItem, null);
                }

                if (json.TryGetProperty("items_found", out var itemsFoundProp) && itemsFoundProp.GetInt32() == 0)
                    return (default, $"{platform}:Invalid user name. No user found..");

                if (platform == "YouTube" &&
                    (!json.TryGetProperty("follower_count", out var countProp) || countProp.ValueKind == JsonValueKind.Null))
                    return (default, $"{platform}:Invalid user name. Follower count is missing or nullll.");

                return (json, null);
            }
            catch (Exception ex)
            {
                return (default, $"{platform}: Failed to parse response: {ex.Message}");
            }
        }
        
        
    }
}
