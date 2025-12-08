using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace inflan_api.Models
{
    public static class NotificationType
    {
        public const int Message = 1;           // New chat message
        public const int CampaignUpdate = 2;    // Campaign status change
        public const int CampaignInvite = 3;    // Invited to campaign
        public const int CampaignApplication = 4; // Someone applied to campaign
        public const int Payment = 5;           // Payment related
        public const int System = 6;            // System notifications
    }

    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        public int Type { get; set; } // NotificationType

        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Message { get; set; }

        // Reference to related entity (e.g., conversationId, campaignId)
        public int? ReferenceId { get; set; }

        // Type of reference (e.g., "conversation", "campaign", "payment")
        public string? ReferenceType { get; set; }

        // Additional data as JSON (e.g., sender info, campaign details)
        public string? Data { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime? ReadAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
