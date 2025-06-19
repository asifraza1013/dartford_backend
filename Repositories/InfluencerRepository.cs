using dartford_api.Interfaces;
using dartford_api.Models;
using dartford_api.MyDBContext;
using Microsoft.EntityFrameworkCore;

namespace dartford_api.Repositories
{
    public class InfluencerRepository : IInfluencerRepository
    {
        private readonly DartfordDBContext _context;

        public InfluencerRepository()
        {
            _context = new DartfordDBContext();
        }

        public async Task<IEnumerable<Influencer>> GetAll()
        {
            return await _context.Influencers.ToListAsync();
        }

        public async Task<Influencer?> GetById(int id)
        {
            return await _context.Influencers.FindAsync(id);
        }

        public async Task<Influencer> Create(Influencer influencer)
        {
            _context.Influencers.Add(influencer);
            await _context.SaveChangesAsync();
            return influencer;
        }

        public async Task Update(Influencer influencer)
        {
            _context.Influencers.Update(influencer);
            await _context.SaveChangesAsync();
        }

        public async Task Delete(int id)
        {
            var influencer = await _context.Influencers.FindAsync(id);
            if (influencer != null)
            {
                _context.Influencers.Remove(influencer);
                await _context.SaveChangesAsync();
            }
        }
        public async Task<Influencer?> GetByUserId(int userId)
        {
            return await _context.Influencers
                .FirstOrDefaultAsync(i => i.UserId == userId);
        }

    }
}
