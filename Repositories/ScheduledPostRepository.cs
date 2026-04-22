using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using Microsoft.EntityFrameworkCore;

namespace inflan_api.Repositories;

public class ScheduledPostRepository : IScheduledPostRepository
{
    private readonly InflanDBContext _context;

    public ScheduledPostRepository(InflanDBContext context)
    {
        _context = context;
    }

    public async Task<ScheduledPost?> GetByIdAsync(int id)
    {
        return await _context.ScheduledPosts
            .Include(sp => sp.Campaign)
            .FirstOrDefaultAsync(sp => sp.Id == id);
    }

    public async Task<List<ScheduledPost>> GetByInfluencerIdAsync(int influencerId, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.ScheduledPosts
            .Include(sp => sp.Campaign)
            .Where(sp => sp.InfluencerId == influencerId);

        if (from.HasValue)
            query = query.Where(sp => sp.ScheduledAt >= from.Value);

        if (to.HasValue)
            query = query.Where(sp => sp.ScheduledAt < to.Value);

        return await query
            .OrderBy(sp => sp.ScheduledAt)
            .ToListAsync();
    }

    public async Task<ScheduledPost> CreateAsync(ScheduledPost post)
    {
        post.CreatedAt = DateTime.UtcNow;
        _context.ScheduledPosts.Add(post);
        await _context.SaveChangesAsync();
        return post;
    }

    public async Task<ScheduledPost> UpdateAsync(ScheduledPost post)
    {
        post.UpdatedAt = DateTime.UtcNow;
        _context.ScheduledPosts.Update(post);
        await _context.SaveChangesAsync();
        return post;
    }

    public async Task DeleteAsync(int id)
    {
        var post = await _context.ScheduledPosts.FindAsync(id);
        if (post != null)
        {
            _context.ScheduledPosts.Remove(post);
            await _context.SaveChangesAsync();
        }
    }
}
