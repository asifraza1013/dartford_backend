using inflan_api.DTOs;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using inflan_api.Utils;
using Microsoft.EntityFrameworkCore;

namespace inflan_api.Repositories;

public class InfluencerPayoutRepository : IInfluencerPayoutRepository
{
    private readonly InflanDBContext _context;

    public InfluencerPayoutRepository(InflanDBContext context)
    {
        _context = context;
    }

    public async Task<InfluencerPayout?> GetByIdAsync(int id)
    {
        return await _context.InfluencerPayouts
            .Include(p => p.Campaign)
            .Include(p => p.Influencer)
            .Include(p => p.Milestone)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<InfluencerPayout>> GetByCampaignIdAsync(int campaignId)
    {
        return await _context.InfluencerPayouts
            .Include(p => p.Milestone)
            .Where(p => p.CampaignId == campaignId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<InfluencerPayout>> GetByInfluencerIdAsync(int influencerId, int page = 1, int pageSize = 20)
    {
        return await _context.InfluencerPayouts
            .Include(p => p.Campaign)
            .Include(p => p.Milestone)
            .Where(p => p.InfluencerId == influencerId)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<InfluencerPayout>> GetPendingByInfluencerIdAsync(int influencerId)
    {
        // Only show payouts that are PENDING_RELEASE (awaiting brand to release)
        // RELEASED payouts are available for withdrawal, not "pending"
        return await _context.InfluencerPayouts
            .Include(p => p.Campaign)
            .Include(p => p.Milestone)
            .Where(p => p.InfluencerId == influencerId &&
                        p.Status == (int)PayoutStatus.PENDING_RELEASE)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<(List<InfluencerPayout> Items, int TotalCount)> GetByInfluencerIdFilteredAsync(int influencerId, PaymentFilterDto filter)
    {
        var query = _context.InfluencerPayouts
            .Include(p => p.Campaign)
            .Include(p => p.Milestone)
            .Where(p => p.InfluencerId == influencerId);

        // Apply filters
        if (filter.DateFrom.HasValue)
            query = query.Where(p => p.CreatedAt >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(p => p.CreatedAt <= filter.DateTo.Value);

        if (filter.MinAmount.HasValue)
            query = query.Where(p => p.NetAmountInPence >= filter.MinAmount.Value);

        if (filter.MaxAmount.HasValue)
            query = query.Where(p => p.NetAmountInPence <= filter.MaxAmount.Value);

        if (filter.CampaignId.HasValue)
            query = query.Where(p => p.CampaignId == filter.CampaignId.Value);

        if (filter.Status.HasValue)
            query = query.Where(p => p.Status == filter.Status.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<InfluencerPayout> CreateAsync(InfluencerPayout payout)
    {
        payout.CreatedAt = DateTime.UtcNow;
        _context.InfluencerPayouts.Add(payout);
        await _context.SaveChangesAsync();
        return payout;
    }

    public async Task<InfluencerPayout> UpdateAsync(InfluencerPayout payout)
    {
        payout.UpdatedAt = DateTime.UtcNow;
        _context.InfluencerPayouts.Update(payout);
        await _context.SaveChangesAsync();
        return payout;
    }

    public async Task<long> GetTotalPendingByInfluencerIdAsync(int influencerId)
    {
        // Only count payouts that are PENDING_RELEASE (awaiting brand release)
        return await _context.InfluencerPayouts
            .Where(p => p.InfluencerId == influencerId &&
                        p.Status == (int)PayoutStatus.PENDING_RELEASE)
            .SumAsync(p => p.NetAmountInPence);
    }

    public async Task<long> GetTotalReleasedByInfluencerIdAsync(int influencerId)
    {
        return await _context.InfluencerPayouts
            .Where(p => p.InfluencerId == influencerId && p.Status == (int)PayoutStatus.RELEASED)
            .SumAsync(p => p.NetAmountInPence);
    }

    public async Task<long> GetTotalReleasedByInfluencerIdAsync(int influencerId, string currency)
    {
        return await _context.InfluencerPayouts
            .Where(p => p.InfluencerId == influencerId &&
                        p.Status == (int)PayoutStatus.RELEASED &&
                        p.Currency == currency)
            .SumAsync(p => p.NetAmountInPence);
    }
}
