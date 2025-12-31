using System.Security.Claims;
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
    public class PlanController : ControllerBase
    {
        private readonly IPlanService _planService;
        private readonly IUserService _userService;

        public PlanController(IPlanService planService, IUserService userService)
        {
            _planService = planService;
            _userService = userService;
        }

        /// <summary>
        /// Gets the currency based on user's location
        /// </summary>
        private string GetCurrencyFromLocation(string? location)
        {
            return location?.ToUpper() switch
            {
                "GB" => "GBP",
                "NG" => "NGN",
                _ => "NGN" // Default to NGN
            };
        }

        [HttpGet("getAllPlans")]
        public async Task<IActionResult> GetAllPlans()
        {
            var plans = await _planService.GetAllPlans();
            return Ok(plans);
        }

        [HttpGet("getPlanById/{id}")]
        public async Task<IActionResult> GetPlanById(int id)
        {
            var plan = await _planService.GetPlanById(id);
            if (plan == null)
                return StatusCode(404, new { 
                    message = "Plan not found",
                    code = Message.PLAN_NOT_FOUND 
                });

            return Ok(plan);
        }

        [HttpPost("createNewPlan")]
        [Authorize]
        public async Task<IActionResult> CreatePlan([FromBody] Plan plan)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new {
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN"
                });

            int userId = int.Parse(userIdClaim.Value);
            plan.UserId = userId;

            // Auto-set currency based on user's location
            var user = await _userService.GetUserById(userId);
            plan.Currency = GetCurrencyFromLocation(user?.Location);

            var created = await _planService.CreatePlan(plan);
            return StatusCode(201, new {
                message = "Plan created successfully",
                code = Message.PLAN_CREATED_SUCCESSFULLY,
                plan = created
            });
        }

        [HttpPut("updatePlan/{id}")]
        public async Task<IActionResult> UpdatePlan(int id, [FromBody] Plan plan)
        {
            var updated = await _planService.UpdatePlan(id, plan);
            if (!updated)
                return StatusCode(500, new { 
                    message = "Failed to update plan",
                    code = Message.PLAN_UPDATE_FAILED 
                });

            return NoContent();
        }

        [HttpDelete("deletePlan/{id}")]
        public async Task<IActionResult> DeletePlan(int id)
        {
            var deleted = await _planService.DeletePlan(id);
            if (!deleted)
                return StatusCode(500, new { 
                    message = "Failed to delete plan",
                    code = Message.PLAN_DELETE_FAILED 
                });

            return NoContent();
        }
        [HttpGet("getCurrentInfluencerPlans")]
        [Authorize]
        public async Task<IActionResult> GetInfluencerPlans()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new { 
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN" 
                });

            int userId = int.Parse(userIdClaim.Value);
            var plans = await _planService.GetPlansByUserId(userId);

            return Ok(plans);
        }
        [HttpGet("getPlansByInfluencerUserId/{userId}")]
        public async Task<IActionResult> GetPlansByInfluencerUserId(int userId)
        {
            var plans = await _planService.GetPlansByUserId(userId);

            if (!plans.Any())
                return NotFound(new { 
                    message = "No plans found for this influencer",
                    code = "NO_PLANS_FOUND" 
                });

            return Ok(plans);
        }


    }
}
