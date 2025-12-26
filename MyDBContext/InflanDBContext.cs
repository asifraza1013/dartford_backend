using inflan_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace inflan_api.MyDBContext
{
    public class InflanDBContext : DbContext
    {
        public InflanDBContext(DbContextOptions<InflanDBContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            // Suppress the pending model changes warning during migrations
            optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        }
        
        public DbSet<User> Users { get; set; }
        public DbSet<Influencer> Influencers { get; set; }
        public DbSet<Plan> Plans { get; set; }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        // Payment Module
        public DbSet<PaymentMilestone> PaymentMilestones { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InfluencerPayout> InfluencerPayouts { get; set; }
        public DbSet<PlatformSettings> PlatformSettings { get; set; }
        public DbSet<Withdrawal> Withdrawals { get; set; }
        public DbSet<InfluencerBankAccount> InfluencerBankAccounts { get; set; }
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
                .HasOne(c => c.Brand)
                .WithMany()
                .HasForeignKey(c => c.BrandId)
                .OnDelete(DeleteBehavior.Cascade);

            // Campaign → User (Influencer)
            modelBuilder.Entity<Campaign>()
                .HasOne(c => c.Influencer)
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
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Transaction → Campaign
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Campaign)
                .WithMany()
                .HasForeignKey(t => t.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            // Optional: Unique index on TransactionId string (legacy)
            modelBuilder.Entity<Transaction>()
                .HasIndex(t => t.TransactionId)
                .IsUnique();

            // Unique index on TransactionReference
            modelBuilder.Entity<Transaction>()
                .HasIndex(t => t.TransactionReference)
                .IsUnique();

            // Transaction → PaymentMilestone
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Milestone)
                .WithMany()
                .HasForeignKey(t => t.MilestoneId)
                .OnDelete(DeleteBehavior.SetNull);

            // Transaction → PaymentMethod
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.PaymentMethod)
                .WithMany()
                .HasForeignKey(t => t.PaymentMethodId)
                .OnDelete(DeleteBehavior.SetNull);

            // PaymentMilestone → Campaign
            modelBuilder.Entity<PaymentMilestone>()
                .HasOne(m => m.Campaign)
                .WithMany()
                .HasForeignKey(m => m.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            // PaymentMilestone → Transaction
            modelBuilder.Entity<PaymentMilestone>()
                .HasOne(m => m.Transaction)
                .WithMany()
                .HasForeignKey(m => m.TransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for milestone lookups by campaign
            modelBuilder.Entity<PaymentMilestone>()
                .HasIndex(m => m.CampaignId);

            // PaymentMethod → User
            modelBuilder.Entity<PaymentMethod>()
                .HasOne(pm => pm.User)
                .WithMany()
                .HasForeignKey(pm => pm.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for payment method lookups by user
            modelBuilder.Entity<PaymentMethod>()
                .HasIndex(pm => pm.UserId);

            // Invoice → Campaign
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Campaign)
                .WithMany()
                .HasForeignKey(i => i.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            // Invoice → Brand
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Brand)
                .WithMany()
                .HasForeignKey(i => i.BrandId)
                .OnDelete(DeleteBehavior.Restrict);

            // Invoice → Influencer
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Influencer)
                .WithMany()
                .HasForeignKey(i => i.InfluencerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Invoice → PaymentMilestone
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Milestone)
                .WithMany()
                .HasForeignKey(i => i.MilestoneId)
                .OnDelete(DeleteBehavior.SetNull);

            // Invoice → Transaction
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Transaction)
                .WithMany()
                .HasForeignKey(i => i.TransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Unique index on invoice number
            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.InvoiceNumber)
                .IsUnique();

            // Index for invoice lookups
            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.CampaignId);

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.BrandId);

            // InfluencerPayout → Campaign
            modelBuilder.Entity<InfluencerPayout>()
                .HasOne(p => p.Campaign)
                .WithMany()
                .HasForeignKey(p => p.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            // InfluencerPayout → Influencer
            modelBuilder.Entity<InfluencerPayout>()
                .HasOne(p => p.Influencer)
                .WithMany()
                .HasForeignKey(p => p.InfluencerId)
                .OnDelete(DeleteBehavior.Restrict);

            // InfluencerPayout → PaymentMilestone
            modelBuilder.Entity<InfluencerPayout>()
                .HasOne(p => p.Milestone)
                .WithMany()
                .HasForeignKey(p => p.MilestoneId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for payout lookups
            modelBuilder.Entity<InfluencerPayout>()
                .HasIndex(p => p.InfluencerId);

            modelBuilder.Entity<InfluencerPayout>()
                .HasIndex(p => p.CampaignId);

            // PlatformSettings unique key
            modelBuilder.Entity<PlatformSettings>()
                .HasIndex(s => s.SettingKey)
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

            // Notification → User
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for faster notification lookups
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.UserId, n.IsRead });

            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.CreatedAt);

            // Withdrawal → User (Influencer)
            modelBuilder.Entity<Withdrawal>()
                .HasOne(w => w.Influencer)
                .WithMany()
                .HasForeignKey(w => w.InfluencerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for faster withdrawal lookups
            modelBuilder.Entity<Withdrawal>()
                .HasIndex(w => w.InfluencerId);

            modelBuilder.Entity<Withdrawal>()
                .HasIndex(w => w.Status);

            // InfluencerBankAccount → User (Influencer)
            modelBuilder.Entity<InfluencerBankAccount>()
                .HasOne(a => a.Influencer)
                .WithMany()
                .HasForeignKey(a => a.InfluencerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for faster bank account lookups
            modelBuilder.Entity<InfluencerBankAccount>()
                .HasIndex(a => a.InfluencerId);

            // Unique index on Paystack recipient code
            modelBuilder.Entity<InfluencerBankAccount>()
                .HasIndex(a => a.PaystackRecipientCode)
                .IsUnique();
        }

    }
}
