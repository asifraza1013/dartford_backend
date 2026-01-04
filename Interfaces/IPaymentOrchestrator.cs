using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface IPaymentOrchestrator
{
    /// <summary>
    /// Initiate a payment for a campaign (one-time or milestone)
    /// </summary>
    Task<PaymentInitiationResponse> InitiatePaymentAsync(InitiatePaymentRequest request);

    /// <summary>
    /// Process webhook from payment gateway
    /// </summary>
    Task<bool> ProcessWebhookAsync(string gateway, string payload, string? signature);

    /// <summary>
    /// Charge saved card for recurring payment (Paystack only)
    /// </summary>
    Task<PaymentInitiationResponse> ChargeRecurringPaymentAsync(int milestoneId, int paymentMethodId);

    /// <summary>
    /// Get payment status
    /// </summary>
    Task<Transaction?> GetPaymentStatusAsync(string transactionReference);

    /// <summary>
    /// Release payment to influencer
    /// </summary>
    Task<InfluencerPayout> ReleasePaymentToInfluencerAsync(int payoutId, int brandUserId);

    /// <summary>
    /// Get campaign payment summary (balance)
    /// </summary>
    Task<CampaignPaymentSummary> GetCampaignPaymentSummaryAsync(int campaignId);

    /// <summary>
    /// Get brand's overdue balance (only overdue milestones)
    /// </summary>
    Task<long> GetBrandOutstandingBalanceAsync(int brandId);

    /// <summary>
    /// Get brand's detailed outstanding balance info
    /// </summary>
    Task<BrandOutstandingBalanceDto> GetBrandOutstandingBalanceDetailedAsync(int brandId);

    /// <summary>
    /// Verify payment with gateway and process if successful (for local dev without webhooks)
    /// </summary>
    Task<PaymentVerificationResult> VerifyAndProcessPaymentAsync(string transactionReference);

    /// <summary>
    /// Get transaction by gateway payment ID (e.g., TrueLayer payment_id)
    /// </summary>
    Task<Transaction?> GetPaymentByGatewayIdAsync(string gatewayPaymentId);

    /// <summary>
    /// Verify payment by gateway payment ID and process if successful
    /// </summary>
    Task<PaymentVerificationResult> VerifyPaymentByGatewayIdAsync(string gatewayPaymentId);

    /// <summary>
    /// Manually trigger auto-pay processing (for testing)
    /// </summary>
    Task<AutoPayProcessingResult> TriggerAutoPayProcessingAsync(int? campaignId = null);

    /// <summary>
    /// Manually trigger auto-withdrawal for a milestone (for testing)
    /// </summary>
    Task<AutoWithdrawalResult> TriggerAutoWithdrawalAsync(int milestoneId);
}

public class BrandOutstandingBalanceDto
{
    public long OverdueAmountInPence { get; set; }
    public long TotalRemainingInPence { get; set; }
    public long TotalPaidInPence { get; set; }
    public bool HasOverdueMilestones { get; set; }
    public int OverdueMilestoneCount { get; set; }
}

public class PaymentVerificationResult
{
    public bool Success { get; set; }
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TransactionReference { get; set; }
}

public class InitiatePaymentRequest
{
    public int CampaignId { get; set; }
    public int UserId { get; set; }
    public string Gateway { get; set; } = string.Empty; // "truelayer" or "paystack"
    public int? MilestoneId { get; set; } // Null for one-time full payment
    public long? AmountInPence { get; set; } // Override amount (for partial payments)
    public bool SavePaymentMethod { get; set; } = false; // Save card for recurring (Paystack)
    public string SuccessUrl { get; set; } = string.Empty;
    public string FailureUrl { get; set; } = string.Empty;
}

public class PaymentInitiationResponse
{
    public bool Success { get; set; }
    public string? RedirectUrl { get; set; }
    public string? TransactionReference { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CampaignPaymentSummary
{
    public int CampaignId { get; set; }
    public long TotalAmountInPence { get; set; }
    public long PaidAmountInPence { get; set; }
    public long OutstandingAmountInPence { get; set; }
    public long ReleasedToInfluencerInPence { get; set; }
    public long PendingReleaseInPence { get; set; }
    public int TotalMilestones { get; set; }
    public int PaidMilestones { get; set; }
    public int PendingMilestones { get; set; }
    public PaymentMilestone? NextDueMilestone { get; set; }
}

public class AutoPayProcessingResult
{
    public int ProcessedCount { get; set; }
    public int ErrorCount { get; set; }
    public int ReminderCount { get; set; }
    public List<string> Details { get; set; } = new();
}

public class AutoWithdrawalResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int? WithdrawalId { get; set; }
    public string? Gateway { get; set; }
    public string? Status { get; set; }
}
