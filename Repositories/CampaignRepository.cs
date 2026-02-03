using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using Microsoft.EntityFrameworkCore;

namespace inflan_api.Repositories;

public class CampaignRepository : ICampaignRepository
{
    private readonly InflanDBContext _context;

    public CampaignRepository(InflanDBContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Campaign>> GetAll()
    {
        return await _context.Campaigns.ToListAsync();
    }

    public async Task<Campaign?> GetById(int id)
    {
        return await _context.Campaigns.FindAsync(id);
    }

    public async Task<Campaign> Create(Campaign campaign)
    {
        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();
        return campaign;
    }

    public async Task Update(Campaign campaign)
    {
        _context.Campaigns.Update(campaign);
        await _context.SaveChangesAsync();
    }

    public async Task Delete(int id)
    {
        var campaign = await _context.Campaigns.FindAsync(id);
        if (campaign != null)
        {
            _context.Campaigns.Remove(campaign);
            await _context.SaveChangesAsync();
        }
    }
    public async Task<IEnumerable<Campaign>> GetCampaignsByInfluencerId(int influencerId)
    {
        return await _context.Campaigns
            .Include(c => c.Brand)
            .Where(c => c.InfluencerId == influencerId)
            .ToListAsync();
    }
    public async Task<IEnumerable<Campaign>> GetCampaignsByBrandId(int brandId)
    {
        return await _context.Campaigns
            .Include(c => c.Brand)
            .Include(c => c.Influencer)
            .Where(c => c.BrandId == brandId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Campaign>> GetAllWithAutoPay()
    {
        // Get campaigns that have milestones payment type and auto-pay enabled
        // CampaignStatus: DRAFT=1, REJECTED=2, AWAITING_CONTRACT_SIGNATURE=3, AWAITING_SIGNATURE_APPROVAL=4, AWAITING_PAYMENT=5, ACTIVE=6, COMPLETED=7, CANCELLED=8
        return await _context.Campaigns
            .Include(c => c.Brand)
            .Where(c => c.IsRecurringEnabled == true &&
                       c.PaymentType == 2 && // Milestones payment type (2 = MILESTONE)
                       c.CampaignStatus == 6) // ACTIVE status
            .ToListAsync();
    }
}