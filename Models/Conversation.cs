using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace inflan_api.Models
{
    public class Conversation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BrandId { get; set; }

        [Required]
        public int InfluencerId { get; set; }

        // Optional: Link conversation to a specific campaign
        public int? CampaignId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastMessageAt { get; set; }

        // Soft delete flags for each party
        public bool IsDeletedByBrand { get; set; } = false;
        public bool IsDeletedByInfluencer { get; set; } = false;

        // Navigation properties
        public virtual ICollection<ChatMessage>? Messages { get; set; }
    }
}
