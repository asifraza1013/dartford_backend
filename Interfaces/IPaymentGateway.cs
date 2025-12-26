using inflan_api.Utils;

namespace inflan_api.Interfaces;

public interface IPaymentGateway
{
    string GatewayName { get; }

    /// <summary>
    /// Initiate a payment and return a redirect URL for the user to complete payment
    /// </summary>
    Task<PaymentInitiationResult> InitiatePaymentAsync(PaymentInitiationRequest request);

    /// <summary>
    /// Get the status of a payment from the gateway
    /// </summary>
    Task<PaymentStatusResult> GetPaymentStatusAsync(string gatewayReference);

    /// <summary>
    /// Process webhook notification from the gateway
    /// </summary>
    Task<WebhookProcessResult> ProcessWebhookAsync(string payload, string? signature);

    /// <summary>
    /// Charge a saved payment method (for recurring payments - Paystack only)
    /// </summary>
    Task<ChargeResult> ChargeAuthorizationAsync(ChargeAuthorizationRequest request);

    /// <summary>
    /// Validate webhook signature
    /// </summary>
    bool ValidateWebhookSignature(string payload, string? signature);
}

public class PaymentInitiationRequest
{
    public string TransactionReference { get; set; } = string.Empty;
    public long AmountInPence { get; set; }
    public string Currency { get; set; } = CurrencyConstants.PrimaryCurrency;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string? SuccessRedirectUrl { get; set; }
    public string? FailureRedirectUrl { get; set; }
    public bool SavePaymentMethod { get; set; } = false;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class PaymentInitiationResult
{
    public bool Success { get; set; }
    public string? AuthorizationUrl { get; set; }
    public string? GatewayReference { get; set; }
    public string? GatewayPaymentId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
}

public class PaymentStatusResult
{
    public bool Success { get; set; }
    public PaymentStatusType Status { get; set; }
    public string? GatewayReference { get; set; }
    public long? AmountInPence { get; set; }
    public string? Currency { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AuthorizationCode { get; set; } // For Paystack - to save for recurring
    public CardDetails? Card { get; set; }
}

public enum PaymentStatusType
{
    Pending,
    Processing,
    Successful,
    Failed,
    Cancelled,
    Abandoned
}

public class CardDetails
{
    public string? CardType { get; set; }
    public string? Last4 { get; set; }
    public string? ExpiryMonth { get; set; }
    public string? ExpiryYear { get; set; }
    public string? Bank { get; set; }
}

public class WebhookProcessResult
{
    public bool Success { get; set; }
    public string? TransactionReference { get; set; }
    public PaymentStatusType Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AuthorizationCode { get; set; }
    public CardDetails? Card { get; set; }
    public long? AmountInPence { get; set; }
    // For transfer events (withdrawals)
    public string? TransferCode { get; set; }
    public bool IsTransferEvent { get; set; }
}

public class ChargeAuthorizationRequest
{
    public string AuthorizationCode { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public long AmountInPence { get; set; }
    public string Currency { get; set; } = CurrencyConstants.PrimaryCurrency;
    public string TransactionReference { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class ChargeResult
{
    public bool Success { get; set; }
    public string? GatewayReference { get; set; }
    public PaymentStatusType Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
}
