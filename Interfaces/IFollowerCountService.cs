using System.Threading.Tasks;
using System.Collections.Generic;

namespace inflan_api.Interfaces
{
    public interface IFollowerCountService
    {
        Task<FollowerCountResult> GetInstagramFollowersAsync(string username);
        Task<FollowerCountResult> GetYouTubeFollowersAsync(string channelId);
        Task<FollowerCountResult> GetTikTokFollowersAsync(string username);
        Task<FollowerCountResult> GetFacebookFollowersAsync(string username);
        Task<Dictionary<string, FollowerCountResult>> GetAllPlatformFollowersAsync(
            string? instagramUsername = null,
            string? youtubeChannelId = null,
            string? tiktokUsername = null,
            string? facebookUsername = null
        );
    }

    public class FollowerCountResult
    {
        public bool Success { get; set; }
        public long Followers { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Platform { get; set; }
        public DateTime? LastUpdated { get; set; }
        public Dictionary<string, object>? AdditionalData { get; set; }
    }
}