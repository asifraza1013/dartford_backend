using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using inflan_api.Interfaces;
using Microsoft.Extensions.Logging;
using TrueLayer.Signing;

namespace inflan_api.Services.Payment;

public class TrueLayerGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _authClient;
    private readonly ILogger<TrueLayerGateway> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _signingKeyId;
    private readonly string _signingPrivateKey;
    private readonly string _webhookSigningKey;
    private readonly string _redirectUri;
    private readonly string _merchantAccountId;
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
        _signingKeyId = configuration["TrueLayer:SigningKeyId"] ?? "";
        _signingPrivateKey = configuration["TrueLayer:SigningPrivateKey"] ?? "";
        _webhookSigningKey = configuration["TrueLayer:WebhookSigningKey"] ?? "";
        _redirectUri = configuration["TrueLayer:RedirectUri"] ?? "";
        _merchantAccountId = configuration["TrueLayer:MerchantAccountId"] ?? "";
        _isSandbox = configuration["TrueLayer:Environment"]?.ToLower() == "sandbox";

        _logger.LogInformation("TrueLayer gateway initialized. Environment={Environment}, Sandbox={IsSandbox}",
            configuration["TrueLayer:Environment"], _isSandbox);

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
            // Check if signing is configured
            if (string.IsNullOrEmpty(_signingKeyId) || string.IsNullOrEmpty(_signingPrivateKey))
            {
                _logger.LogError("TrueLayer signing keys not configured. SigningKeyId and SigningPrivateKey are required.");
                return new PaymentInitiationResult
                {
                    Success = false,
                    ErrorMessage = "TrueLayer payment gateway not fully configured. Please configure signing keys in TrueLayer Console and appsettings.json"
                };
            }

            await EnsureAccessTokenAsync();

            // Get merchant account ID
            var merchantAccountId = await GetMerchantAccountIdAsync();

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
                        merchant_account_id = merchantAccountId
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

            var jsonBody = JsonSerializer.Serialize(trueLayerRequest);
            var idempotencyKey = request.TransactionReference;
            var path = "/v3/payments";

            // Sign the request using TrueLayer.Signing library
            var tlSignature = Signer.SignWithPem(_signingKeyId, Encoding.UTF8.GetBytes(_signingPrivateKey))
                .Method("POST")
                .Path(path)
                .Header("Idempotency-Key", idempotencyKey)
                .Body(jsonBody)
                .Sign();

            // Create request with signed headers
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);
            httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);
            httpRequest.Headers.Add("Tl-Signature", tlSignature);

            var response = await _httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("TrueLayer payment initiation response: Status={Status}, Body={Response}",
                response.StatusCode, responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("TrueLayer payment initiation failed: {StatusCode} - {Response}",
                    response.StatusCode, responseBody);
                return new PaymentInitiationResult
                {
                    Success = false,
                    ErrorMessage = $"TrueLayer API error: {response.StatusCode} - {responseBody}"
                };
            }

            var trueLayerResponse = JsonSerializer.Deserialize<TrueLayerPaymentResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (trueLayerResponse != null && !string.IsNullOrEmpty(trueLayerResponse.Id))
            {
                // Build authorization URL for redirect
                var authorizationUrl = BuildAuthorizationUrl(trueLayerResponse.Id, trueLayerResponse.ResourceToken, request.SuccessRedirectUrl, request.FailureRedirectUrl);

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
                ErrorMessage = "Failed to create TrueLayer payment - no payment ID returned"
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
            // For TrueLayer webhooks, we should verify using their JWKS endpoint
            // For now, we'll parse the webhook and verify signature if configured
            if (!string.IsNullOrEmpty(signature) && !string.IsNullOrEmpty(_webhookSigningKey))
            {
                // Webhook signature verification would go here
                // TrueLayer uses JWS with their public keys from JWKS endpoint
                _logger.LogInformation("TrueLayer webhook signature present, verification would be performed");
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
                "payment_creditable" => PaymentStatusType.Successful,
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
        // Each payment requires user authentication via Open Banking
        return Task.FromResult(new ChargeResult
        {
            Success = false,
            Status = PaymentStatusType.Failed,
            ErrorMessage = "TrueLayer does not support recurring payments. Each payment requires bank authorization. Use Paystack for recurring charges."
        });
    }

    public bool ValidateWebhookSignature(string payload, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
            return true; // Skip validation if no signature provided

        // TrueLayer webhook verification should use their JWKS endpoint
        // For now, return true - proper implementation would fetch JWKS and verify
        _logger.LogWarning("TrueLayer webhook signature verification not fully implemented - accepting webhook");
        return true;
    }

    private string? _cachedMerchantAccountId;

    private async Task<string> GetMerchantAccountIdAsync()
    {
        // Use configured value if available
        if (!string.IsNullOrEmpty(_merchantAccountId))
            return _merchantAccountId;

        // Use cached value if available
        if (!string.IsNullOrEmpty(_cachedMerchantAccountId))
            return _cachedMerchantAccountId;

        // Fetch from TrueLayer API
        try
        {
            await EnsureAccessTokenAsync();
            var response = await _httpClient.GetAsync("/v3/merchant-accounts");
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("TrueLayer merchant accounts response: {Response}", responseBody);

            var merchantAccounts = JsonSerializer.Deserialize<TrueLayerMerchantAccountsResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Find GBP merchant account (for UK users)
            var gbpAccount = merchantAccounts?.Items?.FirstOrDefault(m => m.Currency == "GBP");
            if (gbpAccount != null)
            {
                _cachedMerchantAccountId = gbpAccount.Id;
                _logger.LogInformation("Using GBP merchant account: {MerchantAccountId}", gbpAccount.Id);
                return gbpAccount.Id!;
            }

            // Fallback to first available account
            var firstAccount = merchantAccounts?.Items?.FirstOrDefault();
            if (firstAccount != null)
            {
                _cachedMerchantAccountId = firstAccount.Id;
                _logger.LogInformation("Using merchant account: {MerchantAccountId}", firstAccount.Id);
                return firstAccount.Id!;
            }

            throw new Exception("No merchant accounts found in TrueLayer Console. Please create one by uploading a signing key.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch merchant accounts from TrueLayer");
            throw new Exception("TrueLayer MerchantAccountId not configured and failed to fetch from API. Please complete TrueLayer setup: upload signing keys to Console to create a merchant account.");
        }
    }

    private string BuildAuthorizationUrl(string paymentId, string? resourceToken, string? successUrl, string? failureUrl)
    {
        var basePaymentUrl = _isSandbox
            ? "https://payment.truelayer-sandbox.com/payments"
            : "https://payment.truelayer.com/payments";

        // Build return URI with payment_id as query param so frontend can verify the payment
        // TrueLayer doesn't pass our transaction reference back, only their payment_id
        var returnUri = _redirectUri;
        if (!string.IsNullOrEmpty(paymentId))
        {
            var separator = returnUri.Contains("?") ? "&" : "?";
            returnUri = $"{returnUri}{separator}payment_id={paymentId}";
        }

        var queryParams = new List<string>
        {
            $"payment_id={paymentId}",
            $"resource_token={resourceToken}",
            $"return_uri={Uri.EscapeDataString(returnUri)}"
        };

        return $"{basePaymentUrl}#{string.Join("&", queryParams)}";
    }

    private static PaymentStatusType MapTrueLayerStatus(string? status)
    {
        return status?.ToLower() switch
        {
            "executed" => PaymentStatusType.Successful,
            "settled" => PaymentStatusType.Successful,
            "creditable" => PaymentStatusType.Successful,
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

    private class TrueLayerMerchantAccountsResponse
    {
        public List<TrueLayerMerchantAccount>? Items { get; set; }
    }

    private class TrueLayerMerchantAccount
    {
        public string? Id { get; set; }
        public string? Currency { get; set; }
        [JsonPropertyName("account_holder_name")]
        public string? AccountHolderName { get; set; }
    }

    #region Payout Methods (for GBP withdrawals)

    /// <summary>
    /// Validate bank account details for TrueLayer (UK format)
    /// TrueLayer doesn't require pre-registering external accounts for open-loop payouts,
    /// so this just validates the format and returns a reference ID for tracking.
    /// </summary>
    public Task<TrueLayerBeneficiaryResult> CreateExternalAccountAsync(TrueLayerBeneficiaryRequest request)
    {
        // Validate UK sort code format (6 digits)
        var sortCodeClean = request.SortCode.Replace("-", "").Replace(" ", "");
        if (sortCodeClean.Length != 6 || !sortCodeClean.All(char.IsDigit))
        {
            return Task.FromResult(new TrueLayerBeneficiaryResult
            {
                Success = false,
                ErrorMessage = "Invalid sort code format. Must be 6 digits (e.g., 12-34-56 or 123456)"
            });
        }

        // Validate UK account number format (8 digits)
        var accountNumberClean = request.AccountNumber.Replace(" ", "");
        if (accountNumberClean.Length != 8 || !accountNumberClean.All(char.IsDigit))
        {
            return Task.FromResult(new TrueLayerBeneficiaryResult
            {
                Success = false,
                ErrorMessage = "Invalid account number format. Must be 8 digits"
            });
        }

        // For TrueLayer open-loop payouts, we don't need to pre-register the account.
        // The beneficiary details are sent directly with the payout request.
        // We generate a reference ID to track this account configuration.
        var referenceId = $"UK-{sortCodeClean}-{accountNumberClean[^4..]}";

        _logger.LogInformation("TrueLayer: Validated UK bank account for {AccountName}, SortCode={SortCode}, AccountNumber=****{Last4}",
            request.AccountName, sortCodeClean, accountNumberClean[^4..]);

        return Task.FromResult(new TrueLayerBeneficiaryResult
        {
            Success = true,
            BeneficiaryId = referenceId // This is just a reference, not a TrueLayer ID
        });
    }

    /// <summary>
    /// Initiate a payout to an external account using open-loop payout
    /// For TrueLayer, we send beneficiary details directly in the payout request
    /// </summary>
    public async Task<TrueLayerPayoutResult> InitiatePayoutAsync(TrueLayerPayoutRequest request)
    {
        try
        {
            // Check if signing is configured
            if (string.IsNullOrEmpty(_signingKeyId) || string.IsNullOrEmpty(_signingPrivateKey))
            {
                _logger.LogError("TrueLayer signing keys not configured. SigningKeyId and SigningPrivateKey are required for payouts.");
                return new TrueLayerPayoutResult
                {
                    Success = false,
                    Status = TrueLayerPayoutStatus.Failed,
                    ErrorMessage = "TrueLayer payout gateway not fully configured. Please configure signing keys."
                };
            }

            await EnsureAccessTokenAsync();

            var merchantAccountId = await GetMerchantAccountIdAsync();
            var idempotencyKey = request.Reference;

            // Build the open-loop payout request with inline beneficiary details
            // Per TrueLayer docs: https://docs.truelayer.com/docs/make-a-payout-to-an-external-account
            object payoutRequest;

            if (!string.IsNullOrEmpty(request.SortCode) && !string.IsNullOrEmpty(request.AccountNumber))
            {
                // Open-loop payout with inline beneficiary details (sort code + account number)
                var sortCodeClean = request.SortCode.Replace("-", "").Replace(" ", "");
                var accountNumberClean = request.AccountNumber.Replace(" ", "");

                payoutRequest = new
                {
                    merchant_account_id = merchantAccountId,
                    amount_in_minor = request.AmountInPence,
                    currency = "GBP",
                    beneficiary = new
                    {
                        type = "external_account",
                        account_holder_name = request.AccountName,
                        account_identifier = new
                        {
                            type = "sort_code_account_number",
                            sort_code = sortCodeClean,
                            account_number = accountNumberClean
                        },
                        reference = request.Reference.Length > 18 ? request.Reference[..18] : request.Reference
                    }
                };

                _logger.LogInformation("TrueLayer: Initiating open-loop payout to {AccountName}, SortCode={SortCode}, Amount={Amount}p",
                    request.AccountName, sortCodeClean, request.AmountInPence);
            }
            else if (!string.IsNullOrEmpty(request.ExternalAccountId))
            {
                // Payout using pre-registered external account ID (closed-loop style, but we don't use this path)
                payoutRequest = new
                {
                    merchant_account_id = merchantAccountId,
                    amount_in_minor = request.AmountInPence,
                    currency = "GBP",
                    beneficiary = new
                    {
                        type = "external_account",
                        id = request.ExternalAccountId,
                        reference = request.Reference.Length > 18 ? request.Reference[..18] : request.Reference
                    }
                };
            }
            else
            {
                return new TrueLayerPayoutResult
                {
                    Success = false,
                    Status = TrueLayerPayoutStatus.Failed,
                    ErrorMessage = "Either bank account details (sort code + account number) or external account ID is required"
                };
            }

            var jsonBody = JsonSerializer.Serialize(payoutRequest);
            var path = "/v3/payouts";

            _logger.LogInformation("TrueLayer payout request body: {Body}", jsonBody);

            // Sign the request using TrueLayer.Signing library
            var tlSignature = Signer.SignWithPem(_signingKeyId, Encoding.UTF8.GetBytes(_signingPrivateKey))
                .Method("POST")
                .Path(path)
                .Header("Idempotency-Key", idempotencyKey)
                .Body(jsonBody)
                .Sign();

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);
            httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);
            httpRequest.Headers.Add("Tl-Signature", tlSignature);

            var response = await _httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("TrueLayer payout response: Status={Status}, Body={Response}",
                response.StatusCode, responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("TrueLayer payout failed: {StatusCode} - {Response}",
                    response.StatusCode, responseBody);

                // Parse error response for better error message
                var errorMessage = $"TrueLayer API error: {response.StatusCode}";
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<TrueLayerErrorResponse>(responseBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (!string.IsNullOrEmpty(errorResponse?.Detail))
                    {
                        errorMessage = errorResponse.Detail;
                    }
                    else if (!string.IsNullOrEmpty(errorResponse?.Title))
                    {
                        errorMessage = errorResponse.Title;
                    }
                }
                catch { /* Ignore parsing errors */ }

                return new TrueLayerPayoutResult
                {
                    Success = false,
                    Status = TrueLayerPayoutStatus.Failed,
                    ErrorMessage = errorMessage
                };
            }

            var trueLayerResponse = JsonSerializer.Deserialize<TrueLayerPayoutResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (trueLayerResponse != null && !string.IsNullOrEmpty(trueLayerResponse.Id))
            {
                _logger.LogInformation("TrueLayer payout created successfully: PayoutId={PayoutId}, Status={Status}",
                    trueLayerResponse.Id, trueLayerResponse.Status);

                return new TrueLayerPayoutResult
                {
                    Success = true,
                    PayoutId = trueLayerResponse.Id,
                    Status = MapPayoutStatus(trueLayerResponse.Status)
                };
            }

            return new TrueLayerPayoutResult
            {
                Success = false,
                Status = TrueLayerPayoutStatus.Failed,
                ErrorMessage = "Failed to create payout - no ID returned"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating TrueLayer payout");
            return new TrueLayerPayoutResult
            {
                Success = false,
                Status = TrueLayerPayoutStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Get payout status
    /// </summary>
    public async Task<TrueLayerPayoutResult> GetPayoutStatusAsync(string payoutId)
    {
        try
        {
            await EnsureAccessTokenAsync();

            var response = await _httpClient.GetAsync($"/v3/payouts/{payoutId}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("TrueLayer payout status response: {Response}", responseBody);

            var trueLayerResponse = JsonSerializer.Deserialize<TrueLayerPayoutResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (trueLayerResponse != null)
            {
                return new TrueLayerPayoutResult
                {
                    Success = true,
                    PayoutId = trueLayerResponse.Id,
                    Status = MapPayoutStatus(trueLayerResponse.Status)
                };
            }

            return new TrueLayerPayoutResult
            {
                Success = false,
                Status = TrueLayerPayoutStatus.Failed,
                ErrorMessage = "Failed to get payout status"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TrueLayer payout status");
            return new TrueLayerPayoutResult
            {
                Success = false,
                Status = TrueLayerPayoutStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Get list of UK banks (for bank selection dropdown)
    /// Note: TrueLayer doesn't have a bank list API like Paystack - we use a static list
    /// </summary>
    public List<BankInfo> GetUKBanks()
    {
        // UK major banks - For TrueLayer, we don't need bank codes - we use sort codes directly
        return new List<BankInfo>
        {
            new() { Name = "Barclays", Code = "", Country = "GB" },
            new() { Name = "HSBC UK", Code = "", Country = "GB" },
            new() { Name = "Lloyds Bank", Code = "", Country = "GB" },
            new() { Name = "NatWest", Code = "", Country = "GB" },
            new() { Name = "Santander UK", Code = "", Country = "GB" },
            new() { Name = "Halifax", Code = "", Country = "GB" },
            new() { Name = "TSB Bank", Code = "", Country = "GB" },
            new() { Name = "Nationwide Building Society", Code = "", Country = "GB" },
            new() { Name = "Co-operative Bank", Code = "", Country = "GB" },
            new() { Name = "Metro Bank", Code = "", Country = "GB" },
            new() { Name = "Starling Bank", Code = "", Country = "GB" },
            new() { Name = "Monzo Bank", Code = "", Country = "GB" },
            new() { Name = "Revolut", Code = "", Country = "GB" },
            new() { Name = "Other UK Bank", Code = "", Country = "GB" }
        };
    }

    /// <summary>
    /// Process payout webhook events
    /// </summary>
    public WebhookProcessResult ProcessPayoutWebhook(string payload)
    {
        try
        {
            var webhookEvent = JsonSerializer.Deserialize<TrueLayerPayoutWebhookEvent>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (webhookEvent == null)
            {
                return new WebhookProcessResult
                {
                    Success = false,
                    ErrorMessage = "Failed to parse payout webhook payload"
                };
            }

            var status = webhookEvent.Type switch
            {
                "payout_executed" => PaymentStatusType.Successful,
                "payout_failed" => PaymentStatusType.Failed,
                _ => PaymentStatusType.Pending
            };

            return new WebhookProcessResult
            {
                Success = true,
                TransactionReference = webhookEvent.PayoutId,
                Status = status,
                IsTransferEvent = true,
                ErrorMessage = webhookEvent.FailureReason
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing TrueLayer payout webhook");
            return new WebhookProcessResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static TrueLayerPayoutStatus MapPayoutStatus(string? status)
    {
        return status?.ToLower() switch
        {
            "pending" => TrueLayerPayoutStatus.Pending,
            "authorized" => TrueLayerPayoutStatus.Authorized,
            "executed" => TrueLayerPayoutStatus.Executed,
            "failed" => TrueLayerPayoutStatus.Failed,
            _ => TrueLayerPayoutStatus.Pending
        };
    }

    // Payout DTOs
    private class TrueLayerExternalAccountResponse
    {
        public string? Id { get; set; }
    }

    private class TrueLayerPayoutResponse
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
    }

    private class TrueLayerPayoutWebhookEvent
    {
        public string? Type { get; set; }
        [JsonPropertyName("payout_id")]
        public string? PayoutId { get; set; }
        [JsonPropertyName("failure_reason")]
        public string? FailureReason { get; set; }
    }

    private class TrueLayerErrorResponse
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public string? Status { get; set; }
        [JsonPropertyName("trace_id")]
        public string? TraceId { get; set; }
    }

    #endregion
}

#region TrueLayer Payout DTOs

public class TrueLayerBeneficiaryRequest
{
    public string AccountName { get; set; } = "";
    public string AccountNumber { get; set; } = ""; // 8-digit UK account number
    public string SortCode { get; set; } = ""; // 6-digit UK sort code (XX-XX-XX or XXXXXX)
}

public class TrueLayerBeneficiaryResult
{
    public bool Success { get; set; }
    public string? BeneficiaryId { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TrueLayerPayoutRequest
{
    public long AmountInPence { get; set; }
    public string Reference { get; set; } = "";

    // For open-loop payouts - provide these directly
    public string? SortCode { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }

    // For closed-loop payouts (using pre-registered external account)
    public string? ExternalAccountId { get; set; }
}

public class TrueLayerPayoutResult
{
    public bool Success { get; set; }
    public string? PayoutId { get; set; }
    public TrueLayerPayoutStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum TrueLayerPayoutStatus
{
    Pending,
    Authorized,
    Executed,
    Failed
}

#endregion
