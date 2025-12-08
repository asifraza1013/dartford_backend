using inflan_api.Models;
using Microsoft.EntityFrameworkCore;

namespace inflan_api.MyDBContext
{
    public class InflanDBContext : DbContext
    {
        public InflanDBContext(DbContextOptions<InflanDBContext> options) : base(options)
        {
        }
        
        public DbSet<User> Users { get; set; }
        public DbSet<Influencer> Influencers { get; set; }
        public DbSet<Plan> Plans { get; set; }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
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

            // Conversation → User (Brand)
            modelBuilder.Entity<Conversation>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.BrandId)
                .OnDelete(DeleteBehavior.Cascade);

            // Conversation → User (Influencer)
            modelBuilder.Entity<Conversation>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.InfluencerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Conversation → Campaign (Optional)
            modelBuilder.Entity<Conversation>()
                .HasOne<Campaign>()
                .WithMany()
                .HasForeignKey(c => c.CampaignId)
                .OnDelete(DeleteBehavior.SetNull);

            // ChatMessage → Conversation
            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // ChatMessage → User (Sender)
            modelBuilder.Entity<ChatMessage>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // ChatMessage → User (Recipient)
            modelBuilder.Entity<ChatMessage>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(m => m.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index for faster conversation lookups
            modelBuilder.Entity<Conversation>()
                .HasIndex(c => new { c.BrandId, c.InfluencerId });

            // Index for faster message lookups
            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => m.ConversationId);
        }

    }
}
