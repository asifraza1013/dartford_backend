using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using inflan_api.Utils;

namespace inflan_api.Models;

public class InfluencerPayout
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int CampaignId { get; set; }

    [Required]
    public int InfluencerId { get; set; }

    public int? MilestoneId { get; set; }

    [Required]
    public long GrossAmountInPence { get; set; }

    public long PlatformFeeInPence { get; set; }

    [Required]
    public long NetAmountInPence { get; set; }

    [Required]
    [MaxLength(10)]
    public string Currency { get; set; } = CurrencyConstants.PrimaryCurrency;

    public int Status { get; set; } = 1; // PayoutStatus.PENDING_RELEASE

    public DateTime? ReleasedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    [MaxLength(255)]
    public string? PayoutReference { get; set; }

    [MaxLength(500)]
    public string? FailureMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey("CampaignId")]
    public virtual Campaign? Campaign { get; set; }

    [ForeignKey("InfluencerId")]
    public virtual User? Influencer { get; set; }

    [ForeignKey("MilestoneId")]
    public virtual PaymentMilestone? Milestone { get; set; }
}
