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
            .Where(c => c.InfluencerId == influencerId)
            .ToListAsync();
    }
    public async Task<IEnumerable<Campaign>> GetCampaignsByBrandId(int brandId)
    {
        return await _context.Campaigns
            .Where(c => c.BrandId == brandId)
            .ToListAsync();
    }

}