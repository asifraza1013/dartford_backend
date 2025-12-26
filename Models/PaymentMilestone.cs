using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace inflan_api.Models;

public class PaymentMilestone
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int CampaignId { get; set; }

    [Required]
    public int MilestoneNumber { get; set; }

    [Required]
    public long AmountInPence { get; set; }

    public long PlatformFeeInPence { get; set; }

    [Required]
    public DateTime DueDate { get; set; }

    public int Status { get; set; } = 1; // MilestoneStatus.PENDING

    public DateTime? PaidAt { get; set; }

    public int? TransactionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey("CampaignId")]
    public virtual Campaign? Campaign { get; set; }

    [ForeignKey("TransactionId")]
    public virtual Transaction? Transaction { get; set; }
}
