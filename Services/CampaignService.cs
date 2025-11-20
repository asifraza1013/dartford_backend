using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.Utils;

namespace inflan_api.Services;

public class CampaignService : ICampaignService
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly IPlanService _planService;
    private readonly IUserService _userService;
    private readonly IPdfGenerationService _pdfGenerationService;
    private readonly IEmailService _emailService;
    private readonly IInfluencerService _influencerService;

    public CampaignService(
        ICampaignRepository campaignRepository,
        IPlanService planService,
        IUserService userService,
        IPdfGenerationService pdfGenerationService,
        IEmailService emailService,
        IInfluencerService influencerService)
    {
        _campaignRepository = campaignRepository;
        _planService = planService;
        _userService = userService;
        _pdfGenerationService = pdfGenerationService;
        _emailService = emailService;
        _influencerService = influencerService;
    }

    public async Task<IEnumerable<Campaign>> GetAllCampaigns()
    {
        return await _campaignRepository.GetAll();
    }

    public async Task<Campaign?> GetCampaignById(int id)
    {
        return await _campaignRepository.GetById(id);
    }

    public async Task<Campaign?> CreateCampaign(Campaign campaign)
    {
        var plan = await _planService.GetPlanById(campaign.PlanId);
        if (plan == null)
            return null;

        // Validate plan has a valid price
        if (plan.Price <= 0)
        {
            Console.WriteLine($"Warning: Plan {plan.Id} has invalid price: {plan.Price}");
            return null;
        }

        campaign.Currency = plan.Currency;
        campaign.Amount = plan.Price;

        // Set initial payment status to PENDING
        campaign.PaymentStatus = (int)PaymentStatus.PENDING;

        // Dates should come from the request (Screen 2 of the booking flow)
        // Brand specifies start and end dates when creating the campaign
        var createdCampaign = await _campaignRepository.Create(campaign);

        // Send email notification to influencer
        try
        {
            var influencer = await _userService.GetUserById(campaign.InfluencerId);
            var brand = await _userService.GetUserById(campaign.BrandId);

            if (influencer != null && brand != null && !string.IsNullOrEmpty(influencer.Email))
            {
                await _emailService.SendNewCampaignNotificationAsync(
                    influencer.Email,
                    influencer.Name ?? "Influencer",
                    createdCampaign.Id,
                    campaign.ProjectName ?? "New Campaign",
                    brand.Name ?? "Brand"
                );
            }
        }
        catch (Exception ex)
        {
            // Log email error but don't fail campaign creation
            Console.WriteLine($"Failed to send campaign notification email: {ex.Message}");
        }

        return createdCampaign;
    }

    public async Task<bool> UpdateCampaign(int id, Campaign campaign)
    {
        var existing = await _campaignRepository.GetById(id);
        if (existing == null) return false;

        existing.CampaignStatus = campaign.CampaignStatus != 1 ? campaign.CampaignStatus : existing.CampaignStatus;
        existing.PaymentStatus = campaign.PaymentStatus != 1 ? campaign.PaymentStatus : existing.PaymentStatus;

        if (campaign.InstructionDocuments != null && campaign.InstructionDocuments.Any())
        {
            existing.InstructionDocuments =  campaign.InstructionDocuments;
        }

        await _campaignRepository.Update(existing);
        return true;
    }

    public async Task<bool> DeleteCampaign(int id)
    {
        var existing = await _campaignRepository.GetById(id);
        if (existing == null) return false;

        await _campaignRepository.Delete(id);
        return true;
    }
    public async Task<IEnumerable<Campaign>> GetCampaignsByInfluencerId(int influencerId)
    {
        var campaigns = await _campaignRepository.GetCampaignsByInfluencerId(influencerId);
        // Return all campaigns for influencer (including DRAFT for pending requests)
        // Frontend can filter by status if needed
        return campaigns.OrderBy(c => c.CampaignStartDate);
    }
    public async Task<IEnumerable<Campaign>> GetCampaignsByBrandId(int brandId)
    {
        var campaigns = await _campaignRepository.GetCampaignsByBrandId(brandId);
        return campaigns.OrderBy(c => c.CampaignStartDate);

    }
    public async Task<IEnumerable<Campaign>> GetCompletedPaymentCampaignsByBrandId(int brandId)
    {
        var campaigns = await _campaignRepository.GetCampaignsByBrandId(brandId);
        return campaigns.Where(c => c.PaymentStatus == (int)PaymentStatus.COMPLETED).ToList().OrderBy(c => c.CampaignStartDate);

    }
    
    public async Task<IEnumerable<Campaign>> GetCampaignsByInfluencerAndStatus(int influencerId, int campaignStatus)
    {
        var all = await _campaignRepository.GetCampaignsByInfluencerId(influencerId);
        return all.Where(c => c.CampaignStatus == campaignStatus);
    }
    
    public async Task<List<string>> SaveCampaignDocumentsAsync(List<IFormFile> files)
    {
        var savedPaths = new List<string>();

        if (files == null || !files.Any())
            return savedPaths;

        var campaignFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "campaignDocs");
        if (!Directory.Exists(campaignFolder))
            Directory.CreateDirectory(campaignFolder);

        var allowedExtensions = new[] { ".rtf", ".doc", ".docx", ".txt", ".pdf" };

        foreach (var file in files)
        {
            if (file == null || file.Length == 0)
                continue;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                continue;

            var uniqueFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(campaignFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"/campaignDocs/{uniqueFileName}";
            savedPaths.Add(relativePath);
        }

        return savedPaths;
    }

    public async Task<List<string>> SaveContentFilesAsync(List<IFormFile> files)
    {
        var savedPaths = new List<string>();

        if (files == null || !files.Any())
            return savedPaths;

        var contentFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "contentFiles");
        if (!Directory.Exists(contentFolder))
            Directory.CreateDirectory(contentFolder);

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".svg", ".gif" };
        const long maxFileSize = 5 * 1024 * 1024; // 5MB in bytes

        foreach (var file in files)
        {
            if (file == null || file.Length == 0)
                continue;

            // Check file size (5MB limit)
            if (file.Length > maxFileSize)
                continue;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                continue;

            var uniqueFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(contentFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"/contentFiles/{uniqueFileName}";
            savedPaths.Add(relativePath);
        }

        return savedPaths;
    }

    public async Task<bool> DeleteCampaignDocumentsAsync(List<string> filePaths)
    {
        if (filePaths == null || filePaths.Count == 0)
            return false;

        bool allDeleted = true;

        foreach (var relativePath in filePaths)
        {
            try
            {
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.TrimStart('/'));

                if (File.Exists(fullPath))
                    File.Delete(fullPath);
                else
                    allDeleted = false;
            }
            catch
            {
                allDeleted = false;
            }
        }

        return allDeleted;
    }

    // New Booking Workflow Methods

    public async Task<(bool Success, string Message, Campaign? Campaign)> AcceptCampaignAsync(int campaignId, int influencerId)
    {
        var campaign = await _campaignRepository.GetById(campaignId);

        if (campaign == null)
            return (false, "Campaign not found", null);

        if (campaign.InfluencerId != influencerId)
            return (false, "You are not authorized to accept this campaign", null);

        if (campaign.CampaignStatus != (int)CampaignStatus.DRAFT)
            return (false, "Campaign is not in a state that can be accepted", null);

        // Get related data for PDF generation
        var brand = await _userService.GetUserById(campaign.BrandId);
        var influencer = await _userService.GetUserById(campaign.InfluencerId);
        var influencerProfile = await _influencerService.GetInfluencerByUserId(campaign.InfluencerId);
        var plan = await _planService.GetPlanById(campaign.PlanId);

        if (brand == null || influencer == null || plan == null)
            return (false, "Required data not found for contract generation", null);

        // Update campaign status to awaiting contract signature
        campaign.CampaignStatus = (int)CampaignStatus.AWAITING_CONTRACT_SIGNATURE;
        campaign.InfluencerAcceptedAt = DateTime.UtcNow;

        // Generate contract PDF
        try
        {
            var contractPath = await _pdfGenerationService.GenerateContractPdfAsync(campaign, brand, influencer, influencerProfile, plan);
            campaign.GeneratedContractPdfPath = contractPath;

            await _campaignRepository.Update(campaign);

            // Send email notification to brand with PDF attachment
            // Get the full file system path for the attachment
            var fullPdfPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", contractPath.TrimStart('/'));
            await _emailService.SendInfluencerResponseNotificationAsync(
                brand.Email ?? "",
                brand.Name ?? "",
                campaign.Id,
                campaign.ProjectName,
                true,
                fullPdfPath
            );

            return (true, "Campaign accepted successfully. Contract generated and sent to brand.", campaign);
        }
        catch (Exception ex)
        {
            return (false, $"Error generating contract: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string Message)> RejectCampaignAsync(int campaignId, int influencerId)
    {
        var campaign = await _campaignRepository.GetById(campaignId);

        if (campaign == null)
            return (false, "Campaign not found");

        if (campaign.InfluencerId != influencerId)
            return (false, "You are not authorized to reject this campaign");

        if (campaign.CampaignStatus != (int)CampaignStatus.DRAFT)
            return (false, "Campaign is not in a state that can be rejected");

        // Update campaign status to rejected
        campaign.CampaignStatus = (int)CampaignStatus.REJECTED;
        await _campaignRepository.Update(campaign);

        // Send notification to brand
        var brand = await _userService.GetUserById(campaign.BrandId);
        if (brand != null)
        {
            await _emailService.SendInfluencerResponseNotificationAsync(
                brand.Email ?? "",
                brand.Name ?? "",
                campaign.Id,
                campaign.ProjectName,
                false
            );
        }

        return (true, "Campaign rejected successfully");
    }

    public async Task<(bool Success, string Message)> UploadSignedContractAsync(int campaignId, int brandId, IFormFile signedContract)
    {
        var campaign = await _campaignRepository.GetById(campaignId);

        if (campaign == null)
            return (false, "Campaign not found");

        if (campaign.BrandId != brandId)
            return (false, "You are not authorized to upload contract for this campaign");

        if (campaign.CampaignStatus != (int)CampaignStatus.AWAITING_CONTRACT_SIGNATURE)
            return (false, "Campaign is not awaiting contract signature");

        // Validate file
        if (signedContract == null || signedContract.Length == 0)
            return (false, "No file provided");

        var allowedExtensions = new[] { ".pdf" };
        var extension = Path.GetExtension(signedContract.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return (false, "Only PDF files are allowed for signed contracts");

        // Save signed contract
        try
        {
            var contractsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "contracts", "signed");
            if (!Directory.Exists(contractsFolder))
                Directory.CreateDirectory(contractsFolder);

            var fileName = $"signed_contract_{campaign.Id}_{Guid.NewGuid()}.pdf";
            var filePath = Path.Combine(contractsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await signedContract.CopyToAsync(stream);
            }

            var relativePath = $"/contracts/signed/{fileName}";
            campaign.SignedContractPdfPath = relativePath;
            campaign.ContractSignedAt = DateTime.UtcNow;
            campaign.CampaignStatus = (int)CampaignStatus.AWAITING_PAYMENT;

            await _campaignRepository.Update(campaign);

            // Send payment notification email to brand
            var brand = await _userService.GetUserById(campaign.BrandId);
            if (brand != null)
            {
                await _emailService.SendPaymentRequestAsync(
                    brand.Email ?? "",
                    brand.Name ?? "",
                    campaign.Id,
                    (decimal)campaign.Amount,
                    campaign.Currency ?? "USD"
                );
            }

            return (true, "Signed contract uploaded successfully. Payment notification sent.");
        }
        catch (Exception ex)
        {
            return (false, $"Error uploading signed contract: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> ActivateCampaignAfterPaymentAsync(int campaignId)
    {
        var campaign = await _campaignRepository.GetById(campaignId);

        if (campaign == null)
            return (false, "Campaign not found");

        if (campaign.CampaignStatus != (int)CampaignStatus.AWAITING_PAYMENT)
            return (false, "Campaign is not awaiting payment");

        if (campaign.PaymentStatus != (int)PaymentStatus.COMPLETED)
            return (false, "Payment has not been completed for this campaign");

        // Activate the campaign
        campaign.CampaignStatus = (int)CampaignStatus.ACTIVE;
        campaign.PaymentCompletedAt = DateTime.UtcNow;

        await _campaignRepository.Update(campaign);

        // Send activation notification to influencer
        var influencer = await _userService.GetUserById(campaign.InfluencerId);
        if (influencer != null)
        {
            await _emailService.SendCampaignActivatedAsync(
                influencer.Email ?? "",
                influencer.Name ?? "",
                campaign.Id,
                campaign.ProjectName
            );
        }

        return (true, "Campaign activated successfully");
    }
}