using System.Security.Claims;
using dartford_api.Interfaces;
using dartford_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dartford_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CampaignController : ControllerBase
    {
        private readonly ICampaignService _campaignService;

        public CampaignController(ICampaignService campaignService)
        {
            _campaignService = campaignService;
        }

        [HttpGet("getAllCampaigns")]
        public async Task<IActionResult> GetAllCampaigns()
        {
            var campaigns = await _campaignService.GetAllCampaigns();
            return Ok(campaigns);
        }

        [HttpGet("getCampaignById/{id}")]
        public async Task<IActionResult> GetCampaignById(int id)
        {
            var campaign = await _campaignService.GetCampaignById(id);
            if (campaign == null)
                return StatusCode(400, new { message = "Campaign not found" });

            return Ok(campaign);
        }

        [HttpPost("createNewCampaign")]
        [Authorize]
        public async Task<IActionResult> CreateCampaign([FromBody] Campaign campaign)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new { message = "Unauthorized: UserId not found in token" });

            int userId = int.Parse(userIdClaim.Value);
            campaign.BrandId = userId;
            var created = await _campaignService.CreateCampaign(campaign);
            return StatusCode(200, new { message = "Campaign created", campaign = created });
        }

        [HttpPut("updateCampaign/{id}")]
        public async Task<IActionResult> UpdateCampaign(int id, [FromBody] Campaign campaign)
        {
            var updated = await _campaignService.UpdateCampaign(id, campaign);
            if (!updated)
                return StatusCode(500, new { message = "Campaign update failed" });

            return NoContent();
        }
        [HttpGet("getInfluencerCampaigns/{influencerId}")]
        public async Task<IActionResult> GetInfluencerCampaigns(int influencerId)
        {
            var campaigns = await _campaignService.GetCampaignsByInfluencerId(influencerId);

            if (!campaigns.Any())
                return StatusCode(404, new { message = "No campaigns found for this influencer" });

            return Ok(campaigns);
        }
        [HttpGet("getInfluencerCampaignsByStatusFilter/{influencerId}/{campaignStatus}")]
        public async Task<IActionResult> GetInfluencerCampaignsByStatus(int influencerId, int campaignStatus)
        {
            var campaigns = await _campaignService.GetCampaignsByInfluencerAndStatus(influencerId, campaignStatus);

            if (!campaigns.Any())
                return NotFound(new { message = "No campaigns found with the given status for this influencer." });

            return Ok(campaigns);
        }

    }
}
