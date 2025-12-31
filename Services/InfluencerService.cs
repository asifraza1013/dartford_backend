using System.Text.Json;
using inflan_api.Interfaces;
using inflan_api.Models;

namespace inflan_api.Services
{
    public class InfluencerService : IInfluencerService
    {
        private readonly IInfluencerRepository _influencerRepository;
        private readonly IPlanRepository _planRepository;

        public InfluencerService(IInfluencerRepository influencerRepository, IPlanRepository planRepository)
        {
            _influencerRepository = influencerRepository;
            _planRepository = planRepository;
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


        public async Task<IEnumerable<InfluencerUserModel>> GetAllInfluencers(string? searchQuery = null, string? followers = null, string? channels = null, string? location = null)
        {
            // If location filter is provided, get influencers from that location directly
            IEnumerable<InfluencerUserModel> allInfluencers;
            if (!string.IsNullOrWhiteSpace(location))
            {
                allInfluencers = await _influencerRepository.GetByLocation(location);
            }
            else
            {
                allInfluencers = await _influencerRepository.GetAll();
            }

            var filteredInfluencers = allInfluencers.AsEnumerable();

            // Parse follower range if provided
            int? minFollowers = null;
            int? maxFollowers = null;
            if (!string.IsNullOrWhiteSpace(followers))
            {
                (minFollowers, maxFollowers) = ParseFollowerRange(followers);
            }

            // Parse channels if provided
            List<string>? channelList = null;
            if (!string.IsNullOrWhiteSpace(channels))
            {
                channelList = channels.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(c => c.Trim().ToLower())
                                    .ToList();
            }

            // Apply filters
            filteredInfluencers = filteredInfluencers.Where(influencer =>
            {
                // Search Query Filter - checks name and social media accounts
                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    var query = searchQuery.ToLower();
                    bool matchesSearch = false;

                    // Check if query matches name
                    if (influencer.User?.Name != null && influencer.User.Name.ToLower().Contains(query))
                    {
                        matchesSearch = true;
                    }

                    // Check if query matches social media platform names
                    if (!matchesSearch)
                    {
                        if ((query.Contains("instagram") && influencer.InstagramFollower > 0) ||
                            (query.Contains("youtube") && influencer.YouTubeFollower > 0) ||
                            (query.Contains("tiktok") && influencer.TikTokFollower > 0) ||
                            (query.Contains("facebook") && influencer.FacebookFollower > 0))
                        {
                            matchesSearch = true;
                        }
                    }

                    // Check if query matches social media usernames
                    if (!matchesSearch)
                    {
                        if ((!string.IsNullOrEmpty(influencer.Instagram) && influencer.Instagram.ToLower().Contains(query)) ||
                            (!string.IsNullOrEmpty(influencer.YouTube) && influencer.YouTube.ToLower().Contains(query)) ||
                            (!string.IsNullOrEmpty(influencer.TikTok) && influencer.TikTok.ToLower().Contains(query)) ||
                            (!string.IsNullOrEmpty(influencer.Facebook) && influencer.Facebook.ToLower().Contains(query)))
                        {
                            matchesSearch = true;
                        }
                    }

                    if (!matchesSearch)
                        return false;
                }

                // Channels Filter - if specified, influencer must have followers in at least one of the specified channels
                if (channelList != null && channelList.Any())
                {
                    bool hasChannelWithFollowers = false;

                    foreach (var channel in channelList)
                    {
                        if (channel == "instagram" && influencer.InstagramFollower > 0)
                        {
                            hasChannelWithFollowers = true;
                            break;
                        }
                        if (channel == "youtube" && influencer.YouTubeFollower > 0)
                        {
                            hasChannelWithFollowers = true;
                            break;
                        }
                        if (channel == "tiktok" && influencer.TikTokFollower > 0)
                        {
                            hasChannelWithFollowers = true;
                            break;
                        }
                        if (channel == "facebook" && influencer.FacebookFollower > 0)
                        {
                            hasChannelWithFollowers = true;
                            break;
                        }
                    }

                    if (!hasChannelWithFollowers)
                        return false;
                }

                // Follower Count Filter
                if (minFollowers.HasValue || maxFollowers.HasValue)
                {
                    bool matchesFollowerRange = false;

                    // If channels are specified, check follower count only for those channels
                    if (channelList != null && channelList.Any())
                    {
                        foreach (var channel in channelList)
                        {
                            int followerCount = 0;

                            if (channel == "instagram")
                                followerCount = influencer.InstagramFollower;
                            else if (channel == "youtube")
                                followerCount = influencer.YouTubeFollower;
                            else if (channel == "tiktok")
                                followerCount = influencer.TikTokFollower;
                            else if (channel == "facebook")
                                followerCount = influencer.FacebookFollower;

                            if (IsInFollowerRange(followerCount, minFollowers, maxFollowers))
                            {
                                matchesFollowerRange = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Check any social media platform
                        matchesFollowerRange = IsInFollowerRange(influencer.InstagramFollower, minFollowers, maxFollowers) ||
                                              IsInFollowerRange(influencer.YouTubeFollower, minFollowers, maxFollowers) ||
                                              IsInFollowerRange(influencer.TikTokFollower, minFollowers, maxFollowers) ||
                                              IsInFollowerRange(influencer.FacebookFollower, minFollowers, maxFollowers);
                    }

                    if (!matchesFollowerRange)
                        return false;
                }

                return true;
            });

            return filteredInfluencers;
        }

        private (int? min, int? max) ParseFollowerRange(string followers)
        {
            followers = followers.Trim().ToLower();

            return followers switch
            {
                "< 50k" or "<50k" or "under 50k" => (null, 50000),
                "50k-100k" or "50-100k" => (50000, 100000),
                "100k-250k" or "100-250k" => (100000, 250000),
                "250k-500k" or "250-500k" => (250000, 500000),
                "> 500k" or ">500k" or "500k+" or "over 500k" => (500000, null),
                _ => (null, null)
            };
        }

        private bool IsInFollowerRange(int followerCount, int? min, int? max)
        {
            if (followerCount <= 0)
                return false;

            if (min.HasValue && followerCount < min.Value)
                return false;

            if (max.HasValue && followerCount > max.Value)
                return false;

            return true;
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

            // Update all fields
            existingInfluencer.Bio = influencer.Bio ?? existingInfluencer.Bio;
            existingInfluencer.Instagram = influencer.Instagram ?? existingInfluencer.Instagram;
            existingInfluencer.YouTube = influencer.YouTube ?? existingInfluencer.YouTube;
            existingInfluencer.TikTok = influencer.TikTok ?? existingInfluencer.TikTok;
            existingInfluencer.Facebook = influencer.Facebook ?? existingInfluencer.Facebook;

            // Update follower counts
            if (influencer.InstagramFollower > 0)
                existingInfluencer.InstagramFollower = influencer.InstagramFollower;
            if (influencer.YouTubeFollower > 0)
                existingInfluencer.YouTubeFollower = influencer.YouTubeFollower;
            if (influencer.TikTokFollower > 0)
                existingInfluencer.TikTokFollower = influencer.TikTokFollower;
            if (influencer.FacebookFollower > 0)
                existingInfluencer.FacebookFollower = influencer.FacebookFollower;

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

        public async Task<Influencer?> FindBySocialAccount(string? instagram, string? youtube, string? tiktok, string? facebook)
        {
            return await _influencerRepository.FindBySocialAccount(instagram, youtube, tiktok, facebook);
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
