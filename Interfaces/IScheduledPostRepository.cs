using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface IScheduledPostRepository
{
    Task<ScheduledPost?> GetByIdAsync(int id);
    Task<List<ScheduledPost>> GetByInfluencerIdAsync(int influencerId, DateTime? from = null, DateTime? to = null, string? query = null);
    Task<ScheduledPost> CreateAsync(ScheduledPost post);
    Task<ScheduledPost> UpdateAsync(ScheduledPost post);
    Task DeleteAsync(int id);
}
