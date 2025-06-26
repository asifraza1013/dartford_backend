using dartford_api.Models;
using Microsoft.EntityFrameworkCore;

namespace dartford_api.MyDBContext
{
    public class DartfordDBContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Influencer> Influencers { get; set; } 
        protected override void OnConfiguring (DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=dpg-d1e5gi7diees73bgvp6g-a;Database=dartford; Username=root; Password=dartford");
    }
}
