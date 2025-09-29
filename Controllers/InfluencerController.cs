using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using inflan_api.Interfaces;
using inflan_api.Models;
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
        public async Task<IActionResult> GetAllInfluencers()
        {
            var influencers = await _influencerService.GetAllInfluencers();
            return Ok(influencers);
        }

        [HttpGet("getInfluencerById/{id}")]
        public async Task<IActionResult> GetInfluencerById(int id)
        {
            var influencer = await _influencerService.GetInfluencerById(id);
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
            
            // Get follower counts from the follower service (currently Social Blade)
            var followerResults = await _followerCountService.GetAllPlatformFollowersAsync(
                instagramUsername: influencer.Instagram,
                youtubeChannelId: influencer.YouTube,
                tiktokUsername: influencer.TikTok,
                facebookUsername: influencer.Facebook
            );

            // Check for errors and set follower counts
            var errors = new List<string>();
            
            Console.WriteLine($"Processing follower results. Total platforms: {followerResults.Count}");
            
            // Instagram
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
                        errors.Add($"Instagram account '{influencer.Instagram}': {(result.Success ? $"No followers found (got {result.Followers})" : result.ErrorMessage)}");
                    }
                }
            }
            
            // YouTube
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
            
            // TikTok
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
                        errors.Add($"TikTok account '{influencer.TikTok}': {(result.Success ? $"No followers found (got {result.Followers})" : result.ErrorMessage)}");
                    }
                }
            }
            
            // Facebook
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
            
            // SIMPLIFIED APPROACH: Only block if NO social accounts provided at all
            // Everything else is allowed - SocialBlade issues should not block users
            bool allAccountsEmpty = string.IsNullOrEmpty(influencer.Instagram) &&
                                   string.IsNullOrEmpty(influencer.YouTube) &&
                                   string.IsNullOrEmpty(influencer.TikTok) &&
                                   string.IsNullOrEmpty(influencer.Facebook);

            Console.WriteLine($"Total errors found: {errors.Count}, All accounts empty: {allAccountsEmpty}");
            
            if (allAccountsEmpty)
            {
                Console.WriteLine("No social accounts provided, returning 400:");
                
                return StatusCode(400, new {
                    message = "Please provide at least one social media account.",
                    code = "SOCIAL_MEDIA_VALIDATION_FAILED",
                    errors = new[] { "No social media accounts provided" }
                });
            }
            
            Console.WriteLine("Creating influencer (allowing external API failures)...");
            var created = await _influencerService.CreateInfluencer(influencer);
            Console.WriteLine($"Influencer created with ID: {created.Id}");
            
            // Always return success if we got here - include warnings if there were errors
            if (errors.Any())
            {
                Console.WriteLine("Returning success with warnings:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error}");
                }
                
                return StatusCode(201, new { 
                    message = "Social media accounts added with validation warnings",
                    code = Message.INFLUENCER_CREATED_SUCCESSFULLY, 
                    influencer = created,
                    warnings = errors
                });
            }
            
            return StatusCode(201,  new { 
                message = "Social media accounts added successfully",
                code = Message.INFLUENCER_CREATED_SUCCESSFULLY, 
                influencer = created
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

            user.UserName = influencer.UserName ??  user.UserName;
            user.Name = influencer.Name ??  user.Name;
            user.Email = influencer.Email ??  user.Email;
            user.Password = influencer.Password ?? user.Password;
            
            var updatedUser = await _userService.UpdateUser(userId, user);
            if (updatedUser == false)
                return StatusCode(500, new { 
                    message = "Failed to update user information",
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
            Influencer influencer = await _influencerService.GetInfluencerByUserId(userId);
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
