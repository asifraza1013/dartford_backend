using System.ComponentModel.DataAnnotations;
using System.Transactions;

namespace inflan_api.Models;

public class Transaction
{
    [Key]
    public int Id { get; set; }
    public int UserId { get; set; }
    public float Amount { get; set; }
    public string Currency { get; set; }
    public int TransactionStatus { get; set; }
    public string TransactionId { get; set; }
    public int CampaignId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? FailureMessage { get; set; }
}