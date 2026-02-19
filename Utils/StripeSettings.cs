namespace inflan_api.Utils;

/// <summary>
/// Configuration settings for Stripe integration
/// </summary>
public class StripeSettings
{
    public string SecretKey { get; set; } = "";
    public string PublishableKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string SuccessUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
    public StripeGlobalPayoutsSettings GlobalPayouts { get; set; } = new();
}

/// <summary>
/// Configuration settings for Stripe Global Payouts
/// </summary>
public class StripeGlobalPayoutsSettings
{
    /// <summary>
    /// Whether Global Payouts is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Financial Account ID from Stripe Dashboard (starts with fa_)
    /// Required for creating outbound payments
    /// </summary>
    public string FinancialAccountId { get; set; } = "";

    /// <summary>
    /// Webhook secret for Global Payouts v2 events
    /// Different from standard Stripe webhook secret
    /// </summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>
    /// Stripe API version for Global Payouts v2 API
    /// </summary>
    public string ApiVersion { get; set; } = "2026-01-28.preview";

    /// <summary>
    /// Timeout for API requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of retry attempts for transient failures
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts in milliseconds
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;
}
