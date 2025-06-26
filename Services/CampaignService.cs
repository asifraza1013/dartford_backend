using dartford_api.Interfaces;
using dartford_api.Models;
using dartford_api.Utils;

namespace dartford_api.Services;

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

    public async Task<Campaign> CreateCampaign(Campaign campaign)
    {
        var plan = await _planService.GetPlanById(campaign.PlanId);
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
    public async Task<IEnumerable<Campaign>> GetCampaignsByInfluencerAndStatus(int influencerId, int campaignStatus)
    {
        var all = await _campaignRepository.GetCampaignsByInfluencerId(influencerId);
        return all.Where(c => c.CampaignStatus == campaignStatus);
    }

}