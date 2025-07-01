using System.Security.Claims;
using dartford_api.Interfaces;
using dartford_api.Models;
using dartford_api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace dartford_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlanController : ControllerBase
    {
        private readonly IPlanService _planService;

        public PlanController(IPlanService planService)
        {
            _planService = planService;
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
                return StatusCode(400, new { message = Message.PLAN_NOT_FOUND });

            return Ok(plan);
        }

        [HttpPost("createNewPlan")]
        [Authorize]
        public async Task<IActionResult> CreatePlan([FromBody] Plan plan)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new { message = "Unauthorized: UserId not found in token" });

            int userId = int.Parse(userIdClaim.Value);
            plan.UserId = userId;
            var created = await _planService.CreatePlan(plan);
            return StatusCode(200, new { message = Message.PLAN_CREATED_SUCCESSFULLY, plan = created });
        }

        [HttpPut("updatePlan/{id}")]
        public async Task<IActionResult> UpdatePlan(int id, [FromBody] Plan plan)
        {
            var updated = await _planService.UpdatePlan(id, plan);
            if (!updated)
                return StatusCode(500, new { message = Message.PLAN_UPDATE_FAILED });

            return NoContent();
        }

        [HttpDelete("deletePlan/{id}")]
        public async Task<IActionResult> DeletePlan(int id)
        {
            var deleted = await _planService.DeletePlan(id);
            if (!deleted)
                return StatusCode(500, new { message = Message.PLAN_DELETE_FAILED });

            return NoContent();
        }
        [HttpGet("getCurrentInfluencerPlans")]
        [Authorize]
        public async Task<IActionResult> GetInfluencerPlans()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new { message = "Unauthorized: UserId not found in token" });

            int userId = int.Parse(userIdClaim.Value);
            var plans = await _planService.GetPlansByUserId(userId);

            return Ok(plans);
        }
        [HttpGet("getPlansByInfluencerUserId/{userId}")]
        public async Task<IActionResult> GetPlansByInfluencerUserId(int userId)
        {
            var plans = await _planService.GetPlansByUserId(userId);

            if (!plans.Any())
                return NotFound(new { message = "No plans found for this influencer." });

            return Ok(plans);
        }


    }
}
