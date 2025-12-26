using inflan_api.Utils;

namespace inflan_api.Models;

public class Withdrawal
{
    public int Id { get; set; }
    public int InfluencerId { get; set; }
    public long AmountInPence { get; set; }
    public string Currency { get; set; } = CurrencyConstants.PrimaryCurrency;
    public int Status { get; set; } = (int)WithdrawalStatus.PENDING;
    public string? PaystackTransferCode { get; set; }
    public string? PaystackRecipientCode { get; set; }
    public string? BankName { get; set; }
    public string? BankCode { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public User? Influencer { get; set; }
}

public enum WithdrawalStatus
{
    PENDING = 1,
    PROCESSING = 2,
    COMPLETED = 3,
    FAILED = 4,
    CANCELLED = 5
}
