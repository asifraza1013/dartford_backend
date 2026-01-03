using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace inflan_api.Models;

public class PaymentMethod
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Gateway { get; set; } = "paystack";

    [Required]
    [MaxLength(255)]
    public string AuthorizationCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? CardType { get; set; }

    [MaxLength(4)]
    public string? Last4 { get; set; }

    [MaxLength(2)]
    public string? ExpiryMonth { get; set; }

    [MaxLength(4)]
    public string? ExpiryYear { get; set; }

    [MaxLength(100)]
    public string? Bank { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    public bool IsDefault { get; set; } = false;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this payment method can be used for recurring/auto payments
    /// Paystack cards with valid AuthorizationCode can be reused
    /// </summary>
    public bool IsReusable { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }

    // Navigation property
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}
