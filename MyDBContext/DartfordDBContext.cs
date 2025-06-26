using dartford_api.Models;
using Microsoft.EntityFrameworkCore;

namespace dartford_api.MyDBContext
{
    public class DartfordDBContext : DbContext
    {
        public DartfordDBContext(DbContextOptions<DartfordDBContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Influencer> Influencers { get; set; }
        public DbSet<Plan> Plans { get; set; }
    }
}
