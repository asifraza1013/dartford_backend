namespace inflan_api.Models;

public class InfluencerBankAccount
{
    public int Id { get; set; }
    public int InfluencerId { get; set; }

    // Bank details (stored for display purposes only)
    public string BankName { get; set; } = "";
    public string BankCode { get; set; } = ""; // Bank code for NGN, Sort code for GBP
    public string AccountNumberLast4 { get; set; } = ""; // Only last 4 digits for security
    public string AccountName { get; set; } = "";

    // Full account number for payouts (encrypted/hashed - only used for TrueLayer open-loop payouts)
    // For Paystack (NGN), we use the recipient code instead
    public string? AccountNumberFull { get; set; }

    // Currency and gateway for this bank account
    public string Currency { get; set; } = "NGN";
    public string PaymentGateway { get; set; } = "paystack"; // "paystack" or "truelayer"

    // Gateway-specific recipient codes (secure references)
    public string? PaystackRecipientCode { get; set; }
    public string? TrueLayerBeneficiaryId { get; set; }

    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public User? Influencer { get; set; }
}
