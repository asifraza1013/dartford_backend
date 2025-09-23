using inflan_api.Services;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;

namespace inflan_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly IInfluencerService _influencerService;
        private readonly IPlanService _planService;
 
        public AuthController(IUserService userService, IAuthService authService, IInfluencerService influencerService, IPlanService planService)
        {
            _userService = userService;
            _authService = authService;
            _influencerService = influencerService;
            _planService = planService;
        }

        [HttpGet("getUser/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _userService.GetUserById(id);
            if (user == null)
                return StatusCode(404, new { 
                    message = "User not found",
                    code = Message.USER_NOT_FOUND 
                });
            
            return Ok(user);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel login)
        {
            User? user = await _userService.ValidateUserAsync(login.Email, login.Password);
            if (user == null)
                return StatusCode(401, new { 
                    message = MessageHelper.GetMessageText(Message.INVALID_EMAIL_PASSWORD),
                    code = MessageHelper.GetMessageCode(Message.INVALID_EMAIL_PASSWORD) 
                });

            var token = _authService.GenerateJwtToken(user);
            int userType = user.UserType;

            if (userType == (int)UserType.BRAND)
            {
                bool brandInfoFilled = !string.IsNullOrWhiteSpace(user.BrandCategory)
                                       && !string.IsNullOrWhiteSpace(user.BrandSector)
                                       && user.Goals != null
                                       && user.Goals.Any();

                if (!brandInfoFilled)
                {
                    return StatusCode(200, new
                    {
                        token,
                        user,
                        message = "Please complete your brand profile",
                        code = Message.BRAND_INFO_NOT_FILLED,
                        missingStep = "Goals, Sector or Category missing"
                    });
                }
            }else if (user.UserType == (int)UserType.INFLUENCER)
            {
                var influencer = await _influencerService.GetInfluencerByUserId(user.Id);
                if (influencer == null)
                {
                    return StatusCode(200, new
                    {
                        token,
                        user,
                        message = "Please add your social media accounts",
                        code = Message.INFLUENCER_INFO_NOT_FILLED,
                        missingStep = "Socials missing"
                    });
                }
                // Note: We don't check for plans here because user might be in the process of creating them
                // The frontend will handle navigation to package creation if needed
            }
            
            return Ok(new
            {
                token = token,
                user = user
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            var existing = await _userService.GetByEmailAsync(user.Email);
            if (existing != null)
                return StatusCode(400, new { 
                    message = MessageHelper.GetMessageText(Message.EMAIL_ALREADY_REGISTERED),
                    code = MessageHelper.GetMessageCode(Message.EMAIL_ALREADY_REGISTERED) 
                });

            user.Status = (int)Status.ACTIVE;
            try
            {
                user.UserName = await _userService.GenerateUniqueUsernameFromNameAsync(user.Name);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Failed to generate username. Please try again.",
                    code = "USERNAME_GENERATION_ERROR",
                    details = ex.Message 
                });
            }
            var result = await _authService.RegisterAsync(user);
            var token = _authService.GenerateJwtToken(user);
            return Ok(new
            {
                Token = token,
                User = result
            });
        }
    }
}
