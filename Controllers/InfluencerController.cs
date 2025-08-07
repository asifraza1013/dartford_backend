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
        private readonly string authToken;

        public InfluencerController(IInfluencerService influencerService,  IUserService userService, IConfiguration configuration)
        {
            _influencerService = influencerService;
            _userService = userService;
            authToken = configuration["ZylaLab:AuthToken"];
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
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            var twitterRequest = httpClient.PostAsJsonAsync(
                "https://zylalabs.com/api/9043/twitter+user+profiles+extract+api/16280/get+follower+count+by+username",
                new { username = influencer.Twitter });

            var tiktokRequest = httpClient.PostAsJsonAsync(
                "https://zylalabs.com/api/9008/tiktok+influencer+profile+data+api/16153/get+ranking+and+followers+by+username",
                new { username = influencer.TikTok });

            var instagramRequest = httpClient.PostAsJsonAsync(
                "https://zylalabs.com/api/9059/instagram+influencer+insights+api/16324/get+influencer+profile+by+username",
                new { influencer_username = influencer.Instagram });

            var responses = await Task.WhenAll(twitterRequest, tiktokRequest, instagramRequest);
            
            var twitterJson = await _influencerService.SafeParseJsonAsync(responses[0], "Twitter");
            var tiktokJson = await _influencerService.SafeParseJsonAsync(responses[1], "TikTok");
            var instagramJson = await _influencerService.SafeParseJsonAsync(responses[2], "Instagram");

            if (twitterJson.error != null) return StatusCode(400, new { message = twitterJson.error });
            if (tiktokJson.error != null) return StatusCode(400, new { message = tiktokJson.error });
            if (instagramJson.error != null) return StatusCode(400, new { message = instagramJson.error });
            
            influencer.TwitterFollower = _influencerService.ParseFollowersFromTwitter(twitterJson.data);

            influencer.TikTokFollower = _influencerService.ParseFollowersFromTikTok(tiktokJson.data);

            influencer.InstagramFollower = _influencerService.ParseFollowersFromInstagram(instagramJson.data);
            influencer.FacebookFollower = new Random().Next(10000, 1000000);
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
