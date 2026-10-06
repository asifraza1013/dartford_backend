using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.DTOs;
using inflan_api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace inflan_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InfluencerController : ControllerBase
    {
        private readonly IInfluencerService _influencerService;
        private readonly IUserService _userService;
        private readonly IFollowerCountService _followerCountService;

        public InfluencerController(
            IInfluencerService influencerService,  
            IUserService userService, 
            IFollowerCountService followerCountService)
        {
            _influencerService = influencerService;
            _userService = userService;
            _followerCountService = followerCountService;
        }

        [HttpGet("getAllInfluencers")]
        [Authorize]
        public async Task<IActionResult> GetAllInfluencers(
            [FromQuery] string? searchQuery = null,
            [FromQuery] string? followers = null,
            [FromQuery] string? channels = null)
        {
            // Get current user's location for filtering
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            string? location = null;
            string? currency = null;

            if (userIdClaim != null)
            {
                int userId = int.Parse(userIdClaim.Value);
                var user = await _userService.GetUserById(userId);

                // Only filter by location if user is a brand (UserType == 2)
                if (user != null && user.UserType == 2)
                {
                    location = user.Location ?? "NG"; // Default to NG if not set
                    currency = user.Currency ?? "NGN"; // Also track currency for reference
                }
            }

            var influencers = await _influencerService.GetAllInfluencers(searchQuery, followers, channels, location);
            return Ok(new
            {
                count = influencers.Count(),
                filters = new
                {
                    searchQuery,
                    followers,
                    channels,
                    location,
                    currency
                },
                data = influencers
            });
        }

        [HttpGet("getInfluencerById/{influencerId}")]
        public async Task<IActionResult> GetInfluencerById(int influencerId)
        {
            var influencer = await _influencerService.GetInfluencerById(influencerId);
            return influencer != null ? Ok(influencer) : StatusCode(404, new {
                message = "Influencer not found",
                code = Message.INFLUENCER_NOT_FOUND
            });
        }

        [HttpGet("getInfluencerByUserId/{userId}")]
        public async Task<IActionResult> GetInfluencerByUserId(int userId)
        {
            var influencer = await _influencerService.GetInfluencerByUserId(userId);
            return influencer != null ? Ok(influencer) : StatusCode(404, new {
                message = "Influencer not found",
                code = Message.INFLUENCER_NOT_FOUND
            });
        }

        [HttpGet("testFollowerService")]
        public async Task<IActionResult> TestFollowerService()
        {
            var followerResults = await _followerCountService.GetAllPlatformFollowersAsync(
                instagramUsername: "ch.zulqarnain25",
                youtubeChannelId: "ZulqarnainSikandar25", 
                tiktokUsername: "ch.zulqarnain25",
                facebookUsername: "zulqarnainsikandar09"
            );
            
            return Ok(new {
                message = "Follower service test",
                results = followerResults
            });
        }

        [HttpPost("createNewInfluencer")]
        [Authorize]
        public async Task<IActionResult> CreateInfluencer([FromBody] Influencer influencer)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new {
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN"
                });

            int userId = int.Parse(userIdClaim.Value);
            influencer.UserId = userId;

            // Normalize handles up front so a whitespace-only value (e.g. a stray space
            // in an optional field) is never treated as "provided" by the IsNullOrEmpty
            // checks below — it would otherwise count as a connected platform.
            influencer.Instagram = influencer.Instagram?.Trim();
            influencer.YouTube = influencer.YouTube?.Trim();
            influencer.TikTok = influencer.TikTok?.Trim();
            influencer.Facebook = influencer.Facebook?.Trim();

            // Check if social account already exists for another user
            var existingInfluencer = await _influencerService.FindBySocialAccount(
                influencer.Instagram,
                influencer.YouTube,
                influencer.TikTok,
                influencer.Facebook
            );

            if (existingInfluencer != null)
            {
                // Check if it's the same user (by email)
                var existingUser = await _userService.GetUserById(existingInfluencer.UserId);
                var currentUser = await _userService.GetUserById(userId);

                if (existingUser != null && currentUser != null && existingUser.Email == currentUser.Email)
                {
                    // Same user, just update instead of creating
                    Console.WriteLine($"Social accounts already exist for this user (email: {currentUser.Email}). Updating existing record.");

                    // Update the existing influencer with new data
                    existingInfluencer.Instagram = influencer.Instagram ?? existingInfluencer.Instagram;
                    existingInfluencer.YouTube = influencer.YouTube ?? existingInfluencer.YouTube;
                    existingInfluencer.TikTok = influencer.TikTok ?? existingInfluencer.TikTok;
                    existingInfluencer.Facebook = influencer.Facebook ?? existingInfluencer.Facebook;
                    existingInfluencer.Bio = influencer.Bio ?? existingInfluencer.Bio;

                    // Continue to fetch follower counts for the updated accounts below
                    influencer = existingInfluencer;
                }
                else
                {
                    // Different user - return error with details about which account is duplicate
                    var duplicateAccounts = new List<string>();
                    if (!string.IsNullOrEmpty(influencer.Instagram) && existingInfluencer.Instagram == influencer.Instagram)
                        duplicateAccounts.Add($"Instagram: {influencer.Instagram}");
                    if (!string.IsNullOrEmpty(influencer.YouTube) && existingInfluencer.YouTube == influencer.YouTube)
                        duplicateAccounts.Add($"YouTube: {influencer.YouTube}");
                    if (!string.IsNullOrEmpty(influencer.TikTok) && existingInfluencer.TikTok == influencer.TikTok)
                        duplicateAccounts.Add($"TikTok: {influencer.TikTok}");
                    if (!string.IsNullOrEmpty(influencer.Facebook) && existingInfluencer.Facebook == influencer.Facebook)
                        duplicateAccounts.Add($"Facebook: {influencer.Facebook}");

                    return StatusCode(400, new {
                        message = "One or more social media accounts are already registered with another user",
                        code = "SOCIAL_ACCOUNT_ALREADY_EXISTS",
                        duplicateAccounts = duplicateAccounts
                    });
                }
            }
            
            // Get follower counts from the follower service (currently Social Blade)
            var followerResults = await _followerCountService.GetAllPlatformFollowersAsync(
                instagramUsername: influencer.Instagram,
                youtubeChannelId: influencer.YouTube,
                tiktokUsername: influencer.TikTok,
                facebookUsername: influencer.Facebook
            );

            // Check for errors and set follower counts. Instagram/TikTok are required —
            // a failed lookup there must block signup (the user needs to know exactly which
            // handle is wrong). YouTube/Facebook are optional — a failed lookup there is only
            // a non-blocking warning, since the account itself isn't mandatory.
            var errors = new List<string>();
            var requiredAccountErrors = new Dictionary<string, string>();

            Console.WriteLine($"Processing follower results. Total platforms: {followerResults.Count}");

            // Instagram (required)
            if (followerResults.ContainsKey("Instagram"))
            {
                var result = followerResults["Instagram"];
                Console.WriteLine($"Instagram - Success: {result.Success}, Followers: {result.Followers}, Provided: '{influencer.Instagram}'");

                if (!string.IsNullOrEmpty(influencer.Instagram))
                {
                    if (result.Success && result.Followers > 0)
                    {
                        influencer.InstagramFollower = (int)result.Followers;
                    }
                    else
                    {
                        requiredAccountErrors["instagram"] =
                            $"We couldn't find an Instagram account for \"{influencer.Instagram}\". Please check the handle and try again.";
                    }
                }
            }

            // YouTube (optional)
            if (followerResults.ContainsKey("YouTube"))
            {
                var result = followerResults["YouTube"];
                Console.WriteLine($"YouTube - Success: {result.Success}, Followers: {result.Followers}, Provided: '{influencer.YouTube}'");

                if (!string.IsNullOrEmpty(influencer.YouTube))
                {
                    if (result.Success && result.Followers > 0)
                    {
                        influencer.YouTubeFollower = (int)result.Followers;
                    }
                    else
                    {
                        errors.Add($"YouTube account '{influencer.YouTube}': {(result.Success ? $"No followers found (got {result.Followers})" : result.ErrorMessage)}");
                    }
                }
            }

            // TikTok (required)
            if (followerResults.ContainsKey("TikTok"))
            {
                var result = followerResults["TikTok"];
                Console.WriteLine($"TikTok - Success: {result.Success}, Followers: {result.Followers}, Provided: '{influencer.TikTok}'");

                if (!string.IsNullOrEmpty(influencer.TikTok))
                {
                    if (result.Success && result.Followers > 0)
                    {
                        influencer.TikTokFollower = (int)result.Followers;
                    }
                    else
                    {
                        requiredAccountErrors["tiktok"] =
                            $"We couldn't find a TikTok account for \"{influencer.TikTok}\". Please check the handle and try again.";
                    }
                }
            }

            // Facebook (optional)
            if (followerResults.ContainsKey("Facebook"))
            {
                var result = followerResults["Facebook"];
                Console.WriteLine($"Facebook - Success: {result.Success}, Followers: {result.Followers}, Provided: '{influencer.Facebook}'");

                if (!string.IsNullOrEmpty(influencer.Facebook))
                {
                    if (result.Success && result.Followers > 0)
                    {
                        influencer.FacebookFollower = (int)result.Followers;
                    }
                    else
                    {
                        errors.Add($"Facebook account '{influencer.Facebook}': {(result.Success ? $"No followers found (got {result.Followers})" : result.ErrorMessage)}");
                    }
                }
            }

            // A required handle that was entered but couldn't actually be found blocks
            // signup outright — the account not existing is functionally the same as it
            // being missing, and the user needs a clear, specific reason why they can't move on.
            if (requiredAccountErrors.Any())
            {
                Console.WriteLine("Required social account(s) could not be verified, returning 400:");
                foreach (var kv in requiredAccountErrors)
                    Console.WriteLine($"  - {kv.Key}: {kv.Value}");

                return StatusCode(400, new {
                    message = "We couldn't verify one or more of your required social accounts. Please check the handles below and try again.",
                    code = "REQUIRED_SOCIAL_ACCOUNT_NOT_FOUND",
                    errors = requiredAccountErrors.Values.ToArray(),
                    fieldErrors = requiredAccountErrors
                });
            }
            
            // Instagram and TikTok are the required minimum; YouTube and Facebook are optional.
            var missingRequired = new List<string>();
            if (string.IsNullOrEmpty(influencer.Instagram)) missingRequired.Add("Instagram");
            if (string.IsNullOrEmpty(influencer.TikTok)) missingRequired.Add("TikTok");

            Console.WriteLine($"Total errors found: {errors.Count}, Missing required accounts: {string.Join(", ", missingRequired)}");

            if (missingRequired.Any())
            {
                Console.WriteLine("Required social accounts missing, returning 400:");

                return StatusCode(400, new {
                    message = $"Please provide your {string.Join(" and ", missingRequired)} handle{(missingRequired.Count > 1 ? "s" : "")} — these are required.",
                    code = "SOCIAL_MEDIA_VALIDATION_FAILED",
                    errors = missingRequired.Select(platform => $"{platform} handle is required").ToArray()
                });
            }

            // Determine if this is an update or create
            bool isUpdate = existingInfluencer != null;
            Influencer savedInfluencer;

            if (isUpdate)
            {
                Console.WriteLine($"Updating existing influencer with ID: {influencer.Id}");
                await _influencerService.UpdateInfluencer(influencer.UserId, influencer);
                savedInfluencer = influencer;
            }
            else
            {
                Console.WriteLine("Creating influencer (allowing external API failures)...");
                savedInfluencer = await _influencerService.CreateInfluencer(influencer);
                Console.WriteLine($"Influencer created with ID: {savedInfluencer.Id}");
            }

            // Always return success if we got here - include warnings if there were errors
            if (errors.Any())
            {
                Console.WriteLine("Returning success with warnings:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error}");
                }

                return StatusCode(isUpdate ? 200 : 201, new {
                    message = isUpdate ? "Social media accounts updated with validation warnings" : "Social media accounts added with validation warnings",
                    code = Message.INFLUENCER_CREATED_SUCCESSFULLY,
                    influencer = savedInfluencer,
                    warnings = errors
                });
            }

            return StatusCode(isUpdate ? 200 : 201,  new {
                message = isUpdate ? "Social media accounts updated successfully" : "Social media accounts added successfully",
                code = Message.INFLUENCER_CREATED_SUCCESSFULLY,
                influencer = savedInfluencer
            });
        }

        [HttpPut("updateInfluencer/{userId}")]
        public async Task<IActionResult> UpdateInfluencer(int userId, [FromBody] UpdateModel influencer)
        {
            User user = await _userService.GetUserById(userId);
            if (user == null)
                return StatusCode(404, new {
                    message = "User not found",
                    code = Message.USER_NOT_FOUND
                });

            var userUpdateDto = new UpdateUserDto
            {
                UserName = influencer.UserName,
                Name = influencer.Name,
                Email = influencer.Email,
                Password = influencer.Password
            };

            var (success, errorMessage) = await _userService.UpdateUser(userId, userUpdateDto);
            if (!success)
                return StatusCode(500, new {
                    message = errorMessage ?? "Failed to update user information",
                    code = Message.INFLUENCER_USER_UPDATE_FAILED
                });

            if (influencer.Bio == null)
            {
                return NoContent();
            }
            
            var updated = await _influencerService.UpdateInfluencer(userId, new Influencer{Bio = influencer.Bio, UserId = userId});
            if (!updated)
                return StatusCode(500, new { 
                    message = "Failed to update influencer profile",
                    code = Message.INFLUENCER_UPDATE_FAILED 
                });
            
            return NoContent();
        }

        [HttpDelete("deleteInfluencer/{userId}")]
        public async Task<IActionResult> DeleteInfluencer(int userId)
        {
            Influencer influencer = await _influencerService.GetInfluencerBasicByUserId(userId);
            if(influencer == null)
                return StatusCode(400, new { message = Message.INFLUENCER_NOT_FOUND });
                
            var deleted = await _influencerService.DeleteInfluencer(influencer.Id);
            if (!deleted)
                return StatusCode(500, new { message = Message.INFLUENCER_DELETE_FAILED });
                
            User user = await _userService.GetUserById(userId);
            if (user == null)
                return StatusCode(400, new { message = Message.INFLUENCER_NOT_IN_USER_TABLE });
                
            var deletedUser = await _userService.DeleteUser(userId);
            if (!deletedUser)
                return StatusCode(500, new { message = Message.INFLUENCER_USER_DELETE_FAILED });

            return NoContent();
        }
        
    }
}
