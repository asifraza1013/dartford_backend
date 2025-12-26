using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using inflan_api.Utils;

namespace inflan_api.Models;

public class Transaction
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string TransactionReference { get; set; } = string.Empty;

    [Required]
    public int UserId { get; set; }

    [Required]
    public int CampaignId { get; set; }

    public int? MilestoneId { get; set; }

    // Amounts in pence (minor units) for precision
    [Required]
    public long AmountInPence { get; set; }

    public long PlatformFeeInPence { get; set; }

    [Required]
    public long TotalAmountInPence { get; set; }

    [Required]
    [MaxLength(10)]
    public string Currency { get; set; } = CurrencyConstants.PrimaryCurrency;

    [Required]
    [MaxLength(50)]
    public string Gateway { get; set; } = string.Empty; // "truelayer" or "paystack"

    public int TransactionStatus { get; set; } = 1; // PaymentStatus enum

    [MaxLength(255)]
    public string? GatewayTransactionId { get; set; }

    [MaxLength(255)]
    public string? GatewayPaymentId { get; set; }

    public int? PaymentMethodId { get; set; } // FK if recurring payment used

    [MaxLength(500)]
    public string? FailureMessage { get; set; }

    [MaxLength(100)]
    public string? FailureCode { get; set; }

    public string? WebhookPayload { get; set; } // Store webhook data for debugging

    [MaxLength(500)]
    public string? RedirectUrl { get; set; } // Gateway redirect URL

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // Legacy fields for backward compatibility
    [Obsolete("Use AmountInPence instead")]
    public float Amount { get; set; }

    [Obsolete("Use TransactionReference instead")]
    [MaxLength(100)]
    public string? TransactionId { get; set; }

    [Obsolete("Use GatewayPaymentId instead")]
    [MaxLength(255)]
    public string? StripePaymentIntentId { get; set; }

    // Navigation properties
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    [ForeignKey("CampaignId")]
    public virtual Campaign? Campaign { get; set; }

    [ForeignKey("MilestoneId")]
    public virtual PaymentMilestone? Milestone { get; set; }

    [ForeignKey("PaymentMethodId")]
    public virtual PaymentMethod? PaymentMethod { get; set; }
}
