using System.Security.Claims;
using inflan_api.Interfaces;
using inflan_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace inflan_api.Controllers
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
                return StatusCode(404, new { 
                    message = "Campaign not found",
                    code = "CAMPAIGN_NOT_FOUND" 
                });

            return Ok(campaign);
        }

        [HttpPost("createNewCampaign")]
        [Authorize]
        public async Task<IActionResult> CreateCampaign([FromBody] Campaign campaign)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new { 
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN" 
                });

            int userId = int.Parse(userIdClaim.Value);
            campaign.BrandId = userId;
            var created = await _campaignService.CreateCampaign(campaign);
            if(created == null) return StatusCode(400, new { 
                message = "Campaign creation failed. Please ensure the selected influencer has active plans.",
                code = "CAMPAIGN_CREATION_FAILED_PLAN_MISSING" 
            });
            return StatusCode(201, new { 
                message = "Campaign created successfully",
                campaign = created 
            });
        }

        [HttpPut("updateCampaign/{id}")]
        public async Task<IActionResult> UpdateCampaign(int id, [FromBody] Campaign campaign)
        {
            var updated = await _campaignService.UpdateCampaign(id, campaign);
            if (!updated)
                return StatusCode(500, new { 
                    message = "Failed to update campaign",
                    code = "CAMPAIGN_UPDATE_FAILED" 
                });

            return StatusCode(200, new {
                message = "Campaign updated successfully",
                code = "CAMPAIGN_UPDATE_SUCCESS" 
            });
        }
        [HttpGet("getInfluencerCampaigns/{influencerId}")]
        public async Task<IActionResult> GetInfluencerCampaigns(int influencerId)
        {
            var campaigns = await _campaignService.GetCampaignsByInfluencerId(influencerId);

            if (!campaigns.Any())
                return StatusCode(404, new { 
                    message = "No campaigns found for this influencer",
                    code = "NO_CAMPAIGNS_FOUND" 
                });

            return Ok(campaigns);
        }
        [HttpGet("getBrandCampaigns/{brandId}")]
        public async Task<IActionResult> GetBrandCampaigns(int brandId)
        {
            var campaigns = await _campaignService.GetCampaignsByBrandId(brandId);

            if (!campaigns.Any())
                return StatusCode(404, new { 
                    message = "No campaigns found for this brand",
                    code = "NO_CAMPAIGNS_FOUND" 
                });

            return Ok(campaigns);
        }
        
        [HttpGet("getCompletedPaymentBrandCampaigns/{brandId}")]
        public async Task<IActionResult> GetCompletedPaymentBrandCampaigns(int brandId)
        {
            var campaigns = await _campaignService.GetCompletedPaymentCampaignsByBrandId(brandId);

            if (!campaigns.Any())
                return StatusCode(404, new { 
                    message = "No completed payment campaigns found for this brand",
                    code = "NO_COMPLETED_CAMPAIGNS_FOUND" 
                });

            return Ok(campaigns);
        }
        
        [HttpGet("getInfluencerCampaignsByStatusFilter/{influencerId}/{campaignStatus}")]
        public async Task<IActionResult> GetInfluencerCampaignsByStatus(int influencerId, int campaignStatus)
        {
            var campaigns = await _campaignService.GetCampaignsByInfluencerAndStatus(influencerId, campaignStatus);

            if (!campaigns.Any())
                return NotFound(new { 
                    message = "No campaigns found with the given status for this influencer",
                    code = "NO_CAMPAIGNS_WITH_STATUS" 
                });

            return Ok(campaigns);
        }
        
        [HttpPost("uploadCampaignDocuments")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadCampaignDocuments([FromForm] List<IFormFile> files)
        {
            var filePaths = await _campaignService.SaveCampaignDocumentsAsync(files);

            if (filePaths.Count == 0)
                return StatusCode(400, new { 
                    message = "No valid files were uploaded. Please select files and try again.",
                    code = "NO_VALID_FILES" 
                });

            return Ok(new { 
                message = "Files uploaded successfully",
                files = filePaths 
            });
        }
        [HttpDelete("deleteCampaignDocuments")]
        public async Task<IActionResult> DeleteCampaignDocuments([FromBody] List<string> documentPaths)
        {
            var result = await _campaignService.DeleteCampaignDocumentsAsync(documentPaths);

            if (!result)
                return StatusCode(400, new { 
                    message = "Some or all documents could not be deleted. Please verify the file paths.",
                    code = "DELETE_DOCUMENTS_FAILED" 
                });

            return Ok(new { 
                message = "Documents deleted successfully",
                code = "DELETE_DOCUMENTS_SUCCESS" 
            });
        }


    }
}
