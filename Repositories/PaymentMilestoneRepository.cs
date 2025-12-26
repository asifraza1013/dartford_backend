using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using inflan_api.Utils;
using Microsoft.EntityFrameworkCore;

namespace inflan_api.Repositories;

public class PaymentMilestoneRepository : IPaymentMilestoneRepository
{
    private readonly InflanDBContext _context;

    public PaymentMilestoneRepository(InflanDBContext context)
    {
        _context = context;
    }

    public async Task<PaymentMilestone?> GetByIdAsync(int id)
    {
        return await _context.PaymentMilestones
            .Include(m => m.Campaign)
            .Include(m => m.Transaction)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<List<PaymentMilestone>> GetByCampaignIdAsync(int campaignId)
    {
        return await _context.PaymentMilestones
            .Where(m => m.CampaignId == campaignId)
            .OrderBy(m => m.MilestoneNumber)
            .ToListAsync();
    }

    public async Task<List<PaymentMilestone>> GetPendingMilestonesAsync(DateTime dueDate)
    {
        return await _context.PaymentMilestones
            .Include(m => m.Campaign)
            .Where(m => m.Status == (int)MilestoneStatus.PENDING && m.DueDate <= dueDate)
            .OrderBy(m => m.DueDate)
            .ToListAsync();
    }

    public async Task<List<PaymentMilestone>> GetOverdueMilestonesAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _context.PaymentMilestones
            .Include(m => m.Campaign)
            .Where(m => m.Status == (int)MilestoneStatus.PENDING && m.DueDate < today)
            .OrderBy(m => m.DueDate)
            .ToListAsync();
    }

    public async Task<List<PaymentMilestone>> GetUpcomingByInfluencerIdAsync(int influencerId)
    {
        // Get all pending/overdue milestones for campaigns where this user is the influencer
        return await _context.PaymentMilestones
            .Include(m => m.Campaign)
                .ThenInclude(c => c.Brand)
            .Where(m => m.Campaign != null &&
                        m.Campaign.InfluencerId == influencerId &&
                        (m.Status == (int)MilestoneStatus.PENDING || m.Status == (int)MilestoneStatus.OVERDUE))
            .OrderBy(m => m.DueDate)
            .ToListAsync();
    }

    public async Task<PaymentMilestone> CreateAsync(PaymentMilestone milestone)
    {
        milestone.CreatedAt = DateTime.UtcNow;
        _context.PaymentMilestones.Add(milestone);
        await _context.SaveChangesAsync();
        return milestone;
    }

    public async Task<List<PaymentMilestone>> CreateBulkAsync(List<PaymentMilestone> milestones)
    {
        foreach (var milestone in milestones)
        {
            milestone.CreatedAt = DateTime.UtcNow;
        }
        _context.PaymentMilestones.AddRange(milestones);
        await _context.SaveChangesAsync();
        return milestones;
    }

    public async Task<PaymentMilestone> UpdateAsync(PaymentMilestone milestone)
    {
        milestone.UpdatedAt = DateTime.UtcNow;
        _context.PaymentMilestones.Update(milestone);
        await _context.SaveChangesAsync();
        return milestone;
    }

    public async Task DeleteByCampaignIdAsync(int campaignId)
    {
        var milestones = await _context.PaymentMilestones
            .Where(m => m.CampaignId == campaignId)
            .ToListAsync();
        _context.PaymentMilestones.RemoveRange(milestones);
        await _context.SaveChangesAsync();
    }
}
