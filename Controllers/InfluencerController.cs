using dartford_api.Interfaces;
using dartford_api.Models;
using dartford_api.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace dartford_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InfluencerController : ControllerBase
    {
        private readonly IInfluencerService _influencerService;
        private readonly IUserService _userService;

        public InfluencerController(IInfluencerService influencerService,  IUserService userService)
        {
            _influencerService = influencerService;
            _userService = userService;
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
        public async Task<IActionResult> CreateInfluencer([FromBody] Influencer influencer)
        {
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
