using dartford_api.Models;
using Microsoft.EntityFrameworkCore;

namespace dartford_api.MyDBContext
{
    public class DartfordDBContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Influencer> Influencers { get; set; }
        public DbSet<Plan> Plans { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql("Host=localhost;Database=dartford;Username=postgres;Password=pass123");

    }
}
