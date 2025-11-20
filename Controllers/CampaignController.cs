using System.Security.Claims;
using inflan_api.Interfaces;
using inflan_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace inflan_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CampaignController : ControllerBase
    {
        private readonly ICampaignService _campaignService;
        private readonly IUserService _userService;
        private readonly IInfluencerService _influencerService;

        public CampaignController(ICampaignService campaignService, IUserService userService, IInfluencerService influencerService)
        {
            _campaignService = campaignService;
            _userService = userService;
            _influencerService = influencerService;
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

            // Enrich with influencer data
            var influencerUser = await _userService.GetUserById(campaign.InfluencerId);
            var influencerRecord = await _influencerService.GetInfluencerByUserId(campaign.InfluencerId);

            var enrichedCampaign = new DTOs.CampaignResponseDto
            {
                Id = campaign.Id,
                PlanId = campaign.PlanId,
                ProjectName = campaign.ProjectName,
                AboutProject = campaign.AboutProject,
                CampaignStartDate = campaign.CampaignStartDate,
                CampaignEndDate = campaign.CampaignEndDate,
                ContentFiles = campaign.ContentFiles,
                InstructionDocuments = campaign.InstructionDocuments,
                BrandId = campaign.BrandId,
                InfluencerId = campaign.InfluencerId,
                InfluencerRecordId = influencerRecord?.Id,
                InfluencerName = influencerUser?.Name,
                CampaignStatus = campaign.CampaignStatus,
                PaymentStatus = campaign.PaymentStatus,
                GeneratedContractPdfPath = campaign.GeneratedContractPdfPath,
                SignedContractPdfPath = campaign.SignedContractPdfPath,
                ContractSignedAt = campaign.ContractSignedAt,
                Currency = campaign.Currency,
                Amount = campaign.Amount,
                CreatedAt = campaign.CreatedAt,
                InfluencerAcceptedAt = campaign.InfluencerAcceptedAt,
                PaymentCompletedAt = campaign.PaymentCompletedAt
            };

            return Ok(enrichedCampaign);
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

            // Set initial status to DRAFT
            campaign.CampaignStatus = 1; // DRAFT
            campaign.CreatedAt = DateTime.UtcNow;

            var created = await _campaignService.CreateCampaign(campaign);
            if(created == null) return StatusCode(400, new {
                message = "Campaign creation failed. The selected plan must have a valid price.",
                code = "CAMPAIGN_CREATION_FAILED_INVALID_PLAN"
            });
            return StatusCode(201, new {
                message = "Campaign created successfully. Waiting for influencer response.",
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
        public async Task<IActionResult> GetInfluencerCampaigns(
            int influencerId,
            [FromQuery] int? status = null,
            [FromQuery] string? searchQuery = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var campaigns = await _campaignService.GetCampaignsByInfluencerId(influencerId);

            // Apply filters
            if (status.HasValue)
                campaigns = campaigns.Where(c => c.CampaignStatus == status.Value);

            if (!string.IsNullOrWhiteSpace(searchQuery))
                campaigns = campaigns.Where(c => c.ProjectName != null && c.ProjectName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));

            if (startDate.HasValue)
                campaigns = campaigns.Where(c => c.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                campaigns = campaigns.Where(c => c.CreatedAt <= endDate.Value.AddDays(1)); // Include the entire end date

            var result = campaigns.ToList();

            // Return empty array if no campaigns found (not an error)
            if (!result.Any())
                return Ok(new List<DTOs.CampaignResponseDto>());

            // Enrich campaigns with brand names and logos
            var enrichedCampaigns = new List<DTOs.CampaignResponseDto>();
            foreach (var campaign in result)
            {
                var brandUser = await _userService.GetUserById(campaign.BrandId);
                var influencerUser = await _userService.GetUserById(campaign.InfluencerId);
                var influencerRecord = await _influencerService.GetInfluencerByUserId(campaign.InfluencerId);

                enrichedCampaigns.Add(new DTOs.CampaignResponseDto
                {
                    Id = campaign.Id,
                    PlanId = campaign.PlanId,
                    ProjectName = campaign.ProjectName,
                    AboutProject = campaign.AboutProject,
                    CampaignStartDate = campaign.CampaignStartDate,
                    CampaignEndDate = campaign.CampaignEndDate,
                    ContentFiles = campaign.ContentFiles,
                    InstructionDocuments = campaign.InstructionDocuments,
                    BrandId = campaign.BrandId,
                    BrandName = brandUser?.BrandName ?? brandUser?.Name,
                    BrandLogo = brandUser?.ProfileImage,
                    InfluencerId = campaign.InfluencerId,
                    InfluencerRecordId = influencerRecord?.Id,
                    InfluencerName = influencerUser?.Name,
                    CampaignStatus = campaign.CampaignStatus,
                    PaymentStatus = campaign.PaymentStatus,
                    GeneratedContractPdfPath = campaign.GeneratedContractPdfPath,
                    SignedContractPdfPath = campaign.SignedContractPdfPath,
                    ContractSignedAt = campaign.ContractSignedAt,
                    Currency = campaign.Currency,
                    Amount = campaign.Amount,
                    CreatedAt = campaign.CreatedAt,
                    InfluencerAcceptedAt = campaign.InfluencerAcceptedAt,
                    PaymentCompletedAt = campaign.PaymentCompletedAt
                });
            }

            return Ok(enrichedCampaigns);
        }

        [HttpGet("getBrandCampaigns/{brandId}")]
        public async Task<IActionResult> GetBrandCampaigns(
            int brandId,
            [FromQuery] int? status = null,
            [FromQuery] string? searchQuery = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var campaigns = await _campaignService.GetCampaignsByBrandId(brandId);

            // Apply filters
            if (status.HasValue)
                campaigns = campaigns.Where(c => c.CampaignStatus == status.Value);

            if (!string.IsNullOrWhiteSpace(searchQuery))
                campaigns = campaigns.Where(c => c.ProjectName != null && c.ProjectName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));

            if (startDate.HasValue)
                campaigns = campaigns.Where(c => c.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                campaigns = campaigns.Where(c => c.CreatedAt <= endDate.Value.AddDays(1)); // Include the entire end date

            var result = campaigns.ToList();

            // Return empty array if no campaigns found (not an error)
            if (!result.Any())
                return Ok(new List<DTOs.CampaignResponseDto>());

            // Enrich campaigns with influencer names and record IDs
            var enrichedCampaigns = new List<DTOs.CampaignResponseDto>();
            foreach (var campaign in result)
            {
                var brandUser = await _userService.GetUserById(campaign.BrandId);
                var influencerUser = await _userService.GetUserById(campaign.InfluencerId);
                var influencerRecord = await _influencerService.GetInfluencerByUserId(campaign.InfluencerId);

                enrichedCampaigns.Add(new DTOs.CampaignResponseDto
                {
                    Id = campaign.Id,
                    PlanId = campaign.PlanId,
                    ProjectName = campaign.ProjectName,
                    AboutProject = campaign.AboutProject,
                    CampaignStartDate = campaign.CampaignStartDate,
                    CampaignEndDate = campaign.CampaignEndDate,
                    ContentFiles = campaign.ContentFiles,
                    InstructionDocuments = campaign.InstructionDocuments,
                    BrandId = campaign.BrandId,
                    BrandName = brandUser?.BrandName ?? brandUser?.Name,
                    BrandLogo = brandUser?.ProfileImage,
                    InfluencerId = campaign.InfluencerId,
                    InfluencerRecordId = influencerRecord?.Id,
                    InfluencerName = influencerUser?.Name,
                    CampaignStatus = campaign.CampaignStatus,
                    PaymentStatus = campaign.PaymentStatus,
                    GeneratedContractPdfPath = campaign.GeneratedContractPdfPath,
                    SignedContractPdfPath = campaign.SignedContractPdfPath,
                    ContractSignedAt = campaign.ContractSignedAt,
                    Currency = campaign.Currency,
                    Amount = campaign.Amount,
                    CreatedAt = campaign.CreatedAt,
                    InfluencerAcceptedAt = campaign.InfluencerAcceptedAt,
                    PaymentCompletedAt = campaign.PaymentCompletedAt
                });
            }

            return Ok(enrichedCampaigns);
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

        [HttpPost("uploadContentFiles")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadContentFiles([FromForm] List<IFormFile> files)
        {
            var filePaths = await _campaignService.SaveContentFilesAsync(files);

            if (filePaths.Count == 0)
                return StatusCode(400, new {
                    message = "No valid content files were uploaded. Allowed formats: JPG, PNG, PDF, SVG, GIF. Maximum file size: 5MB",
                    code = "NO_VALID_FILES"
                });

            return Ok(new {
                message = "Content files uploaded successfully",
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

        // New Booking Workflow Endpoints

        [HttpPost("acceptCampaign/{campaignId}")]
        [Authorize]
        public async Task<IActionResult> AcceptCampaign(int campaignId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new {
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN"
                });

            int influencerId = int.Parse(userIdClaim.Value);
            var (success, message, campaign) = await _campaignService.AcceptCampaignAsync(campaignId, influencerId);

            if (!success)
                return StatusCode(400, new {
                    message,
                    code = "CAMPAIGN_ACCEPT_FAILED"
                });

            return Ok(new {
                message,
                campaign,
                code = "CAMPAIGN_ACCEPTED"
            });
        }

        [HttpPost("rejectCampaign/{campaignId}")]
        [Authorize]
        public async Task<IActionResult> RejectCampaign(int campaignId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new {
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN"
                });

            int influencerId = int.Parse(userIdClaim.Value);
            var (success, message) = await _campaignService.RejectCampaignAsync(campaignId, influencerId);

            if (!success)
                return StatusCode(400, new {
                    message,
                    code = "CAMPAIGN_REJECT_FAILED"
                });

            return Ok(new {
                message,
                code = "CAMPAIGN_REJECTED"
            });
        }

        [HttpPost("uploadSignedContract/{campaignId}")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadSignedContract(int campaignId, IFormFile signedContract)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new {
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN"
                });

            int brandId = int.Parse(userIdClaim.Value);
            var (success, message) = await _campaignService.UploadSignedContractAsync(campaignId, brandId, signedContract);

            if (!success)
                return StatusCode(400, new {
                    message,
                    code = "CONTRACT_UPLOAD_FAILED"
                });

            return Ok(new {
                message,
                code = "CONTRACT_UPLOADED"
            });
        }

        [HttpPost("approveSignedContract/{campaignId}")]
        [Authorize]
        public async Task<IActionResult> ApproveSignedContract(int campaignId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new {
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN"
                });

            int influencerId = int.Parse(userIdClaim.Value);
            var (success, message) = await _campaignService.ApproveSignedContractAsync(campaignId, influencerId);

            if (!success)
                return StatusCode(400, new {
                    message,
                    code = "CONTRACT_APPROVAL_FAILED"
                });

            return Ok(new {
                message,
                code = "CONTRACT_APPROVED"
            });
        }

        [HttpPost("rejectSignedContract/{campaignId}")]
        [Authorize]
        public async Task<IActionResult> RejectSignedContract(int campaignId, [FromBody] RejectContractRequest? request = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new {
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN"
                });

            int influencerId = int.Parse(userIdClaim.Value);
            var (success, message) = await _campaignService.RejectSignedContractAsync(campaignId, influencerId, request?.Reason);

            if (!success)
                return StatusCode(400, new {
                    message,
                    code = "CONTRACT_REJECTION_FAILED"
                });

            return Ok(new {
                message,
                code = "CONTRACT_REJECTED"
            });
        }

        [HttpGet("downloadContract/{campaignId}")]
        [Authorize]
        public async Task<IActionResult> DownloadContract(int campaignId)
        {
            var campaign = await _campaignService.GetCampaignById(campaignId);

            if (campaign == null)
                return StatusCode(404, new {
                    message = "Campaign not found",
                    code = "CAMPAIGN_NOT_FOUND"
                });

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new {
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN"
                });

            int userId = int.Parse(userIdClaim.Value);

            // Verify user is either brand or influencer for this campaign
            if (campaign.BrandId != userId && campaign.InfluencerId != userId)
                return StatusCode(403, new {
                    message = "You are not authorized to download this contract",
                    code = "UNAUTHORIZED_ACCESS"
                });

            if (string.IsNullOrEmpty(campaign.GeneratedContractPdfPath))
                return StatusCode(404, new {
                    message = "Contract PDF not found for this campaign",
                    code = "CONTRACT_NOT_GENERATED"
                });

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", campaign.GeneratedContractPdfPath.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
                return StatusCode(404, new {
                    message = "Contract file not found on server",
                    code = "FILE_NOT_FOUND"
                });

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, "application/pdf", $"contract_{campaignId}.pdf");
        }

        [HttpGet("downloadSignedContract/{campaignId}")]
        [Authorize]
        public async Task<IActionResult> DownloadSignedContract(int campaignId)
        {
            var campaign = await _campaignService.GetCampaignById(campaignId);

            if (campaign == null)
                return StatusCode(404, new {
                    message = "Campaign not found",
                    code = "CAMPAIGN_NOT_FOUND"
                });

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new {
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN"
                });

            int userId = int.Parse(userIdClaim.Value);

            // Verify user is either brand or influencer for this campaign
            if (campaign.BrandId != userId && campaign.InfluencerId != userId)
                return StatusCode(403, new {
                    message = "You are not authorized to download this signed contract",
                    code = "UNAUTHORIZED_ACCESS"
                });

            if (string.IsNullOrEmpty(campaign.SignedContractPdfPath))
                return StatusCode(404, new {
                    message = "Signed contract not found for this campaign",
                    code = "SIGNED_CONTRACT_NOT_FOUND"
                });

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", campaign.SignedContractPdfPath.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
                return StatusCode(404, new {
                    message = "Signed contract file not found on server",
                    code = "FILE_NOT_FOUND"
                });

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, "application/pdf", $"signed_contract_{campaignId}.pdf");
        }
    }

    public class RejectContractRequest
    {
        public string? Reason { get; set; }
    }
}
