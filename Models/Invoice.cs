using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using inflan_api.Utils;

namespace inflan_api.Models;

public class Invoice
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required]
    public int CampaignId { get; set; }

    [Required]
    public int BrandId { get; set; }

    [Required]
    public int InfluencerId { get; set; }

    public int? MilestoneId { get; set; }

    public int? TransactionId { get; set; }

    [Required]
    public long SubtotalInPence { get; set; }

    public long PlatformFeeInPence { get; set; }

    [Required]
    public long TotalAmountInPence { get; set; }

    [Required]
    [MaxLength(10)]
    public string Currency { get; set; } = CurrencyConstants.PrimaryCurrency;

    public int Status { get; set; } = 1; // InvoiceStatus.DRAFT

    [MaxLength(500)]
    public string? PdfPath { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CampaignId")]
    public virtual Campaign? Campaign { get; set; }

    [ForeignKey("BrandId")]
    public virtual User? Brand { get; set; }

    [ForeignKey("InfluencerId")]
    public virtual User? Influencer { get; set; }

    [ForeignKey("MilestoneId")]
    public virtual PaymentMilestone? Milestone { get; set; }

    [ForeignKey("TransactionId")]
    public virtual Transaction? Transaction { get; set; }
}
