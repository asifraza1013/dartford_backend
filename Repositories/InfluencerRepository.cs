using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using Microsoft.EntityFrameworkCore;

namespace inflan_api.Repositories
{
    public class InfluencerRepository : IInfluencerRepository
    {
        private readonly InflanDBContext _context;

        public InfluencerRepository(InflanDBContext context)
        {
            _context = context;
        }

        private async Task<IEnumerable<InfluencerUserModel>> getExplicitInfluencers()
        {
            return await (from i in _context.Influencers
                join u in _context.Users on i.UserId equals u.Id
                select new InfluencerUserModel
                {
                    Id = i.Id,
                    UserId = i.UserId,
                    YouTube = i.YouTube,
                    Instagram = i.Instagram,
                    Facebook = i.Facebook,
                    TikTok = i.TikTok,
                    YouTubeFollower = i.YouTubeFollower,
                    InstagramFollower = i.InstagramFollower,
                    FacebookFollower = i.FacebookFollower,
                    TikTokFollower = i.TikTokFollower,
                    Bio = i.Bio,

                    // populate navigation property
                    User = new User
                    {
                        Id = u.Id,
                        Name = u.Name,
                        UserName = u.UserName,
                        Email = u.Email,
                        Password = u.Password,
                        BrandName = u.BrandName,
                        BrandCategory = u.BrandCategory,
                        BrandSector = u.BrandSector,
                        Goals = u.Goals,
                        UserType = u.UserType,
                        ProfileImage = u.ProfileImage,
                        Status = u.Status,
                        Currency = u.Currency,
                        Location = u.Location
                    }
                }).ToListAsync();

        }

        private async Task<InfluencerUserModel?> getExplicitInfluencer(int id)
        {
            return await (from i in _context.Influencers
                join u in _context.Users on i.UserId equals u.Id
                where i.Id == id
                select new InfluencerUserModel
                {
                    Id = i.Id,
                    UserId = i.UserId,
                    YouTube = i.YouTube,
                    Instagram = i.Instagram,
                    Facebook = i.Facebook,
                    TikTok = i.TikTok,
                    YouTubeFollower = i.YouTubeFollower,
                    InstagramFollower = i.InstagramFollower,
                    FacebookFollower = i.FacebookFollower,
                    TikTokFollower = i.TikTokFollower,
                    Bio = i.Bio,

                    User = new User
                    {
                        Id = u.Id,
                        Name = u.Name,
                        UserName = u.UserName,
                        Email = u.Email,
                        Password = u.Password,
                        BrandName = u.BrandName,
                        BrandCategory = u.BrandCategory,
                        BrandSector = u.BrandSector,
                        Goals = u.Goals,
                        UserType = u.UserType,
                        ProfileImage = u.ProfileImage,
                        Status = u.Status,
                        Currency = u.Currency,
                        Location = u.Location
                    }
                }).FirstOrDefaultAsync();

        }
        public async Task<IEnumerable<InfluencerUserModel>> GetAll()
        {
            return await getExplicitInfluencers();
        }

        public async Task<InfluencerUserModel?> GetById(int id)
        {
            return await getExplicitInfluencer(id);
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

        public async Task<Influencer?> FindBySocialAccount(string? instagram, string? youtube, string? tiktok, string? facebook)
        {
            // Check if any of the provided social accounts already exist in the database
            // Only check non-empty values
            return await _context.Influencers
                .FirstOrDefaultAsync(i =>
                    (!string.IsNullOrEmpty(instagram) && i.Instagram == instagram) ||
                    (!string.IsNullOrEmpty(youtube) && i.YouTube == youtube) ||
                    (!string.IsNullOrEmpty(tiktok) && i.TikTok == tiktok) ||
                    (!string.IsNullOrEmpty(facebook) && i.Facebook == facebook)
                );
        }

        public async Task<IEnumerable<InfluencerUserModel>> GetByLocation(string location)
        {
            return await (from i in _context.Influencers
                join u in _context.Users on i.UserId equals u.Id
                where u.Location == location
                select new InfluencerUserModel
                {
                    Id = i.Id,
                    UserId = i.UserId,
                    YouTube = i.YouTube,
                    Instagram = i.Instagram,
                    Facebook = i.Facebook,
                    TikTok = i.TikTok,
                    YouTubeFollower = i.YouTubeFollower,
                    InstagramFollower = i.InstagramFollower,
                    FacebookFollower = i.FacebookFollower,
                    TikTokFollower = i.TikTokFollower,
                    Bio = i.Bio,

                    User = new User
                    {
                        Id = u.Id,
                        Name = u.Name,
                        UserName = u.UserName,
                        Email = u.Email,
                        Password = u.Password,
                        BrandName = u.BrandName,
                        BrandCategory = u.BrandCategory,
                        BrandSector = u.BrandSector,
                        Goals = u.Goals,
                        UserType = u.UserType,
                        ProfileImage = u.ProfileImage,
                        Status = u.Status,
                        Currency = u.Currency,
                        Location = u.Location
                    }
                }).ToListAsync();
        }
    }
}
