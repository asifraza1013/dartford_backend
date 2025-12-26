using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using inflan_api.Interfaces;
using Microsoft.Extensions.Logging;

namespace inflan_api.Services.Payment;

public class TrueLayerGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _authClient;
    private readonly ILogger<TrueLayerGateway> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _webhookSigningKey;
    private readonly string _redirectUri;
    private readonly bool _isSandbox;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private string BaseUrl => _isSandbox ? "https://api.truelayer-sandbox.com" : "https://api.truelayer.com";
    private string AuthUrl => _isSandbox ? "https://auth.truelayer-sandbox.com" : "https://auth.truelayer.com";

    public string GatewayName => "truelayer";

    public TrueLayerGateway(IConfiguration configuration, ILogger<TrueLayerGateway> logger)
    {
        _logger = logger;
        _clientId = configuration["TrueLayer:ClientId"] ?? throw new ArgumentNullException("TrueLayer:ClientId not configured");
        _clientSecret = configuration["TrueLayer:ClientSecret"] ?? throw new ArgumentNullException("TrueLayer:ClientSecret not configured");
        _webhookSigningKey = configuration["TrueLayer:WebhookSigningKey"] ?? "";
        _redirectUri = configuration["TrueLayer:RedirectUri"] ?? "";
        _isSandbox = configuration["TrueLayer:Environment"]?.ToLower() == "sandbox";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl)
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _authClient = new HttpClient
        {
            BaseAddress = new Uri(AuthUrl)
        };
    }

    private async Task EnsureAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
            return;

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", _clientId },
            { "client_secret", _clientSecret },
            { "grant_type", "client_credentials" },
            { "scope", "payments" }
        });

        var response = await _authClient.PostAsync("/connect/token", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("TrueLayer token response: {Response}", responseBody);

        var tokenResponse = JsonSerializer.Deserialize<TrueLayerTokenResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (tokenResponse?.AccessToken != null)
        {
            _accessToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60); // 60s buffer
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }
        else
        {
            throw new Exception("Failed to obtain TrueLayer access token");
        }
    }

    public async Task<PaymentInitiationResult> InitiatePaymentAsync(PaymentInitiationRequest request)
    {
        try
        {
            await EnsureAccessTokenAsync();

            // TrueLayer uses currency in minor units (pence/kobo)
            var trueLayerRequest = new
            {
                amount_in_minor = request.AmountInPence,
                currency = request.Currency,
                payment_method = new
                {
                    type = "bank_transfer",
                    provider_selection = new
                    {
                        type = "user_selected",
                        scheme_selection = new
                        {
                            type = "instant_preferred"
                        }
                    },
                    beneficiary = new
                    {
                        type = "merchant_account",
                        merchant_account_id = GetMerchantAccountId()
                    }
                },
                user = new
                {
                    name = request.CustomerName,
                    email = request.CustomerEmail
                },
                metadata = new
                {
                    transaction_reference = request.TransactionReference,
                    description = request.Description
                }
            };

            var jsonContent = JsonSerializer.Serialize(trueLayerRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Add idempotency key
            content.Headers.Add("Idempotency-Key", request.TransactionReference);

            var response = await _httpClient.PostAsync("/v3/payments", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("TrueLayer payment initiation response: {Response}", responseBody);

            var trueLayerResponse = JsonSerializer.Deserialize<TrueLayerPaymentResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (trueLayerResponse != null && !string.IsNullOrEmpty(trueLayerResponse.Id))
            {
                // Build authorization URL for redirect
                var authorizationUrl = BuildAuthorizationUrl(trueLayerResponse.Id, request.SuccessRedirectUrl, request.FailureRedirectUrl);

                return new PaymentInitiationResult
                {
                    Success = true,
                    AuthorizationUrl = authorizationUrl,
                    GatewayReference = request.TransactionReference,
                    GatewayPaymentId = trueLayerResponse.Id
                };
            }

            return new PaymentInitiationResult
            {
                Success = false,
                ErrorMessage = "Failed to create TrueLayer payment"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating TrueLayer payment");
            return new PaymentInitiationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<PaymentStatusResult> GetPaymentStatusAsync(string gatewayReference)
    {
        try
        {
            await EnsureAccessTokenAsync();

            var response = await _httpClient.GetAsync($"/v3/payments/{gatewayReference}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("TrueLayer payment status response: {Response}", responseBody);

            var trueLayerResponse = JsonSerializer.Deserialize<TrueLayerPaymentStatusResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (trueLayerResponse != null)
            {
                return new PaymentStatusResult
                {
                    Success = true,
                    Status = MapTrueLayerStatus(trueLayerResponse.Status),
                    GatewayReference = trueLayerResponse.Id,
                    AmountInPence = trueLayerResponse.AmountInMinor,
                    Currency = trueLayerResponse.Currency,
                    PaidAt = trueLayerResponse.ExecutedAt
                };
            }

            return new PaymentStatusResult
            {
                Success = false,
                Status = PaymentStatusType.Failed,
                ErrorMessage = "Failed to get payment status"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TrueLayer payment status");
            return new PaymentStatusResult
            {
                Success = false,
                Status = PaymentStatusType.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task<WebhookProcessResult> ProcessWebhookAsync(string payload, string? signature)
    {
        try
        {
            if (!ValidateWebhookSignature(payload, signature))
            {
                return Task.FromResult(new WebhookProcessResult
                {
                    Success = false,
                    ErrorMessage = "Invalid webhook signature"
                });
            }

            var webhookEvent = JsonSerializer.Deserialize<TrueLayerWebhookEvent>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (webhookEvent == null)
            {
                return Task.FromResult(new WebhookProcessResult
                {
                    Success = false,
                    ErrorMessage = "Failed to parse webhook payload"
                });
            }

            var status = webhookEvent.Type switch
            {
                "payment_executed" => PaymentStatusType.Successful,
                "payment_failed" => PaymentStatusType.Failed,
                "payment_settled" => PaymentStatusType.Successful,
                _ => PaymentStatusType.Pending
            };

            // Extract transaction reference from metadata
            var transactionReference = webhookEvent.Metadata?.TransactionReference;

            return Task.FromResult(new WebhookProcessResult
            {
                Success = true,
                TransactionReference = transactionReference,
                Status = status,
                AmountInPence = webhookEvent.AmountInMinor
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing TrueLayer webhook");
            return Task.FromResult(new WebhookProcessResult
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    public Task<ChargeResult> ChargeAuthorizationAsync(ChargeAuthorizationRequest request)
    {
        // TrueLayer doesn't support recurring payments via authorization codes
        // Each payment requires user authentication
        return Task.FromResult(new ChargeResult
        {
            Success = false,
            Status = PaymentStatusType.Failed,
            ErrorMessage = "TrueLayer does not support recurring payments. Use Paystack for recurring charges."
        });
    }

    public bool ValidateWebhookSignature(string payload, string? signature)
    {
        if (string.IsNullOrEmpty(_webhookSigningKey) || string.IsNullOrEmpty(signature))
            return true; // Skip validation if not configured

        try
        {
            // TrueLayer uses HMAC-SHA512 for webhook signatures
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_webhookSigningKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computedSignature = Convert.ToBase64String(hash);

            return computedSignature == signature;
        }
        catch
        {
            return false;
        }
    }

    private string GetMerchantAccountId()
    {
        // In production, this should come from configuration
        // The merchant account is set up in TrueLayer console
        return _isSandbox ? "sandbox-merchant-account" : "production-merchant-account";
    }

    private string BuildAuthorizationUrl(string paymentId, string? successUrl, string? failureUrl)
    {
        var baseAuthUrl = _isSandbox
            ? "https://payment.truelayer-sandbox.com/payments"
            : "https://payment.truelayer.com/payments";

        var queryParams = new List<string>
        {
            $"payment_id={paymentId}",
            $"redirect_uri={Uri.EscapeDataString(_redirectUri)}"
        };

        if (!string.IsNullOrEmpty(successUrl))
            queryParams.Add($"success_uri={Uri.EscapeDataString(successUrl)}");

        if (!string.IsNullOrEmpty(failureUrl))
            queryParams.Add($"failure_uri={Uri.EscapeDataString(failureUrl)}");

        return $"{baseAuthUrl}#{string.Join("&", queryParams)}";
    }

    private static PaymentStatusType MapTrueLayerStatus(string? status)
    {
        return status?.ToLower() switch
        {
            "executed" => PaymentStatusType.Successful,
            "settled" => PaymentStatusType.Successful,
            "failed" => PaymentStatusType.Failed,
            "authorization_required" => PaymentStatusType.Pending,
            "authorizing" => PaymentStatusType.Processing,
            _ => PaymentStatusType.Pending
        };
    }

    // TrueLayer Response DTOs
    private class TrueLayerTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }

    private class TrueLayerPaymentResponse
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        [JsonPropertyName("resource_token")]
        public string? ResourceToken { get; set; }
    }

    private class TrueLayerPaymentStatusResponse
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        [JsonPropertyName("amount_in_minor")]
        public long AmountInMinor { get; set; }
        public string? Currency { get; set; }
        [JsonPropertyName("executed_at")]
        public DateTime? ExecutedAt { get; set; }
    }

    private class TrueLayerWebhookEvent
    {
        public string? Type { get; set; }
        [JsonPropertyName("payment_id")]
        public string? PaymentId { get; set; }
        [JsonPropertyName("amount_in_minor")]
        public long AmountInMinor { get; set; }
        public TrueLayerWebhookMetadata? Metadata { get; set; }
    }

    private class TrueLayerWebhookMetadata
    {
        [JsonPropertyName("transaction_reference")]
        public string? TransactionReference { get; set; }
    }
}
