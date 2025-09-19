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
            return influencer != null ? Ok(influencer) : StatusCode(401, new { message = Message.INFLUENCER_NOT_FOUND });
        }

        [HttpPost("createNewInfluencer")]
        [Authorize]
        public async Task<IActionResult> CreateInfluencer([FromBody] Influencer influencer)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new { message = "Unauthorized: UserId not found in token" });

            int userId = int.Parse(userIdClaim.Value);
            influencer.UserId = userId;
            
            // Get follower counts from the follower service (currently Social Blade)
            var followerResults = await _followerCountService.GetAllPlatformFollowersAsync(
                instagramUsername: influencer.Instagram,
                twitterUsername: influencer.Twitter, // This will now contain YouTube channel name
                tiktokUsername: influencer.TikTok,
                facebookUsername: influencer.Facebook
            );

            // Check for errors and set follower counts
            var errors = new List<string>();
            
            // Instagram
            if (followerResults.ContainsKey("Instagram"))
            {
                var result = followerResults["Instagram"];
                if (result.Success)
                {
                    influencer.InstagramFollower = (int)result.Followers;
                }
                else if (!string.IsNullOrEmpty(influencer.Instagram))
                {
                    errors.Add($"Instagram: {result.ErrorMessage}");
                }
            }
            
            // YouTube (stored in Twitter field)
            if (followerResults.ContainsKey("Twitter"))
            {
                var result = followerResults["Twitter"];
                if (result.Success)
                {
                    influencer.TwitterFollower = (int)result.Followers;
                }
                else if (!string.IsNullOrEmpty(influencer.Twitter))
                {
                    errors.Add($"YouTube: {result.ErrorMessage}");
                }
            }
            
            // TikTok
            if (followerResults.ContainsKey("TikTok"))
            {
                var result = followerResults["TikTok"];
                if (result.Success)
                {
                    influencer.TikTokFollower = (int)result.Followers;
                }
                else if (!string.IsNullOrEmpty(influencer.TikTok))
                {
                    errors.Add($"TikTok: {result.ErrorMessage}");
                }
            }
            
            // Facebook
            if (followerResults.ContainsKey("Facebook"))
            {
                var result = followerResults["Facebook"];
                if (result.Success)
                {
                    influencer.FacebookFollower = (int)result.Followers;
                }
                else if (!string.IsNullOrEmpty(influencer.Facebook))
                {
                    errors.Add($"Facebook: {result.ErrorMessage}");
                }
            }
            
            // If any platform failed to get follower count, log warnings but continue
            if (errors.Any())
            {
                foreach (var error in errors)
                {
                    Console.WriteLine($"Warning: {error}");
                }
            }
            var created = await _influencerService.CreateInfluencer(influencer);
            return StatusCode(200,  new { message = Message.INFLUENCER_CREATED_SUCCESSFULLY, influencer = created});
        }

        [HttpPut("updateInfluencer/{userId}")]
        public async Task<IActionResult> UpdateInfluencer(int userId, [FromBody] UpdateModel influencer)
        {
            User user = await _userService.GetUserById(userId);
            if (user == null)
                return StatusCode(400, new { message = Message.USER_NOT_FOUND });

            user.UserName = influencer.UserName ??  user.UserName;
            user.Name = influencer.Name ??  user.Name;
            user.Email = influencer.Email ??  user.Email;
            user.Password = influencer.Password ?? user.Password;
            
            var updatedUser = await _userService.UpdateUser(userId, user);
            if (updatedUser == false)
                return StatusCode(500, new { message = Message.INFLUENCER_USER_UPDATE_FAILED });

            if (influencer.Bio == null)
            {
                return NoContent();
            }
            
            var updated = await _influencerService.UpdateInfluencer(userId, new Influencer{Bio = influencer.Bio, UserId = userId});
            if (!updated)
                return StatusCode(500, new { message = Message.INFLUENCER_UPDATE_FAILED });
            
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
