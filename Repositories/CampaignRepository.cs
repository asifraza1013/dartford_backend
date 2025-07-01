using dartford_api.Interfaces;
using dartford_api.Models;
using dartford_api.MyDBContext;
using Microsoft.EntityFrameworkCore;

namespace dartford_api.Repositories;

public class CampaignRepository : ICampaignRepository
{
    private readonly DartfordDBContext _context;

    public CampaignRepository()
    {
        _context = new DartfordDBContext();
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

}