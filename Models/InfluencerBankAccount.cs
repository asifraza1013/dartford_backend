namespace inflan_api.Models;

public class InfluencerBankAccount
{
    public int Id { get; set; }
    public int InfluencerId { get; set; }

    // Bank details (stored for display purposes only)
    public string BankName { get; set; } = "";
    public string BankCode { get; set; } = "";
    public string AccountNumberLast4 { get; set; } = ""; // Only last 4 digits for security
    public string AccountName { get; set; } = "";

    // Paystack recipient code (the actual secure reference)
    public string PaystackRecipientCode { get; set; } = "";

    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public User? Influencer { get; set; }
}
