using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.Utils;

namespace inflan_api.Services;

public class CampaignService : ICampaignService
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly IPlanService _planService;
    public CampaignService(ICampaignRepository campaignRepository , IPlanService planService)
    {
        _campaignRepository = campaignRepository;
        _planService = planService;
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
        campaign.Currency = plan.Currency;
        campaign.Amount = plan.Price * plan.NumberOfMonths;
        campaign.CampaignStartDate = DateOnly.FromDateTime(DateTime.Now);
        campaign.CampaignEndDate = campaign.CampaignStartDate.AddMonths(plan.NumberOfMonths);
        return await _campaignRepository.Create(campaign);
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
        return campaigns.Where(c => c.PaymentStatus == (int)PaymentStatus.COMPLETED).ToList().OrderBy(c => c.CampaignStartDate);

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


}