using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using inflan_api.Interfaces;
using inflan_api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace inflan_api.Services
{
    public class SocialBladeFollowerService : IFollowerCountService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SocialBladeFollowerService> _logger;
        private readonly SocialBladeConfig _config;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public SocialBladeFollowerService(
            HttpClient httpClient,
            ILogger<SocialBladeFollowerService> logger,
            IOptions<SocialBladeConfig> config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = config.Value;

            // Configure HTTP client
            _httpClient.BaseAddress = new Uri(_config.BaseUrl + "/");
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
            
            // Set up retry policy with exponential backoff
            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    _config.RetryCount,
                    retryAttempt => TimeSpan.FromMilliseconds(_config.RetryDelayMs * Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Retry {retryCount} after {timespan} seconds");
                    });
        }

        public async Task<FollowerCountResult> GetInstagramFollowersAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new FollowerCountResult
                {
                    Success = false,
                    Platform = "Instagram",
                    ErrorMessage = "Username cannot be empty"
                };
            }

            try
            {
                var response = await MakeSocialBladeRequestAsync("instagram", username);
                return ParseSocialBladeResponse(response, "Instagram");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching Instagram followers for {username}");
                return new FollowerCountResult
                {
                    Success = false,
                    Platform = "Instagram",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<FollowerCountResult> GetTwitterFollowersAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new FollowerCountResult
                {
                    Success = false,
                    Platform = "Twitter",
                    ErrorMessage = "Username cannot be empty"
                };
            }

            try
            {
                var response = await MakeSocialBladeRequestAsync("twitter", username);
                return ParseSocialBladeResponse(response, "Twitter");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching Twitter followers for {username}");
                return new FollowerCountResult
                {
                    Success = false,
                    Platform = "Twitter",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<FollowerCountResult> GetTikTokFollowersAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new FollowerCountResult
                {
                    Success = false,
                    Platform = "TikTok",
                    ErrorMessage = "Username cannot be empty"
                };
            }

            try
            {
                var response = await MakeSocialBladeRequestAsync("tiktok", username);
                return ParseSocialBladeResponse(response, "TikTok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching TikTok followers for {username}");
                return new FollowerCountResult
                {
                    Success = false,
                    Platform = "TikTok",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<FollowerCountResult> GetFacebookFollowersAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new FollowerCountResult
                {
                    Success = false,
                    Platform = "Facebook",
                    ErrorMessage = "Username cannot be empty"
                };
            }

            try
            {
                var response = await MakeSocialBladeRequestAsync("facebook", username);
                return ParseSocialBladeResponse(response, "Facebook");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching Facebook followers for {username}");
                return new FollowerCountResult
                {
                    Success = false,
                    Platform = "Facebook",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<FollowerCountResult> GetYouTubeFollowersAsync(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                return new FollowerCountResult
                {
                    Success = false,
                    Platform = "YouTube",
                    ErrorMessage = "Channel ID cannot be empty"
                };
            }

            try
            {
                var response = await MakeSocialBladeRequestAsync("youtube", channelId);
                return ParseSocialBladeResponse(response, "YouTube");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching YouTube subscribers for {channelId}");
                return new FollowerCountResult
                {
                    Success = false,
                    Platform = "YouTube",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<Dictionary<string, FollowerCountResult>> GetAllPlatformFollowersAsync(
            string? instagramUsername = null,
            string? twitterUsername = null,
            string? tiktokUsername = null,
            string? facebookUsername = null,
            string? youtubeChannelId = null)
        {
            var results = new Dictionary<string, FollowerCountResult>();
            var tasks = new List<Task>();

            // Create tasks for each platform with username provided
            if (!string.IsNullOrWhiteSpace(instagramUsername))
            {
                tasks.Add(Task.Run(async () =>
                {
                    results["Instagram"] = await GetInstagramFollowersAsync(instagramUsername);
                }));
            }

            if (!string.IsNullOrWhiteSpace(twitterUsername))
            {
                tasks.Add(Task.Run(async () =>
                {
                    results["Twitter"] = await GetTwitterFollowersAsync(twitterUsername);
                }));
            }

            if (!string.IsNullOrWhiteSpace(tiktokUsername))
            {
                tasks.Add(Task.Run(async () =>
                {
                    results["TikTok"] = await GetTikTokFollowersAsync(tiktokUsername);
                }));
            }

            if (!string.IsNullOrWhiteSpace(facebookUsername))
            {
                tasks.Add(Task.Run(async () =>
                {
                    results["Facebook"] = await GetFacebookFollowersAsync(facebookUsername);
                }));
            }

            if (!string.IsNullOrWhiteSpace(youtubeChannelId))
            {
                tasks.Add(Task.Run(async () =>
                {
                    results["YouTube"] = await GetYouTubeFollowersAsync(youtubeChannelId);
                }));
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            return results;
        }

        private async Task<JsonDocument> MakeSocialBladeRequestAsync(string platform, string identifier)
        {
            // Use the actual user-provided identifier (username)
            var queryIdentifier = identifier;
            
            // Build request URL based on Social Blade Matrix API format
            var requestUrl = $"{platform}/statistics?query={queryIdentifier}&history=default&allow-stale=false";
            
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            
            // Add authentication headers (not query parameters)
            request.Headers.Add("clientid", _config.ApiKey);
            request.Headers.Add("token", _config.ApiSecret);
            
            // Add User-Agent to avoid bot detection
            request.Headers.Add("User-Agent", "Inflan-API/1.0");

            _logger.LogInformation($"Making Social Blade API request: {platform}/statistics?query={queryIdentifier}");

            // Execute request with retry policy
            var response = await _retryPolicy.ExecuteAsync(async () => 
                await _httpClient.SendAsync(request));

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Social Blade API error: {response.StatusCode} - {errorContent}");
                throw new HttpRequestException($"Social Blade API returned {response.StatusCode}: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(content);
        }

        private FollowerCountResult ParseSocialBladeResponse(JsonDocument response, string platform)
        {
            try
            {
                var root = response.RootElement;
                
                // Check status first
                if (root.TryGetProperty("status", out var statusObj))
                {
                    if (statusObj.TryGetProperty("success", out var success) && !success.GetBoolean())
                    {
                        var errorMessage = statusObj.TryGetProperty("error", out var error) 
                            ? error.GetString() 
                            : "API request failed";
                        
                        return new FollowerCountResult
                        {
                            Success = false,
                            Platform = platform,
                            ErrorMessage = errorMessage
                        };
                    }
                }

                // Extract follower count based on platform
                long followers = 0;
                DateTime? lastUpdated = null;
                var additionalData = new Dictionary<string, object>();

                if (root.TryGetProperty("data", out var data))
                {
                    if (data.TryGetProperty("statistics", out var stats))
                    {
                        if (stats.TryGetProperty("total", out var total))
                        {
                            // Different platforms have different follower count fields
                            switch (platform.ToLower())
                            {
                                case "youtube":
                                    if (total.TryGetProperty("subscribers", out var subs))
                                        followers = subs.GetInt64();
                                    if (total.TryGetProperty("views", out var views))
                                        additionalData["totalViews"] = views.GetInt64();
                                    break;
                                case "instagram":
                                case "tiktok":
                                case "twitter":
                                case "facebook":
                                    // These platforms typically use followers field
                                    if (total.TryGetProperty("followers", out var followersProp))
                                        followers = followersProp.GetInt64();
                                    else if (total.TryGetProperty("following", out var following))
                                        followers = following.GetInt64();
                                    break;
                            }
                        }
                    }

                    // Extract general info
                    if (data.TryGetProperty("general", out var general))
                    {
                        if (general.TryGetProperty("created_at", out var createdAt))
                        {
                            var createdAtString = createdAt.GetString();
                            if (!string.IsNullOrEmpty(createdAtString))
                            {
                                if (DateTime.TryParse(createdAtString, out var created))
                                    additionalData["createdAt"] = created;
                            }
                        }
                    }

                    // Extract ranks if available
                    if (data.TryGetProperty("ranks", out var ranks))
                    {
                        if (ranks.TryGetProperty("sbrank", out var sbrank))
                            additionalData["socialBladeRank"] = sbrank.GetInt32();
                    }
                }

                return new FollowerCountResult
                {
                    Success = true,
                    Platform = platform,
                    Followers = followers,
                    LastUpdated = DateTime.UtcNow, // API doesn't provide last updated, use current time
                    AdditionalData = additionalData
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing Social Blade response for {platform}");
                return new FollowerCountResult
                {
                    Success = false,
                    Platform = platform,
                    ErrorMessage = $"Failed to parse response: {ex.Message}"
                };
            }
        }
    }
}