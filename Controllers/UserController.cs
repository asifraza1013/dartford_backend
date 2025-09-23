using System.Security.Claims;
using inflan_api.Services;
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
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IInfluencerService _influencerService;
        private readonly IPlanService _planService;

        public UserController(IUserService userService,  IInfluencerService influencerService, IPlanService planService)
        {
            _userService = userService;
            _influencerService = influencerService;
            _planService = planService;
        }

        [HttpGet("getAllUsers")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsers();
            return Ok(users);
        }

        [HttpGet("getCurrentUser")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new { 
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN" 
                });
            
            int userId = int.Parse(userIdClaim.Value);
            var user = await _userService.GetUserById(userId);
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
                        user,
                        message = "Please complete your brand profile",
                        code = Message.BRAND_INFO_NOT_FILLED,
                        missingStep = "Goals or Category missing"
                    });
                }
            }else if (user.UserType == (int)UserType.INFLUENCER)
            {
                var influencer = await _influencerService.GetInfluencerByUserId(user.Id);
                if (influencer == null)
                {
                    return StatusCode(200, new
                    {
                        user,
                        message = "Please add your social media accounts",
                        code = Message.INFLUENCER_INFO_NOT_FILLED,
                        missingStep = "Socials missing"
                    });
                }
                var influencerPlans = await _planService.GetPlansByUserId(user.Id);
                if (influencerPlans == null || !influencerPlans.Any())
                {
                    return StatusCode(200, new
                    {
                        user,
                        message = "Please create at least one service plan",
                        code = Message.INFLUENCER_INFO_NOT_FILLED,
                        missingStep = "Plans missing"
                    });
                }
            }
            return user == null ? StatusCode(404, new { 
                message = "User not found",
                code = Message.USER_NOT_FOUND 
            }) : Ok(user);
        }
        
        [Authorize]
        [HttpPost("uploadProfilePicture")]
        public async Task<IActionResult> UploadProfilePicture(IFormFile file)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized(new { 
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN" 
                });

            int userId = int.Parse(userIdClaim.Value);

            var relativePath = await _userService.SaveOrUpdateProfilePictureAsync(userId, file);

            if (relativePath == null)
                return BadRequest(new { 
                    message = "Invalid file format or size",
                    code = "INVALID_FILE" 
                });

            var user = await _userService.GetUserById(userId);
            if (user == null)
                return NotFound();

            user.ProfileImage = relativePath;
            await _userService.UpdateUser(userId, user);

            return Ok(new { message = "Profile picture uploaded", path = relativePath });
        }



        [HttpGet("getUserById/{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _userService.GetUserById(id);
            if (user == null)
                return StatusCode(404, new { 
                    message = "User not found",
                    code = Message.USER_NOT_FOUND 
                });

            return Ok(user);
        }

        [HttpPost("createNewUser")]
        public async Task<IActionResult> CreateUser([FromBody] User user)
        {
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

            var createdUser = await _userService.CreateUser(user);
            return StatusCode(201,  new { 
                message = "User created successfully",
                code = Message.USER_CREATED_SUCCESSFULLY, 
                user = createdUser
            });
        }

        [HttpPut("updateUser/{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateUser(int id, [FromForm] User user, IFormFile? file)
        {
            var newImagePath = await _userService.SaveOrUpdateProfilePictureAsync(id, file);
            if (newImagePath != null)
                user.ProfileImage = newImagePath;
            var updated = await _userService.UpdateUser(id, user);
            if (!updated)
                return StatusCode(500,  new { 
                    message = "Failed to update user information",
                    code = Message.USER_UPDATE_FAILED 
                });

            return StatusCode(200, new { 
                message = "User updated successfully",
                code = Message.USER_UPDATED_SUCCESSFULLY 
            });
        }

        [HttpDelete("deleteUser/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var deleted = await _userService.DeleteUser(id);
            if (!deleted)
                return StatusCode(500, new { 
                    message = "Failed to delete user",
                    code = Message.USER_DELETE_FAILED 
                });

            return NoContent();
        }
    }
}
