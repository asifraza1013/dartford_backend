﻿using System.Security.Claims;
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
                return StatusCode(401, new { message = "Unauthorized: UserId not found in token" });
            
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
                        message = Message.BRAND_INFO_NOT_FILLED,
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
                        message = Message.INFLUENCER_INFO_NOT_FILLED,
                        missingStep = "Socials missing"
                    });
                }
                var influencerPlans = await _planService.GetPlansByUserId(user.Id);
                if (influencerPlans == null || !influencerPlans.Any())
                {
                    return StatusCode(200, new
                    {
                        user,
                        message = Message.INFLUENCER_INFO_NOT_FILLED,
                        missingStep = "Plans missing"
                    });
                }
            }
            return user == null ? StatusCode(400, new { message = Message.USER_NOT_FOUND }) : Ok(user);
        }

        [HttpGet("getUserById/{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _userService.GetUserById(id);
            if (user == null)
                return StatusCode(400, new { message = Message.USER_NOT_FOUND });

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
                return StatusCode(500, new { message = ex.Message });
            }

            var createdUser = await _userService.CreateUser(user);
            return StatusCode(200,  new { message = Message.USER_CREATED_SUCCESSFULLY, user = createdUser});
        }

        [HttpPut("updateUser/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] User user)
        {
            var updated = await _userService.UpdateUser(id, user);
            if (!updated)
                return StatusCode(500,  new { message = Message.USER_UPDATE_FAILED });

            return NoContent();
        }

        [HttpDelete("deleteUser/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var deleted = await _userService.DeleteUser(id);
            if (!deleted)
                return StatusCode(500, new { message = Message.USER_DELETE_FAILED });

            return NoContent();
        }
    }
}
