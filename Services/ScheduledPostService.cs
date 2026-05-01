using inflan_api.Interfaces;
using inflan_api.Models;

namespace inflan_api.Services;

public class ScheduledPostService : IScheduledPostService
{
    private readonly IScheduledPostRepository _repo;

    public ScheduledPostService(IScheduledPostRepository repo)
    {
        _repo = repo;
    }

    public Task<ScheduledPost?> GetByIdAsync(int id) => _repo.GetByIdAsync(id);

    public Task<List<ScheduledPost>> GetByInfluencerIdAsync(int influencerId, DateTime? from = null, DateTime? to = null, string? query = null)
        => _repo.GetByInfluencerIdAsync(influencerId, from, to, query);

    public Task<ScheduledPost> CreateAsync(ScheduledPost post) => _repo.CreateAsync(post);

    public Task<ScheduledPost> UpdateAsync(ScheduledPost post) => _repo.UpdateAsync(post);

    public Task DeleteAsync(int id) => _repo.DeleteAsync(id);
}
