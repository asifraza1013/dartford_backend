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

    public async Task<List<ScheduledPost>> GetByInfluencerIdAsync(int influencerId, DateTime? from = null, DateTime? to = null, string? query = null)
    {
        var posts = _context.ScheduledPosts
            .Include(sp => sp.Campaign)
            .Where(sp => sp.InfluencerId == influencerId);

        if (from.HasValue)
            posts = posts.Where(sp => sp.ScheduledAt >= from.Value);

        if (to.HasValue)
            posts = posts.Where(sp => sp.ScheduledAt < to.Value);

        if (!string.IsNullOrWhiteSpace(query))
        {
            // Case-insensitive ILIKE against the post's title / description and the
            // joined campaign's project name. EF Core translates EF.Functions.ILike
            // to PostgreSQL's native ILIKE so the filter runs at the DB layer.
            var pattern = $"%{query.Trim()}%";
            posts = posts.Where(sp =>
                EF.Functions.ILike(sp.Title, pattern) ||
                (sp.Description != null && EF.Functions.ILike(sp.Description, pattern)) ||
                (sp.Campaign != null && sp.Campaign.ProjectName != null &&
                    EF.Functions.ILike(sp.Campaign.ProjectName, pattern)));
        }

        return await posts
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
