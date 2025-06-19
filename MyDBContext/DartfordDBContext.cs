using dartford_api.Models;
using Microsoft.EntityFrameworkCore;

namespace dartford_api.MyDBContext
{
    public class DartfordDBContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Influencer> Influencers { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql("Host=localhost;Database=dartford_2;Username=postgres;Password=pass123");

    }
}
