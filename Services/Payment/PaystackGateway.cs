using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using inflan_api.Interfaces;
using inflan_api.Utils;
using Microsoft.Extensions.Logging;

namespace inflan_api.Services.Payment;

public class PaystackGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaystackGateway> _logger;
    private readonly string _secretKey;
    private readonly string _publicKey;
    private readonly string _webhookSecret;
    private const string BaseUrl = "https://api.paystack.co";

    public string GatewayName => "paystack";

    public PaystackGateway(IConfiguration configuration, ILogger<PaystackGateway> logger)
    {
        _logger = logger;
        _secretKey = configuration["Paystack:SecretKey"] ?? throw new ArgumentNullException("Paystack:SecretKey not configured");
        _publicKey = configuration["Paystack:PublicKey"] ?? "";
        var configuredWebhookSecret = configuration["Paystack:WebhookSecret"];
        _webhookSecret = string.IsNullOrEmpty(configuredWebhookSecret) ? _secretKey : configuredWebhookSecret;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl)
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<PaymentInitiationResult> InitiatePaymentAsync(PaymentInitiationRequest request)
    {
        try
        {
            var paystackRequest = new
            {
                email = request.CustomerEmail,
                amount = request.AmountInPence, // Paystack uses minor units (kobo/pence)
                currency = request.Currency,
                reference = request.TransactionReference,
                callback_url = request.SuccessRedirectUrl ?? request.CallbackUrl,
                metadata = new
                {
                    custom_fields = request.Metadata.Select(kv => new
                    {
                        display_name = kv.Key,
                        variable_name = kv.Key.ToLower().Replace(" ", "_"),
                        value = kv.Value
                    }).ToList(),
                    cancel_action = request.FailureRedirectUrl
                }
            };

            var jsonContent = JsonSerializer.Serialize(paystackRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/transaction/initialize", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Paystack initialize response: {Response}", responseBody);

            var paystackResponse = JsonSerializer.Deserialize<PaystackInitializeResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (paystackResponse?.Status == true && paystackResponse.Data != null)
            {
                return new PaymentInitiationResult
                {
                    Success = true,
                    AuthorizationUrl = paystackResponse.Data.AuthorizationUrl,
                    GatewayReference = paystackResponse.Data.Reference,
                    GatewayPaymentId = paystackResponse.Data.AccessCode
                };
            }

            return new PaymentInitiationResult
            {
                Success = false,
                ErrorMessage = paystackResponse?.Message ?? "Failed to initialize payment"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Paystack payment");
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
            var response = await _httpClient.GetAsync($"/transaction/verify/{gatewayReference}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Paystack verify response: {Response}", responseBody);

            var paystackResponse = JsonSerializer.Deserialize<PaystackVerifyResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (paystackResponse?.Status == true && paystackResponse.Data != null)
            {
                var data = paystackResponse.Data;
                return new PaymentStatusResult
                {
                    Success = true,
                    Status = MapPaystackStatus(data.Status),
                    GatewayReference = data.Reference,
                    AmountInPence = data.Amount,
                    Currency = data.Currency,
                    PaidAt = data.PaidAt,
                    AuthorizationCode = data.Authorization?.AuthorizationCode,
                    Card = data.Authorization != null ? new CardDetails
                    {
                        CardType = data.Authorization.CardType,
                        Last4 = data.Authorization.Last4,
                        ExpiryMonth = data.Authorization.ExpMonth,
                        ExpiryYear = data.Authorization.ExpYear,
                        Bank = data.Authorization.Bank
                    } : null
                };
            }

            return new PaymentStatusResult
            {
                Success = false,
                Status = PaymentStatusType.Failed,
                ErrorMessage = paystackResponse?.Message ?? "Failed to verify payment"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Paystack payment");
            return new PaymentStatusResult
            {
                Success = false,
                Status = PaymentStatusType.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<WebhookProcessResult> ProcessWebhookAsync(string payload, string? signature)
    {
        try
        {
            if (!ValidateWebhookSignature(payload, signature))
            {
                return new WebhookProcessResult
                {
                    Success = false,
                    ErrorMessage = "Invalid webhook signature"
                };
            }

            var webhookEvent = JsonSerializer.Deserialize<PaystackWebhookEvent>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (webhookEvent?.Event == "charge.success" && webhookEvent.Data != null)
            {
                var data = webhookEvent.Data;
                return new WebhookProcessResult
                {
                    Success = true,
                    TransactionReference = data.Reference,
                    Status = PaymentStatusType.Successful,
                    AuthorizationCode = data.Authorization?.AuthorizationCode,
                    AmountInPence = data.Amount,
                    Card = data.Authorization != null ? new CardDetails
                    {
                        CardType = data.Authorization.CardType,
                        Last4 = data.Authorization.Last4,
                        ExpiryMonth = data.Authorization.ExpMonth,
                        ExpiryYear = data.Authorization.ExpYear,
                        Bank = data.Authorization.Bank
                    } : null
                };
            }

            if (webhookEvent?.Event == "charge.failed" && webhookEvent.Data != null)
            {
                return new WebhookProcessResult
                {
                    Success = true,
                    TransactionReference = webhookEvent.Data.Reference,
                    Status = PaymentStatusType.Failed,
                    ErrorMessage = webhookEvent.Data.GatewayResponse
                };
            }

            // Handle transfer events for withdrawals
            if (webhookEvent?.Event == "transfer.success" && webhookEvent.Data != null)
            {
                return new WebhookProcessResult
                {
                    Success = true,
                    TransactionReference = webhookEvent.Data.Reference,
                    TransferCode = webhookEvent.Data.TransferCode,
                    Status = PaymentStatusType.Successful,
                    IsTransferEvent = true
                };
            }

            if (webhookEvent?.Event == "transfer.failed" && webhookEvent.Data != null)
            {
                return new WebhookProcessResult
                {
                    Success = true,
                    TransactionReference = webhookEvent.Data.Reference,
                    TransferCode = webhookEvent.Data.TransferCode,
                    Status = PaymentStatusType.Failed,
                    ErrorMessage = webhookEvent.Data.Reason ?? "Transfer failed",
                    IsTransferEvent = true
                };
            }

            if (webhookEvent?.Event == "transfer.reversed" && webhookEvent.Data != null)
            {
                return new WebhookProcessResult
                {
                    Success = true,
                    TransactionReference = webhookEvent.Data.Reference,
                    TransferCode = webhookEvent.Data.TransferCode,
                    Status = PaymentStatusType.Failed,
                    ErrorMessage = "Transfer reversed",
                    IsTransferEvent = true
                };
            }

            return new WebhookProcessResult
            {
                Success = true,
                Status = PaymentStatusType.Pending,
                ErrorMessage = $"Unhandled webhook event: {webhookEvent?.Event}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Paystack webhook");
            return new WebhookProcessResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ChargeResult> ChargeAuthorizationAsync(ChargeAuthorizationRequest request)
    {
        try
        {
            var paystackRequest = new
            {
                authorization_code = request.AuthorizationCode,
                email = request.Email,
                amount = request.AmountInPence,
                currency = request.Currency,
                reference = request.TransactionReference,
                metadata = request.Metadata
            };

            var jsonContent = JsonSerializer.Serialize(paystackRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/transaction/charge_authorization", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Paystack charge_authorization response: {Response}", responseBody);

            var paystackResponse = JsonSerializer.Deserialize<PaystackChargeResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (paystackResponse?.Status == true && paystackResponse.Data != null)
            {
                return new ChargeResult
                {
                    Success = true,
                    GatewayReference = paystackResponse.Data.Reference,
                    Status = MapPaystackStatus(paystackResponse.Data.Status)
                };
            }

            return new ChargeResult
            {
                Success = false,
                Status = PaymentStatusType.Failed,
                ErrorMessage = paystackResponse?.Message ?? "Failed to charge authorization"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error charging Paystack authorization");
            return new ChargeResult
            {
                Success = false,
                Status = PaymentStatusType.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public bool ValidateWebhookSignature(string payload, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Webhook signature is missing");
            return false;
        }

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_webhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

        var isValid = computedSignature == signature.ToLower();

        if (!isValid)
        {
            _logger.LogWarning("Webhook signature mismatch. Expected: {Expected}, Received: {Received}",
                computedSignature.Substring(0, Math.Min(20, computedSignature.Length)) + "...",
                signature.Substring(0, Math.Min(20, signature.Length)) + "...");
        }
        else
        {
            _logger.LogInformation("Webhook signature validated successfully");
        }

        return isValid;
    }

    private static PaymentStatusType MapPaystackStatus(string? status)
    {
        return status?.ToLower() switch
        {
            "success" => PaymentStatusType.Successful,
            "failed" => PaymentStatusType.Failed,
            "abandoned" => PaymentStatusType.Abandoned,
            "pending" => PaymentStatusType.Pending,
            _ => PaymentStatusType.Pending
        };
    }

    // Paystack Response DTOs
    private class PaystackInitializeResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public PaystackInitializeData? Data { get; set; }
    }

    private class PaystackInitializeData
    {
        [JsonPropertyName("authorization_url")]
        public string? AuthorizationUrl { get; set; }
        [JsonPropertyName("access_code")]
        public string? AccessCode { get; set; }
        public string? Reference { get; set; }
    }

    private class PaystackVerifyResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public PaystackTransactionData? Data { get; set; }
    }

    private class PaystackChargeResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public PaystackTransactionData? Data { get; set; }
    }

    private class PaystackTransactionData
    {
        public long Amount { get; set; }
        public string? Currency { get; set; }
        public string? Status { get; set; }
        public string? Reference { get; set; }
        [JsonPropertyName("gateway_response")]
        public string? GatewayResponse { get; set; }
        [JsonPropertyName("paid_at")]
        public DateTime? PaidAt { get; set; }
        public PaystackAuthorization? Authorization { get; set; }
        // Transfer-specific fields
        [JsonPropertyName("transfer_code")]
        public string? TransferCode { get; set; }
        public string? Reason { get; set; }
    }

    private class PaystackAuthorization
    {
        [JsonPropertyName("authorization_code")]
        public string? AuthorizationCode { get; set; }
        [JsonPropertyName("card_type")]
        public string? CardType { get; set; }
        public string? Last4 { get; set; }
        [JsonPropertyName("exp_month")]
        public string? ExpMonth { get; set; }
        [JsonPropertyName("exp_year")]
        public string? ExpYear { get; set; }
        public string? Bank { get; set; }
        public bool Reusable { get; set; }
    }

    private class PaystackWebhookEvent
    {
        public string? Event { get; set; }
        public PaystackTransactionData? Data { get; set; }
    }

    #region Transfer API (for withdrawals/payouts)

    /// <summary>
    /// Create a transfer recipient (bank account) in Paystack
    /// </summary>
    public async Task<TransferRecipientResult> CreateTransferRecipientAsync(TransferRecipientRequest request)
    {
        try
        {
            var paystackRequest = new
            {
                type = "nuban", // Nigerian bank account type
                name = request.AccountName,
                account_number = request.AccountNumber,
                bank_code = request.BankCode,
                currency = request.Currency ?? CurrencyConstants.PrimaryCurrency
            };

            var jsonContent = JsonSerializer.Serialize(paystackRequest);
            _logger.LogInformation("Paystack create recipient request: {Request}", jsonContent);

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/transferrecipient", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Paystack create recipient response (Status: {StatusCode}): {Response}",
                response.StatusCode, responseBody);

            var paystackResponse = JsonSerializer.Deserialize<PaystackRecipientResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (paystackResponse?.Status == true && paystackResponse.Data != null)
            {
                return new TransferRecipientResult
                {
                    Success = true,
                    RecipientCode = paystackResponse.Data.RecipientCode,
                    RecipientId = paystackResponse.Data.Id.ToString()
                };
            }

            return new TransferRecipientResult
            {
                Success = false,
                ErrorMessage = paystackResponse?.Message ?? "Failed to create transfer recipient"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Paystack transfer recipient");
            return new TransferRecipientResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Initiate a transfer to a recipient
    /// </summary>
    public async Task<TransferResult> InitiateTransferAsync(TransferRequest request)
    {
        try
        {
            var paystackRequest = new
            {
                source = "balance",
                amount = request.AmountInPence,
                recipient = request.RecipientCode,
                reason = request.Reason ?? "Withdrawal payout",
                reference = request.Reference,
                currency = request.Currency ?? CurrencyConstants.PrimaryCurrency
            };

            var jsonContent = JsonSerializer.Serialize(paystackRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/transfer", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Paystack transfer response (Status: {StatusCode}): {Response}",
                response.StatusCode, responseBody);

            var paystackResponse = JsonSerializer.Deserialize<PaystackTransferResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (paystackResponse?.Status == true && paystackResponse.Data != null)
            {
                return new TransferResult
                {
                    Success = true,
                    TransferCode = paystackResponse.Data.TransferCode,
                    Reference = paystackResponse.Data.Reference,
                    Status = MapTransferStatus(paystackResponse.Data.Status)
                };
            }

            return new TransferResult
            {
                Success = false,
                ErrorMessage = paystackResponse?.Message ?? "Failed to initiate transfer"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Paystack transfer");
            return new TransferResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Get list of banks for the specified country
    /// </summary>
    public async Task<List<BankInfo>> GetBanksAsync(string? country = null)
    {
        try
        {
            var countryCode = country ?? CurrencyConstants.PrimaryCountry;
            // Paystack uses different parameters for different countries
            // For Nigeria (NG), we use currency=NGN; for others, we use country code
            var queryParam = countryCode.ToUpper() == CurrencyConstants.PrimaryCountry
                ? $"currency={CurrencyConstants.PrimaryCurrency}"
                : $"country={countryCode}";
            var response = await _httpClient.GetAsync($"/bank?{queryParam}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Paystack banks response for {Country}: {Response}", country, responseBody.Substring(0, Math.Min(500, responseBody.Length)));

            var paystackResponse = JsonSerializer.Deserialize<PaystackBanksResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (paystackResponse?.Status == true && paystackResponse.Data != null)
            {
                return paystackResponse.Data.Select(b => new BankInfo
                {
                    Name = b.Name ?? "",
                    Code = b.Code ?? "",
                    Country = b.Country ?? country
                }).ToList();
            }

            return new List<BankInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching banks from Paystack");
            return new List<BankInfo>();
        }
    }

    /// <summary>
    /// Verify a bank account number
    /// </summary>
    public async Task<BankAccountVerificationResult> VerifyBankAccountAsync(string accountNumber, string bankCode)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/bank/resolve?account_number={accountNumber}&bank_code={bankCode}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Paystack verify account response: {Response}", responseBody);

            var paystackResponse = JsonSerializer.Deserialize<PaystackResolveAccountResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (paystackResponse?.Status == true && paystackResponse.Data != null)
            {
                return new BankAccountVerificationResult
                {
                    Success = true,
                    AccountName = paystackResponse.Data.AccountName,
                    AccountNumber = paystackResponse.Data.AccountNumber,
                    BankId = paystackResponse.Data.BankId.ToString()
                };
            }

            return new BankAccountVerificationResult
            {
                Success = false,
                ErrorMessage = paystackResponse?.Message ?? "Failed to verify account"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying bank account with Paystack");
            return new BankAccountVerificationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static TransferStatus MapTransferStatus(string? status)
    {
        return status?.ToLower() switch
        {
            "success" => TransferStatus.Success,
            "pending" => TransferStatus.Pending,
            "failed" => TransferStatus.Failed,
            "reversed" => TransferStatus.Reversed,
            "otp" => TransferStatus.OtpRequired,
            _ => TransferStatus.Pending
        };
    }

    // Transfer Response DTOs
    private class PaystackRecipientResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public PaystackRecipientData? Data { get; set; }
    }

    private class PaystackRecipientData
    {
        public int Id { get; set; }
        [JsonPropertyName("recipient_code")]
        public string? RecipientCode { get; set; }
    }

    private class PaystackTransferResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public PaystackTransferData? Data { get; set; }
    }

    private class PaystackTransferData
    {
        [JsonPropertyName("transfer_code")]
        public string? TransferCode { get; set; }
        public string? Reference { get; set; }
        public string? Status { get; set; }
    }

    private class PaystackBanksResponse
    {
        public bool Status { get; set; }
        public List<PaystackBankData>? Data { get; set; }
    }

    private class PaystackBankData
    {
        public string? Name { get; set; }
        public string? Code { get; set; }
        public string? Country { get; set; }
    }

    private class PaystackResolveAccountResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public PaystackResolveAccountData? Data { get; set; }
    }

    private class PaystackResolveAccountData
    {
        [JsonPropertyName("account_name")]
        public string? AccountName { get; set; }
        [JsonPropertyName("account_number")]
        public string? AccountNumber { get; set; }
        [JsonPropertyName("bank_id")]
        public int BankId { get; set; }
    }

    #endregion
}

#region Transfer DTOs

public class TransferRecipientRequest
{
    public string AccountName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string BankCode { get; set; } = "";
    public string? Currency { get; set; }
}

public class TransferRecipientResult
{
    public bool Success { get; set; }
    public string? RecipientCode { get; set; }
    public string? RecipientId { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TransferRequest
{
    public string RecipientCode { get; set; } = "";
    public long AmountInPence { get; set; }
    public string? Reason { get; set; }
    public string Reference { get; set; } = "";
    public string? Currency { get; set; }
}

public class TransferResult
{
    public bool Success { get; set; }
    public string? TransferCode { get; set; }
    public string? Reference { get; set; }
    public TransferStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BankInfo
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Country { get; set; } = "";
}

public class BankAccountVerificationResult
{
    public bool Success { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public string? BankId { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum TransferStatus
{
    Pending,
    Success,
    Failed,
    Reversed,
    OtpRequired
}

#endregion
