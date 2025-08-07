using inflan_api.Models;
using Microsoft.EntityFrameworkCore;

namespace inflan_api.MyDBContext
{
    public class InflanDBContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Influencer> Influencers { get; set; }
        public DbSet<Plan> Plans { get; set; }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        protected override void OnConfiguring (DbContextOptionsBuilder optionsBuilder)
              => optionsBuilder.UseNpgsql("Host=127.0.0.1;Port=5432;Database=your_app_db;Username=your_app_user;Password=YourAppPassword123");
        // protected override void OnConfiguring (DbContextOptionsBuilder optionsBuilder)
        //  => optionsBuilder.UseNpgsql("Host=localhost;Database=inflan;Username=postgres;Password=pass123");
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Campaign → Plan
            modelBuilder.Entity<Campaign>()
                .HasOne<Plan>()
                .WithMany()
                .HasForeignKey(c => c.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            // Campaign → User (Brand)
            modelBuilder.Entity<Campaign>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.BrandId)
                .OnDelete(DeleteBehavior.Cascade);

            // Campaign → User (Influencer)
            modelBuilder.Entity<Campaign>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.InfluencerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Plan → User
            modelBuilder.Entity<Plan>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Influencer → User
            modelBuilder.Entity<Influencer>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Transaction → User
            modelBuilder.Entity<Transaction>()
                .HasOne<User>()                             
                .WithMany()                               
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Transaction → Campaign
            modelBuilder.Entity<Transaction>()
                .HasOne<Campaign>()                 
                .WithMany()
                .HasForeignKey(t => t.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            // Optional: Unique index on TransactionId string
            modelBuilder.Entity<Transaction>()
                .HasIndex(t => t.TransactionId)
                .IsUnique();
        }

    }
}
