using System.Text;
using System.Text.Json;
using inflan_api.Interfaces;
using inflan_api.Utils;
using Stripe;
using Stripe.Checkout;
using Stripe.Treasury;

namespace inflan_api.Services.Payment;

/// <summary>
/// Stripe payment gateway implementation for UK (GBP) payments
/// Supports one-time payments, card saving, recurring charges, and Global Payouts
/// </summary>
public class StripeGateway : IPaymentGateway
{
    private readonly ILogger<StripeGateway> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StripeSettings _settings;
    private readonly string _secretKey;
    private readonly string _publishableKey;
    private readonly string _webhookSecret;
    private readonly string _successUrl;
    private readonly string _cancelUrl;
    private readonly bool _enabled;

    public string GatewayName => "stripe";

    public StripeGateway(
        IConfiguration configuration,
        ILogger<StripeGateway> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;

        // Bind Stripe settings from configuration
        _settings = new StripeSettings();
        configuration.GetSection("Stripe").Bind(_settings);

        _secretKey = _settings.SecretKey ?? throw new ArgumentNullException("Stripe:SecretKey not configured");
        _publishableKey = _settings.PublishableKey ?? throw new ArgumentNullException("Stripe:PublishableKey not configured");
        _webhookSecret = _settings.WebhookSecret ?? "";
        _successUrl = _settings.SuccessUrl ?? "https://dev.inflan.com/payment/callback?status=success";
        _cancelUrl = _settings.CancelUrl ?? "https://dev.inflan.com/payment/callback?status=cancel";
        _enabled = _settings.Enabled;

        // Validate Global Payouts configuration
        if (_settings.GlobalPayouts.Enabled && string.IsNullOrEmpty(_settings.GlobalPayouts.FinancialAccountId))
        {
            _logger.LogWarning("Stripe Global Payouts is enabled but FinancialAccountId is not configured. Payouts will fail until configured.");
        }

        if (!_enabled)
        {
            _logger.LogWarning("Stripe gateway is disabled in configuration");
        }

        // Set Stripe API key
        StripeConfiguration.ApiKey = _secretKey;

        _logger.LogInformation("Stripe Gateway initialized. Enabled: {Enabled}, GlobalPayouts: {GlobalPayoutsEnabled}",
            _enabled, _settings.GlobalPayouts.Enabled);
    }

    /// <summary>
    /// Initiate payment using Stripe Checkout Session
    /// Creates a hosted payment page with card input
    /// </summary>
    public async Task<PaymentInitiationResult> InitiatePaymentAsync(PaymentInitiationRequest request)
    {
        try
        {
            if (!_enabled)
            {
                return new PaymentInitiationResult
                {
                    Success = false,
                    ErrorMessage = "Stripe gateway is currently disabled"
                };
            }

            _logger.LogInformation("Initiating Stripe Checkout Session for reference: {Reference}", request.TransactionReference);

            var sessionService = new SessionService();

            // Build line items for checkout
            var lineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency.ToLower(),
                        UnitAmount = request.AmountInPence, // Stripe uses minor units (pence)
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = request.Description,
                            Description = $"Campaign Payment - {request.TransactionReference}"
                        }
                    },
                    Quantity = 1
                }
            };

            // Prepare metadata
            var metadata = new Dictionary<string, string>(request.Metadata)
            {
                ["transaction_reference"] = request.TransactionReference,
                ["customer_email"] = request.CustomerEmail,
                ["customer_name"] = request.CustomerName,
                ["save_payment_method"] = request.SavePaymentMethod.ToString()
            };

            // Create checkout session options
            // Add save_payment_method flag to metadata so we can check it in webhook
            if (request.SavePaymentMethod)
            {
                metadata["save_payment_method"] = "true";
            }

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = lineItems,
                Mode = "payment",
                SuccessUrl = $"{request.SuccessRedirectUrl ?? _successUrl}&session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = request.FailureRedirectUrl ?? _cancelUrl,
                ClientReferenceId = request.TransactionReference,
                Metadata = metadata,
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    Description = request.Description,
                    Metadata = metadata,
                    // Setup future usage if card saving is requested - this tells Stripe to save the payment method
                    SetupFutureUsage = request.SavePaymentMethod ? "off_session" : null
                }
            };

            // Configure customer for payment method saving
            if (request.SavePaymentMethod)
            {
                // Stripe will create a customer automatically and attach the payment method
                options.CustomerEmail = request.CustomerEmail;
                options.CustomerCreation = "always"; // Always create a customer when saving payment method

                _logger.LogInformation("Creating Stripe session with payment method saving enabled - Email: {Email}", request.CustomerEmail);
            }
            else
            {
                options.CustomerEmail = request.CustomerEmail;
            }

            // Create the checkout session
            var session = await sessionService.CreateAsync(options);

            _logger.LogInformation("Stripe Checkout Session created: {SessionId}", session.Id);

            return new PaymentInitiationResult
            {
                Success = true,
                AuthorizationUrl = session.Url,
                GatewayReference = request.TransactionReference,
                GatewayPaymentId = session.Id // Checkout Session ID
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error initiating payment: {Error}", ex.Message);
            return new PaymentInitiationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ErrorCode = ex.StripeError?.Code
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error initiating Stripe payment");
            return new PaymentInitiationResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred"
            };
        }
    }

    /// <summary>
    /// Get payment status by retrieving the Checkout Session and PaymentIntent
    /// </summary>
    public async Task<PaymentStatusResult> GetPaymentStatusAsync(string gatewayReference)
    {
        try
        {
            _logger.LogInformation("Retrieving Stripe payment status for: {Reference}", gatewayReference);

            // Try to retrieve as Checkout Session first
            if (gatewayReference.StartsWith("cs_"))
            {
                var sessionService = new SessionService();
                var session = await sessionService.GetAsync(gatewayReference, new SessionGetOptions
                {
                    Expand = new List<string> { "payment_intent", "payment_intent.payment_method" }
                });

                return await MapSessionToStatusResult(session);
            }
            // Try to retrieve as PaymentIntent
            else if (gatewayReference.StartsWith("pi_"))
            {
                var paymentIntentService = new PaymentIntentService();
                var paymentIntent = await paymentIntentService.GetAsync(gatewayReference, new PaymentIntentGetOptions
                {
                    Expand = new List<string> { "payment_method" }
                });

                return MapPaymentIntentToStatusResult(paymentIntent);
            }
            else
            {
                return new PaymentStatusResult
                {
                    Success = false,
                    Status = PaymentStatusType.Failed,
                    ErrorMessage = "Invalid Stripe reference format"
                };
            }
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error getting payment status: {Error}", ex.Message);
            return new PaymentStatusResult
            {
                Success = false,
                Status = PaymentStatusType.Failed,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting Stripe payment status");
            return new PaymentStatusResult
            {
                Success = false,
                Status = PaymentStatusType.Failed,
                ErrorMessage = "An unexpected error occurred"
            };
        }
    }

    /// <summary>
    /// Process Stripe webhook events
    /// Handles payment success, failure, and card saving events
    /// </summary>
    public async Task<WebhookProcessResult> ProcessWebhookAsync(string payload, string? signature)
    {
        try
        {
            _logger.LogInformation("Processing Stripe webhook");

            // Check if this is a Global Payouts v2 event by examining the payload
            var isV2Event = payload.Contains("\"type\":\"v2.") || payload.Contains("\"object\":\"v2.core.event\"");

            if (isV2Event && _settings.GlobalPayouts.Enabled)
            {
                _logger.LogInformation("Detected Global Payouts v2 event");
                return await ProcessGlobalPayoutsV2WebhookAsync(payload, signature);
            }

            // Standard v1 event processing
            // Verify webhook signature
            if (!string.IsNullOrEmpty(_webhookSecret) && !string.IsNullOrEmpty(signature))
            {
                if (!ValidateWebhookSignature(payload, signature))
                {
                    _logger.LogWarning("Invalid Stripe webhook signature");
                    return new WebhookProcessResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid webhook signature"
                    };
                }
            }

            // Construct Stripe event
            var stripeEvent = EventUtility.ConstructEvent(
                payload,
                signature,
                _webhookSecret,
                throwOnApiVersionMismatch: false
            );

            _logger.LogInformation("Stripe webhook event type: {EventType}", stripeEvent.Type);

            // Handle different event types
            return stripeEvent.Type switch
            {
                "checkout.session.completed" => await HandleCheckoutSessionCompleted(stripeEvent),
                "payment_intent.succeeded" => await HandlePaymentIntentSucceeded(stripeEvent),
                "payment_intent.payment_failed" => await HandlePaymentIntentFailed(stripeEvent),
                "payment_intent.canceled" => HandlePaymentIntentCanceled(stripeEvent),
                "payment_method.attached" => HandlePaymentMethodAttached(stripeEvent),
                _ => new WebhookProcessResult
                {
                    Success = true,
                    Status = PaymentStatusType.Processing
                }
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook processing error: {Error}", ex.Message);
            return new WebhookProcessResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing Stripe webhook");
            return new WebhookProcessResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred"
            };
        }
    }

    /// <summary>
    /// Process Global Payouts v2 webhook events
    /// </summary>
    private async Task<WebhookProcessResult> ProcessGlobalPayoutsV2WebhookAsync(string payload, string? signature)
    {
        try
        {
            // Validate v2 webhook signature using GlobalPayouts webhook secret
            var globalPayoutsSecret = _settings.GlobalPayouts.WebhookSecret;

            if (string.IsNullOrEmpty(globalPayoutsSecret))
            {
                _logger.LogWarning("Global Payouts webhook secret not configured");
                return new WebhookProcessResult
                {
                    Success = false,
                    ErrorMessage = "Global Payouts webhook secret not configured"
                };
            }

            if (!string.IsNullOrEmpty(signature))
            {
                // Manually validate v2 webhook signature using HMAC-SHA256
                // The Stripe .NET SDK's EventUtility does NOT support v2 events
                if (!ValidateStripeV2Signature(payload, signature, globalPayoutsSecret))
                {
                    _logger.LogWarning("Invalid Global Payouts v2 webhook signature");
                    return new WebhookProcessResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid webhook signature"
                    };
                }
            }

            // Parse the v2 event payload manually
            using var doc = System.Text.Json.JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var eventType))
            {
                _logger.LogWarning("v2 event missing type property");
                return new WebhookProcessResult
                {
                    Success = false,
                    ErrorMessage = "Invalid event format"
                };
            }

            var eventTypeName = eventType.GetString() ?? "";
            _logger.LogInformation("Global Payouts v2 event type: {EventType}, Payload: {Payload}", eventTypeName, payload);

            // Extract the outbound payment ID from the v2 event
            // v2 events have related_object at ROOT level (not inside data)
            // Structure: { type, related_object: { id, type, url }, data: {} }
            string? outboundPaymentId = null;

            // Try root-level related_object first (v2 format)
            if (root.TryGetProperty("related_object", out var relatedObject))
            {
                if (relatedObject.TryGetProperty("id", out var roIdElement))
                    outboundPaymentId = roIdElement.GetString();
            }

            // Fallback: try data.related_object (older format)
            if (string.IsNullOrEmpty(outboundPaymentId) &&
                root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("related_object", out var dataRelatedObject))
            {
                if (dataRelatedObject.TryGetProperty("id", out var drIdElement))
                    outboundPaymentId = drIdElement.GetString();
            }

            // Fallback: try data.id directly
            if (string.IsNullOrEmpty(outboundPaymentId) &&
                root.TryGetProperty("data", out var dataObj) &&
                dataObj.TryGetProperty("id", out var dataIdElement))
            {
                outboundPaymentId = dataIdElement.GetString();
            }

            if (string.IsNullOrEmpty(outboundPaymentId))
            {
                _logger.LogWarning("v2 event missing outbound payment ID. Full payload logged above.");
                return new WebhookProcessResult
                {
                    Success = false,
                    ErrorMessage = "Missing outbound payment ID"
                };
            }

            _logger.LogInformation("Processing Global Payouts v2 event for outbound payment: {PayoutId}", outboundPaymentId);

            // Handle different v2 event types
            // NOTE: v2 events don't include full payment details - we fetch from Stripe API when needed
            return eventTypeName switch
            {
                "v2.money_management.outbound_payment.created" => HandleOutboundPaymentCreated(outboundPaymentId),
                "v2.money_management.outbound_payment.posted" => HandleOutboundPaymentPosted(outboundPaymentId),
                "v2.money_management.outbound_payment.failed" => await HandleOutboundPaymentFailedAsync(outboundPaymentId),
                "v2.money_management.outbound_payment.returned" => await HandleOutboundPaymentReturnedAsync(outboundPaymentId),
                "v2.money_management.outbound_payment.canceled" => HandleOutboundPaymentCanceled(outboundPaymentId),
                // "updated" fires on every status change - fetch the actual status from Stripe API
                "v2.money_management.outbound_payment.updated" => await HandleOutboundPaymentUpdatedAsync(outboundPaymentId),
                _ => new WebhookProcessResult
                {
                    Success = true,
                    Status = PaymentStatusType.Processing,
                    IsTransferEvent = true,
                    TransactionReference = outboundPaymentId
                }
            };
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Global Payouts v2 webhook JSON");
            return new WebhookProcessResult
            {
                Success = false,
                ErrorMessage = "Invalid JSON format"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Global Payouts v2 webhook");
            return new WebhookProcessResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private WebhookProcessResult HandleOutboundPaymentCreated(string payoutId)
    {
        _logger.LogInformation("Outbound payment {PayoutId} created", payoutId);
        return new WebhookProcessResult
        {
            Success = true,
            Status = PaymentStatusType.Processing,
            IsTransferEvent = true,
            TransactionReference = payoutId
        };
    }

    private async Task<WebhookProcessResult> HandleOutboundPaymentUpdatedAsync(string payoutId)
    {
        // v2 "updated" fires on every status transition - fetch the real status from Stripe API
        var payment = await FetchOutboundPaymentAsync(payoutId);

        string? status = null;
        if (payment.HasValue && payment.Value.TryGetProperty("status", out var statusElement))
            status = statusElement.GetString();

        _logger.LogInformation("Outbound payment {PayoutId} updated, fetched status: {Status}", payoutId, status ?? "unknown");

        // Route to the correct terminal handler based on fetched status
        return status switch
        {
            "posted" => HandleOutboundPaymentPosted(payoutId),
            "failed" => await HandleOutboundPaymentFailedAsync(payoutId, payment),
            "returned" => await HandleOutboundPaymentReturnedAsync(payoutId, payment),
            "canceled" => HandleOutboundPaymentCanceled(payoutId),
            _ => new WebhookProcessResult  // "processing" or anything transitional
            {
                Success = true,
                Status = PaymentStatusType.Processing,
                IsTransferEvent = true,
                TransactionReference = payoutId
            }
        };
    }

    private WebhookProcessResult HandleOutboundPaymentPosted(string payoutId)
    {
        _logger.LogInformation("Outbound payment {PayoutId} posted (completed)", payoutId);
        return new WebhookProcessResult
        {
            Success = true,
            Status = PaymentStatusType.Successful,
            IsTransferEvent = true,
            TransactionReference = payoutId
        };
    }

    private async Task<WebhookProcessResult> HandleOutboundPaymentFailedAsync(string payoutId, System.Text.Json.JsonElement? preloadedPayment = null)
    {
        // Use pre-loaded payment data or fetch from Stripe API
        var payment = preloadedPayment ?? await FetchOutboundPaymentAsync(payoutId);

        string? failureReason = null;
        if (payment.HasValue &&
            payment.Value.TryGetProperty("failure_details", out var failureDetails) &&
            failureDetails.TryGetProperty("reason", out var reasonElement))
        {
            failureReason = reasonElement.GetString();
        }

        _logger.LogWarning("Outbound payment {PayoutId} failed: {Reason}", payoutId, failureReason ?? "Unknown reason");

        return new WebhookProcessResult
        {
            Success = true,
            Status = PaymentStatusType.Failed,
            IsTransferEvent = true,
            TransactionReference = payoutId,
            ErrorMessage = failureReason ?? "Payout failed"
        };
    }

    private async Task<WebhookProcessResult> HandleOutboundPaymentReturnedAsync(string payoutId, System.Text.Json.JsonElement? preloadedPayment = null)
    {
        // Use pre-loaded payment data or fetch from Stripe API
        var payment = preloadedPayment ?? await FetchOutboundPaymentAsync(payoutId);

        string? returnReason = null;
        if (payment.HasValue &&
            payment.Value.TryGetProperty("return_details", out var returnDetails) &&
            returnDetails.TryGetProperty("reason", out var reasonElement))
        {
            returnReason = reasonElement.GetString();
        }

        _logger.LogWarning("Outbound payment {PayoutId} returned: {Reason}", payoutId, returnReason ?? "Unknown reason");

        return new WebhookProcessResult
        {
            Success = true,
            Status = PaymentStatusType.Failed,
            IsTransferEvent = true,
            TransactionReference = payoutId,
            ErrorMessage = returnReason ?? "Payout returned by bank"
        };
    }

    /// <summary>
    /// Fetch a full outbound payment object from Stripe v2 API.
    /// v2 webhook events only include related_object reference (id/url), not the full payment data.
    /// </summary>
    private async Task<System.Text.Json.JsonElement?> FetchOutboundPaymentAsync(string outboundPaymentId)
    {
        var endpoint = $"https://api.stripe.com/v2/money_management/outbound_payments/{outboundPaymentId}";
        try
        {
            var httpClient = _httpClientFactory.CreateClient("StripeV2Api");
            httpClient.Timeout = TimeSpan.FromSeconds(_settings.GlobalPayouts.TimeoutSeconds);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("Authorization", $"Bearer {StripeConfiguration.ApiKey}");
            request.Headers.Add("Stripe-Version", _settings.GlobalPayouts.ApiVersion);

            var response = await httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Fetched outbound payment {PayoutId}: {Response}", outboundPaymentId, responseBody);
                var doc = System.Text.Json.JsonDocument.Parse(responseBody);
                return doc.RootElement.Clone();
            }

            _logger.LogWarning("Failed to fetch outbound payment {PayoutId}: {StatusCode} {Response}",
                outboundPaymentId, response.StatusCode, responseBody);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching outbound payment {PayoutId}", outboundPaymentId);
            return null;
        }
    }

    private WebhookProcessResult HandleOutboundPaymentCanceled(string payoutId)
    {
        _logger.LogInformation("Outbound payment {PayoutId} canceled", payoutId);
        return new WebhookProcessResult
        {
            Success = true,
            Status = PaymentStatusType.Failed,
            IsTransferEvent = true,
            TransactionReference = payoutId,
            ErrorMessage = "Payout canceled"
        };
    }

    /// <summary>
    /// Charge a saved Stripe PaymentMethod for recurring payments
    /// Used for milestone autopay
    /// </summary>
    public async Task<ChargeResult> ChargeAuthorizationAsync(ChargeAuthorizationRequest request)
    {
        try
        {
            _logger.LogInformation("Charging saved Stripe PaymentMethod: {PaymentMethodId}", request.AuthorizationCode);

            var paymentIntentService = new PaymentIntentService();

            // Create PaymentIntent with saved payment method
            var options = new PaymentIntentCreateOptions
            {
                Amount = request.AmountInPence,
                Currency = request.Currency.ToLower(),
                Customer = request.Metadata.GetValueOrDefault("stripe_customer_id"),
                PaymentMethod = request.AuthorizationCode, // This is the PaymentMethod ID (pm_xxx)
                OffSession = true, // Indicates payment made without customer present
                Confirm = true, // Automatically confirm the payment
                Description = $"Recurring payment - {request.TransactionReference}",
                Metadata = new Dictionary<string, string>(request.Metadata)
                {
                    ["transaction_reference"] = request.TransactionReference,
                    ["email"] = request.Email,
                    ["recurring"] = "true"
                }
            };

            var paymentIntent = await paymentIntentService.CreateAsync(options);

            _logger.LogInformation("Stripe recurring charge status: {Status}", paymentIntent.Status);

            return new ChargeResult
            {
                Success = paymentIntent.Status == "succeeded",
                GatewayReference = paymentIntent.Id,
                Status = MapStripePaymentStatusToPaymentStatusType(paymentIntent.Status),
                ErrorMessage = paymentIntent.Status == "succeeded" ? null : paymentIntent.CancellationReason
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error charging saved payment method: {Error}", ex.Message);
            return new ChargeResult
            {
                Success = false,
                Status = PaymentStatusType.Failed,
                ErrorMessage = ex.Message,
                ErrorCode = ex.StripeError?.Code
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error charging Stripe payment method");
            return new ChargeResult
            {
                Success = false,
                Status = PaymentStatusType.Failed,
                ErrorMessage = "An unexpected error occurred"
            };
        }
    }

    /// <summary>
    /// Validate Stripe webhook signature using HMAC SHA256
    /// </summary>
    public bool ValidateWebhookSignature(string payload, string? signature)
    {
        if (string.IsNullOrEmpty(_webhookSecret) || string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Webhook secret or signature is missing");
            return false;
        }

        try
        {
            // Stripe automatically validates the signature when constructing the event
            EventUtility.ConstructEvent(payload, signature, _webhookSecret, throwOnApiVersionMismatch: false);
            return true;
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature validation failed");
            return false;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Handle checkout.session.completed event
    /// </summary>
    private async Task<WebhookProcessResult> HandleCheckoutSessionCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Session;
        if (session == null)
        {
            return new WebhookProcessResult { Success = false, ErrorMessage = "Invalid session data" };
        }

        _logger.LogInformation("Checkout session completed: {SessionId}", session.Id);

        // Expand PaymentIntent to get full details
        var sessionService = new SessionService();
        var expandedSession = await sessionService.GetAsync(session.Id, new SessionGetOptions
        {
            Expand = new List<string> { "payment_intent", "payment_intent.payment_method" }
        });

        if (expandedSession.PaymentIntent == null)
        {
            return new WebhookProcessResult
            {
                Success = false,
                ErrorMessage = "No payment intent found in session"
            };
        }

        var paymentIntent = expandedSession.PaymentIntent as PaymentIntent;
        var transactionReference = session.Metadata.GetValueOrDefault("transaction_reference")
            ?? session.ClientReferenceId;

        // Check if card should be saved based on metadata flag
        var shouldSaveCard = session.Metadata.GetValueOrDefault("save_payment_method") == "true";

        _logger.LogInformation("Webhook Debug - SessionId: {SessionId}, PaymentIntentId: {PaymentIntentId}, ShouldSaveCard: {ShouldSaveCard}, CustomerId: {CustomerId}, HasPaymentMethod: {HasPaymentMethod}",
            session.Id,
            paymentIntent?.Id,
            shouldSaveCard,
            expandedSession.CustomerId,
            paymentIntent?.PaymentMethod != null);

        var result = new WebhookProcessResult
        {
            Success = true,
            TransactionReference = transactionReference,
            Status = MapStripePaymentStatusToPaymentStatusType(paymentIntent?.Status ?? "processing"),
            AmountInPence = paymentIntent?.Amount,
            CustomerId = expandedSession.CustomerId // Save Stripe customer ID
        };

        // Only extract and save card details if user chose to save card
        if (shouldSaveCard && paymentIntent?.PaymentMethod is PaymentMethod paymentMethod)
        {
            result.Card = ExtractCardDetails(paymentMethod);
            result.AuthorizationCode = paymentMethod.Id; // Save PaymentMethod ID for recurring payments
            _logger.LogInformation("Payment method will be SAVED - PaymentMethodId: {PaymentMethodId}, CustomerId: {CustomerId}, Last4: {Last4}",
                paymentMethod.Id,
                expandedSession.CustomerId,
                paymentMethod.Card?.Last4);
        }
        else
        {
            _logger.LogInformation("Payment method will NOT be saved - ShouldSaveCard: {ShouldSaveCard}, HasPaymentMethod: {HasPaymentMethod}",
                shouldSaveCard,
                paymentIntent?.PaymentMethod != null);
        }

        return result;
    }

    /// <summary>
    /// Handle payment_intent.succeeded event
    /// </summary>
    private async Task<WebhookProcessResult> HandlePaymentIntentSucceeded(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null)
        {
            return new WebhookProcessResult { Success = false, ErrorMessage = "Invalid payment intent data" };
        }

        _logger.LogInformation("Payment intent succeeded: {PaymentIntentId}", paymentIntent.Id);

        // Expand PaymentIntent to get full payment method details
        var paymentIntentService = new PaymentIntentService();
        var expandedPaymentIntent = await paymentIntentService.GetAsync(paymentIntent.Id, new PaymentIntentGetOptions
        {
            Expand = new List<string> { "payment_method" }
        });

        var transactionReference = expandedPaymentIntent.Metadata.GetValueOrDefault("transaction_reference");

        // Check if card should be saved based on metadata flag
        var shouldSaveCard = expandedPaymentIntent.Metadata.GetValueOrDefault("save_payment_method") == "true";

        _logger.LogInformation("PaymentIntent expanded - ShouldSaveCard: {ShouldSaveCard}, HasPaymentMethod: {HasPaymentMethod}, CustomerId: {CustomerId}",
            shouldSaveCard,
            expandedPaymentIntent.PaymentMethod != null,
            expandedPaymentIntent.CustomerId);

        var result = new WebhookProcessResult
        {
            Success = true,
            TransactionReference = transactionReference,
            Status = PaymentStatusType.Successful,
            AmountInPence = expandedPaymentIntent.Amount,
            CustomerId = expandedPaymentIntent.CustomerId // Save Stripe customer ID
        };

        // Only extract and save card details if user chose to save card
        if (shouldSaveCard && expandedPaymentIntent.PaymentMethod is PaymentMethod paymentMethod)
        {
            result.Card = ExtractCardDetails(paymentMethod);
            result.AuthorizationCode = paymentMethod.Id;
            _logger.LogInformation("Payment method will be SAVED - PaymentMethodId: {PaymentMethodId}, CustomerId: {CustomerId}, Last4: {Last4}",
                paymentMethod.Id,
                expandedPaymentIntent.CustomerId,
                paymentMethod.Card?.Last4);
        }
        else
        {
            _logger.LogInformation("Payment method will NOT be saved - ShouldSaveCard: {ShouldSaveCard}, HasPaymentMethod: {HasPaymentMethod}",
                shouldSaveCard,
                expandedPaymentIntent.PaymentMethod != null);
        }

        return result;
    }

    /// <summary>
    /// Handle payment_intent.payment_failed event
    /// </summary>
    private Task<WebhookProcessResult> HandlePaymentIntentFailed(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null)
        {
            return Task.FromResult(new WebhookProcessResult { Success = false, ErrorMessage = "Invalid payment intent data" });
        }

        _logger.LogWarning("Payment intent failed: {PaymentIntentId}, Reason: {Reason}",
            paymentIntent.Id, paymentIntent.CancellationReason);

        var transactionReference = paymentIntent.Metadata.GetValueOrDefault("transaction_reference");

        return Task.FromResult(new WebhookProcessResult
        {
            Success = true,
            TransactionReference = transactionReference,
            Status = PaymentStatusType.Failed,
            ErrorMessage = paymentIntent.CancellationReason ?? "Payment failed",
            AmountInPence = paymentIntent.Amount
        });
    }

    /// <summary>
    /// Handle payment_intent.canceled event
    /// </summary>
    private WebhookProcessResult HandlePaymentIntentCanceled(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null)
        {
            return new WebhookProcessResult { Success = false, ErrorMessage = "Invalid payment intent data" };
        }

        _logger.LogInformation("Payment intent canceled: {PaymentIntentId}", paymentIntent.Id);

        var transactionReference = paymentIntent.Metadata.GetValueOrDefault("transaction_reference");

        return new WebhookProcessResult
        {
            Success = true,
            TransactionReference = transactionReference,
            Status = PaymentStatusType.Cancelled,
            ErrorMessage = "Payment was canceled",
            AmountInPence = paymentIntent.Amount
        };
    }

    /// <summary>
    /// Handle payment_method.attached event
    /// </summary>
    private WebhookProcessResult HandlePaymentMethodAttached(Event stripeEvent)
    {
        var paymentMethod = stripeEvent.Data.Object as PaymentMethod;
        if (paymentMethod == null)
        {
            return new WebhookProcessResult { Success = false, ErrorMessage = "Invalid payment method data" };
        }

        _logger.LogInformation("Payment method attached: {PaymentMethodId} to customer: {CustomerId}",
            paymentMethod.Id, paymentMethod.CustomerId);

        return new WebhookProcessResult
        {
            Success = true,
            Status = PaymentStatusType.Processing,
            AuthorizationCode = paymentMethod.Id,
            Card = ExtractCardDetails(paymentMethod)
        };
    }

    /// <summary>
    /// Map Stripe Checkout Session to PaymentStatusResult
    /// </summary>
    private async Task<PaymentStatusResult> MapSessionToStatusResult(Session session)
    {
        var result = new PaymentStatusResult
        {
            Success = true,
            GatewayReference = session.Id
        };

        // Get PaymentIntent details
        if (session.PaymentIntentId != null)
        {
            var paymentIntentService = new PaymentIntentService();
            var paymentIntent = await paymentIntentService.GetAsync(session.PaymentIntentId, new PaymentIntentGetOptions
            {
                Expand = new List<string> { "payment_method" }
            });

            result.Status = MapStripePaymentStatusToPaymentStatusType(paymentIntent.Status);
            result.AmountInPence = paymentIntent.Amount;
            result.Currency = paymentIntent.Currency.ToUpper();

            if (paymentIntent.Status == "succeeded")
            {
                result.PaidAt = paymentIntent.Created;

                // Extract card details and payment method
                if (paymentIntent.PaymentMethod is PaymentMethod paymentMethod)
                {
                    result.Card = ExtractCardDetails(paymentMethod);
                    result.AuthorizationCode = paymentMethod.Id; // Stripe payment method ID
                    result.CustomerId = session.CustomerId; // Stripe customer ID
                }
            }
        }
        else
        {
            result.Status = session.PaymentStatus == "paid" ? PaymentStatusType.Successful : PaymentStatusType.Pending;
        }

        // Always set customer ID from session if available
        if (!string.IsNullOrEmpty(session.CustomerId))
        {
            result.CustomerId = session.CustomerId;
        }

        return result;
    }

    /// <summary>
    /// Map Stripe PaymentIntent to PaymentStatusResult
    /// </summary>
    private PaymentStatusResult MapPaymentIntentToStatusResult(PaymentIntent paymentIntent)
    {
        var result = new PaymentStatusResult
        {
            Success = true,
            GatewayReference = paymentIntent.Id,
            Status = MapStripePaymentStatusToPaymentStatusType(paymentIntent.Status),
            AmountInPence = paymentIntent.Amount,
            Currency = paymentIntent.Currency.ToUpper()
        };

        if (paymentIntent.Status == "succeeded")
        {
            result.PaidAt = paymentIntent.Created;

            // Extract card details
            if (paymentIntent.PaymentMethod is PaymentMethod paymentMethod)
            {
                result.Card = ExtractCardDetails(paymentMethod);
                result.AuthorizationCode = paymentMethod.Id;
            }
        }

        return result;
    }

    /// <summary>
    /// Extract card details from Stripe PaymentMethod
    /// </summary>
    private CardDetails? ExtractCardDetails(PaymentMethod paymentMethod)
    {
        if (paymentMethod.Card == null)
        {
            return null;
        }

        return new CardDetails
        {
            CardType = paymentMethod.Card.Brand,
            Last4 = paymentMethod.Card.Last4,
            ExpiryMonth = paymentMethod.Card.ExpMonth.ToString(),
            ExpiryYear = paymentMethod.Card.ExpYear.ToString(),
            Bank = paymentMethod.Card.Brand, // Stripe doesn't expose issuer bank name via API
            Reusable = true // Stripe PaymentMethods are reusable by default
        };
    }

    /// <summary>
    /// Map Stripe payment status to our PaymentStatusType enum
    /// </summary>
    private PaymentStatusType MapStripePaymentStatusToPaymentStatusType(string stripeStatus)
    {
        return stripeStatus.ToLower() switch
        {
            "succeeded" => PaymentStatusType.Successful,
            "processing" => PaymentStatusType.Processing,
            "requires_action" => PaymentStatusType.Processing,
            "requires_confirmation" => PaymentStatusType.Pending,
            "requires_payment_method" => PaymentStatusType.Pending,
            "requires_capture" => PaymentStatusType.Processing,
            "canceled" => PaymentStatusType.Cancelled,
            "failed" => PaymentStatusType.Failed,
            _ => PaymentStatusType.Pending
        };
    }

    #endregion

    #region Payout Methods (for GBP withdrawals)

    /// <summary>
    /// Create and validate a UK bank account for Stripe payouts
    /// For Global Payouts v2: Creates recipient account and payout method upfront
    /// </summary>
    public async Task<StripeBankAccountResult> CreateExternalAccountAsync(StripeBankAccountRequest request)
    {
        try
        {
            // Validate UK sort code format (6 digits)
            var sortCodeClean = request.SortCode.Replace("-", "").Replace(" ", "");
            if (sortCodeClean.Length != 6 || !sortCodeClean.All(char.IsDigit))
            {
                return new StripeBankAccountResult
                {
                    Success = false,
                    ErrorMessage = "Invalid sort code format. Must be 6 digits (e.g., 12-34-56 or 123456)"
                };
            }

            // Validate UK account number format (8 digits)
            var accountNumberClean = request.AccountNumber.Replace(" ", "");
            if (accountNumberClean.Length != 8 || !accountNumberClean.All(char.IsDigit))
            {
                return new StripeBankAccountResult
                {
                    Success = false,
                    ErrorMessage = "Invalid account number format. Must be 8 digits"
                };
            }

            // Create a bank account token for validation
            // Stripe validates bank accounts by creating a token
            var tokenService = new TokenService();
            var tokenOptions = new TokenCreateOptions
            {
                BankAccount = new TokenBankAccountOptions
                {
                    Country = "GB",
                    Currency = "gbp",
                    AccountHolderName = request.AccountName,
                    AccountHolderType = "individual",
                    RoutingNumber = sortCodeClean, // Sort code
                    AccountNumber = accountNumberClean
                }
            };

            var token = await tokenService.CreateAsync(tokenOptions);

            _logger.LogInformation("Stripe: Validated UK bank account for {AccountName}, SortCode={SortCode}, AccountNumber=****{Last4}",
                request.AccountName, sortCodeClean, accountNumberClean[^4..]);

            // For Global Payouts v2: Create recipient account and payout method upfront
            string? recipientAccountId = null;
            string? payoutMethodId = null;

            if (_settings.GlobalPayouts.Enabled)
            {
                _logger.LogInformation("Creating Stripe Global Payouts v2 recipient and payout method for {AccountName}", request.AccountName);

                // Generate a unique reference for this bank account
                var reference = $"bank_{Guid.NewGuid().ToString("N")[..8]}";

                // Step 1: Create recipient account
                var (accountId, accountError) = await CreateRecipientAccountAsync(request.AccountName, reference);
                if (string.IsNullOrEmpty(accountId))
                {
                    _logger.LogError("Failed to create recipient account: {Error}", accountError);
                    return new StripeBankAccountResult
                    {
                        Success = false,
                        ErrorMessage = $"Failed to create recipient account: {accountError}"
                    };
                }

                recipientAccountId = accountId;
                _logger.LogInformation("Created recipient account: {AccountId}", recipientAccountId);

                // Step 2: Create payout method in recipient's context
                var (methodId, gbBankAccountId, methodError) = await SetupPayoutMethodAsync(recipientAccountId, sortCodeClean, accountNumberClean, request.AccountName);
                if (string.IsNullOrEmpty(methodId))
                {
                    _logger.LogError("Failed to setup payout method: {Error}", methodError);
                    return new StripeBankAccountResult
                    {
                        Success = false,
                        ErrorMessage = $"Failed to setup payout method: {methodError}"
                    };
                }

                payoutMethodId = methodId;
                _logger.LogInformation("Setup payout method: {PayoutMethodId}", payoutMethodId);

                // Step 3: Initiate then Acknowledge Confirmation of Payee (CoP) for UK bank accounts
                // MUST call initiate first, then acknowledge
                if (!string.IsNullOrEmpty(gbBankAccountId))
                {
                    // Step 3a: Initiate CoP check
                    var (initSuccess, initError) = await InitiateConfirmationOfPayeeAsync(recipientAccountId, gbBankAccountId, request.AccountName);
                    if (!initSuccess)
                    {
                        _logger.LogError("Failed to initiate CoP, failing bank account setup: {Error}", initError);
                        return new StripeBankAccountResult
                        {
                            Success = false,
                            ErrorMessage = $"Failed to initiate Confirmation of Payee: {initError}. Please try again or contact support."
                        };
                    }

                    _logger.LogInformation("Successfully initiated Confirmation of Payee");

                    // Step 3b: Acknowledge CoP result
                    var (copSuccess, copError) = await AcknowledgeConfirmationOfPayeeAsync(recipientAccountId, gbBankAccountId);
                    if (!copSuccess)
                    {
                        _logger.LogError("Failed to acknowledge CoP, failing bank account setup: {Error}", copError);
                        return new StripeBankAccountResult
                        {
                            Success = false,
                            ErrorMessage = $"Failed to acknowledge Confirmation of Payee: {copError}. Please try again or contact support."
                        };
                    }

                    _logger.LogInformation("Successfully acknowledged Confirmation of Payee");
                }
            }

            // Return all IDs for storage
            return new StripeBankAccountResult
            {
                Success = true,
                BankAccountId = token.Id, // Token ID (legacy, for validation)
                RecipientAccountId = recipientAccountId, // v2 recipient account ID
                PayoutMethodId = payoutMethodId, // v2 payout method ID
                Last4 = accountNumberClean[^4..]
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error validating bank account: {Error}", ex.Message);
            return new StripeBankAccountResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating Stripe bank account");
            return new StripeBankAccountResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred while validating bank account"
            };
        }
    }

    /// <summary>
    /// Initiate a payout to an external bank account using Stripe Global Payouts v2 API
    /// This allows sending money directly to third-party bank accounts without requiring recipients to have Stripe accounts
    /// If RecipientAccountId and PayoutMethodId are provided, uses them directly (for saved bank accounts)
    /// </summary>
    public async Task<StripePayoutResult> InitiatePayoutAsync(StripePayoutRequest request)
    {
        try
        {
            _logger.LogInformation("Initiating Stripe Global Payout: Amount={Amount}p, Reference={Reference}, RecipientAccountId={RecipientAccountId}, PayoutMethodId={PayoutMethodId}",
                request.AmountInPence, request.Reference, request.RecipientAccountId, request.PayoutMethodId);

            string recipientAccountId;
            string payoutMethodId;

            // Check if we have stored IDs (from saved bank account)
            if (!string.IsNullOrEmpty(request.RecipientAccountId) && !string.IsNullOrEmpty(request.PayoutMethodId))
            {
                // Use provided IDs - bank account was already set up when user added it
                recipientAccountId = request.RecipientAccountId;
                payoutMethodId = request.PayoutMethodId;

                _logger.LogInformation("Using stored recipient and payout method: RecipientAccountId={RecipientAccountId}, PayoutMethodId={PayoutMethodId}",
                    recipientAccountId, payoutMethodId);
            }
            else
            {
                // No stored IDs - need to create them (one-time use)
                _logger.LogInformation("No stored IDs provided, creating new recipient and payout method");

                // Validate UK bank account format
                var sortCodeClean = request.SortCode.Replace("-", "").Replace(" ", "");
                var accountNumberClean = request.AccountNumber.Replace(" ", "");

                if (sortCodeClean.Length != 6 || !sortCodeClean.All(char.IsDigit))
                {
                    return new StripePayoutResult
                    {
                        Success = false,
                        Status = StripePayoutStatus.Failed,
                        ErrorMessage = "Invalid sort code format. Must be 6 digits."
                    };
                }

                if (accountNumberClean.Length != 8 || !accountNumberClean.All(char.IsDigit))
                {
                    return new StripePayoutResult
                    {
                        Success = false,
                        Status = StripePayoutStatus.Failed,
                        ErrorMessage = "Invalid account number format. Must be 8 digits."
                    };
                }

                // Step 1: Create recipient account
                var (accountId, recipientError) = await CreateRecipientAccountAsync(request.AccountName, request.Reference);
                if (string.IsNullOrEmpty(accountId))
                {
                    return new StripePayoutResult
                    {
                        Success = false,
                        Status = StripePayoutStatus.Failed,
                        ErrorMessage = $"Failed to create recipient account: {recipientError ?? "Unknown error"}"
                    };
                }

                recipientAccountId = accountId;
                _logger.LogInformation("Created recipient account: {AccountId}", recipientAccountId);

                // Step 2: Create payout method in recipient's context
                var (methodId, gbBankAccountId, payoutMethodError) = await SetupPayoutMethodAsync(recipientAccountId, sortCodeClean, accountNumberClean, request.AccountName);
                if (string.IsNullOrEmpty(methodId))
                {
                    return new StripePayoutResult
                    {
                        Success = false,
                        Status = StripePayoutStatus.Failed,
                        ErrorMessage = $"Failed to setup payout method: {payoutMethodError ?? "Unknown error"}"
                    };
                }

                payoutMethodId = methodId;
                _logger.LogInformation("Setup payout method: {PayoutMethodId}", payoutMethodId);

                // Step 3: Initiate then Acknowledge Confirmation of Payee (CoP) for UK bank accounts
                // MUST call initiate first, then acknowledge
                if (!string.IsNullOrEmpty(gbBankAccountId))
                {
                    // Step 3a: Initiate CoP check
                    var (initSuccess, initError) = await InitiateConfirmationOfPayeeAsync(recipientAccountId, gbBankAccountId, request.AccountName);
                    if (!initSuccess)
                    {
                        _logger.LogError("Failed to initiate CoP, failing payout: {Error}", initError);
                        return new StripePayoutResult
                        {
                            Success = false,
                            Status = StripePayoutStatus.Failed,
                            ErrorMessage = $"Failed to initiate Confirmation of Payee: {initError}. Please try again or contact support."
                        };
                    }

                    _logger.LogInformation("Successfully initiated Confirmation of Payee");

                    // Step 3b: Acknowledge CoP result
                    var (copSuccess, copError) = await AcknowledgeConfirmationOfPayeeAsync(recipientAccountId, gbBankAccountId);
                    if (!copSuccess)
                    {
                        _logger.LogError("Failed to acknowledge CoP, failing payout: {Error}", copError);
                        return new StripePayoutResult
                        {
                            Success = false,
                            Status = StripePayoutStatus.Failed,
                            ErrorMessage = $"Failed to acknowledge Confirmation of Payee: {copError}. Please try again or contact support."
                        };
                    }

                    _logger.LogInformation("Successfully acknowledged Confirmation of Payee");
                }
            }

            // Create outbound payment (v2 OutboundPayments API)
            var (outboundPaymentId, outboundPaymentError) = await CreateOutboundPaymentAsync(
                recipientAccountId,
                payoutMethodId,
                request.AmountInPence,
                request.Reference
            );

            if (string.IsNullOrEmpty(outboundPaymentId))
            {
                return new StripePayoutResult
                {
                    Success = false,
                    Status = StripePayoutStatus.Failed,
                    ErrorMessage = $"Failed to create outbound payment: {outboundPaymentError ?? "Unknown error"}"
                };
            }

            _logger.LogInformation("Stripe Global Payout created successfully: PayoutId={PayoutId}",
                outboundPaymentId);

            return new StripePayoutResult
            {
                Success = true,
                PayoutId = outboundPaymentId,
                Status = StripePayoutStatus.Pending, // Global Payouts start as processing
                ArrivalDate = null // Will be updated by webhook
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error initiating Global Payout: {Error}", ex.Message);
            return new StripePayoutResult
            {
                Success = false,
                Status = StripePayoutStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error initiating Stripe Global Payout");
            return new StripePayoutResult
            {
                Success = false,
                Status = StripePayoutStatus.Failed,
                ErrorMessage = "An unexpected error occurred: " + ex.Message
            };
        }
    }

    /// <summary>
    /// Create a recipient account using Stripe v2 Accounts API
    /// Implements retry logic for transient failures
    /// Returns (accountId, errorMessage) tuple
    /// </summary>
    private async Task<(string? accountId, string? errorMessage)> CreateRecipientAccountAsync(string accountName, string reference)
    {
        const string endpoint = "https://api.stripe.com/v2/core/accounts";
        var idempotencyKey = $"recipient_{reference}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        _logger.LogInformation("Creating Stripe recipient account: DisplayName={DisplayName}, Reference={Reference}",
            accountName, reference);

        // Split account name into given_name and surname
        var nameParts = accountName.Trim().Split(' ', 2);
        var givenName = nameParts[0];
        var surname = nameParts.Length > 1 ? nameParts[1] : nameParts[0];

        var requestBody = new
        {
            contact_email = $"noreply+{reference}@inflan.com",
            display_name = accountName,
            identity = new
            {
                country = "GB",
                entity_type = "individual",
                individual = new
                {
                    given_name = givenName,
                    surname = surname
                }
            },
            configuration = new
            {
                recipient = new
                {
                    capabilities = new
                    {
                        bank_accounts = new
                        {
                            local = new
                            {
                                requested = true
                            }
                        }
                    }
                }
            }
        };

        for (int attempt = 1; attempt <= _settings.GlobalPayouts.RetryCount; attempt++)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("StripeV2Api");
                httpClient.Timeout = TimeSpan.FromSeconds(_settings.GlobalPayouts.TimeoutSeconds);

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("Authorization", $"Bearer {StripeConfiguration.ApiKey}");
                request.Headers.Add("Stripe-Version", _settings.GlobalPayouts.ApiVersion);
                request.Headers.Add("Idempotency-Key", idempotencyKey);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    var accountId = result.GetProperty("id").GetString();

                    // Log capability status so we can debug activation issues
                    _logger.LogInformation("Successfully created recipient account: AccountId={AccountId}, FullResponse={Response}",
                        accountId, responseBody);
                    return (accountId, null);
                }

                // Handle retryable errors (429, 500, 502, 503, 504)
                if (IsRetryableStatusCode(response.StatusCode) && attempt < _settings.GlobalPayouts.RetryCount)
                {
                    _logger.LogWarning("Retryable error creating recipient account (attempt {Attempt}/{MaxAttempts}): {StatusCode}",
                        attempt, _settings.GlobalPayouts.RetryCount, response.StatusCode);
                    await Task.Delay(_settings.GlobalPayouts.RetryDelayMs * attempt); // Exponential backoff
                    continue;
                }

                // Log non-retryable errors and return the response for debugging
                var errorMessage = $"HTTP {(int)response.StatusCode}: {responseBody}";
                _logger.LogError("Failed to create recipient account: StatusCode={StatusCode}, Response={Response}",
                    response.StatusCode, responseBody);
                return (null, errorMessage);
            }
            catch (TaskCanceledException ex)
            {
                var errorMessage = $"Request timeout after {_settings.GlobalPayouts.TimeoutSeconds}s";
                _logger.LogError(ex, "Timeout creating recipient account (attempt {Attempt}/{MaxAttempts})",
                    attempt, _settings.GlobalPayouts.RetryCount);

                if (attempt < _settings.GlobalPayouts.RetryCount)
                {
                    await Task.Delay(_settings.GlobalPayouts.RetryDelayMs * attempt);
                    continue;
                }
                return (null, errorMessage);
            }
            catch (HttpRequestException ex)
            {
                var errorMessage = $"Network error: {ex.Message}";
                _logger.LogError(ex, "Network error creating recipient account (attempt {Attempt}/{MaxAttempts})",
                    attempt, _settings.GlobalPayouts.RetryCount);

                if (attempt < _settings.GlobalPayouts.RetryCount)
                {
                    await Task.Delay(_settings.GlobalPayouts.RetryDelayMs * attempt);
                    continue;
                }
                return (null, errorMessage);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Unexpected error: {ex.Message}";
                _logger.LogError(ex, "Unexpected error creating recipient account");
                return (null, errorMessage);
            }
        }

        return (null, "Failed after all retry attempts");
    }

    /// <summary>
    /// Setup payout method using Stripe v2 OutboundSetupIntents API
    /// Implements retry logic for transient failures
    /// Returns (payoutMethodId, gbBankAccountId, errorMessage) tuple
    /// NOTE: Payout methods MUST be created in a recipient account's context
    /// </summary>
    private async Task<(string? payoutMethodId, string? gbBankAccountId, string? errorMessage)> SetupPayoutMethodAsync(string recipientAccountId, string sortCode, string accountNumber, string accountName)
    {
        const string endpoint = "https://api.stripe.com/v2/money_management/outbound_setup_intents";
        var idempotencyKey = $"payout_method_{sortCode}_{accountNumber[^4..]}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        _logger.LogInformation("Setting up Stripe payout method: AccountName={AccountName}, SortCode={SortCode}, Last4={Last4}",
            accountName, sortCode, accountNumber[^4..]);

        var requestBody = new
        {
            payout_method_data = new
            {
                type = "bank_account",
                bank_account = new
                {
                    country = "GB",
                    account_number = accountNumber,
                    routing_number = sortCode  // UK sort code (6 digits)
                }
            }
        };

        for (int attempt = 1; attempt <= _settings.GlobalPayouts.RetryCount; attempt++)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("StripeV2Api");
                httpClient.Timeout = TimeSpan.FromSeconds(_settings.GlobalPayouts.TimeoutSeconds);

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("Authorization", $"Bearer {StripeConfiguration.ApiKey}");
                request.Headers.Add("Stripe-Version", _settings.GlobalPayouts.ApiVersion);
                request.Headers.Add("Stripe-Context", recipientAccountId); // CRITICAL: Create payout method in recipient's context
                request.Headers.Add("Idempotency-Key", idempotencyKey);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseBody);

                    // The response contains the OutboundSetupIntent with a nested payout_method
                    // Extract payout method ID and GB bank account ID from the response
                    string? payoutMethodId = null;
                    string? gbBankAccountId = null;

                    if (result.TryGetProperty("payout_method", out var payoutMethod))
                    {
                        if (payoutMethod.TryGetProperty("id", out var idElement))
                        {
                            payoutMethodId = idElement.GetString();

                            // For UK bank accounts, the payout_method ID itself IS the GB bank account ID (starts with gbba_)
                            // For other types, we would need to extract from nested properties
                            if (!string.IsNullOrEmpty(payoutMethodId) && payoutMethodId.StartsWith("gbba_"))
                            {
                                gbBankAccountId = payoutMethodId;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(payoutMethodId))
                    {
                        _logger.LogInformation("Successfully setup payout method: PayoutMethodId={PayoutMethodId}, GbBankAccountId={GbBankAccountId}",
                            payoutMethodId, gbBankAccountId ?? "N/A");
                        return (payoutMethodId, gbBankAccountId, null);
                    }

                    _logger.LogError("Payout method setup succeeded but no payout method ID found in response. Response: {Response}", responseBody);
                    return (null, null, "No payout method ID found in response");
                }

                if (IsRetryableStatusCode(response.StatusCode) && attempt < _settings.GlobalPayouts.RetryCount)
                {
                    _logger.LogWarning("Retryable error setting up payout method (attempt {Attempt}/{MaxAttempts}): {StatusCode}",
                        attempt, _settings.GlobalPayouts.RetryCount, response.StatusCode);
                    await Task.Delay(_settings.GlobalPayouts.RetryDelayMs * attempt);
                    continue;
                }

                // Log non-retryable errors and return the response for debugging
                var errorMessage = $"HTTP {(int)response.StatusCode}: {responseBody}";
                _logger.LogError("Failed to setup payout method: StatusCode={StatusCode}, Response={Response}",
                    response.StatusCode, responseBody);
                return (null, null, errorMessage);
            }
            catch (TaskCanceledException ex)
            {
                var errorMessage = $"Request timeout after {_settings.GlobalPayouts.TimeoutSeconds}s";
                _logger.LogError(ex, "Timeout setting up payout method (attempt {Attempt}/{MaxAttempts})",
                    attempt, _settings.GlobalPayouts.RetryCount);

                if (attempt < _settings.GlobalPayouts.RetryCount)
                {
                    await Task.Delay(_settings.GlobalPayouts.RetryDelayMs * attempt);
                    continue;
                }
                return (null, null, errorMessage);
            }
            catch (HttpRequestException ex)
            {
                var errorMessage = $"Network error: {ex.Message}";
                _logger.LogError(ex, "Network error setting up payout method (attempt {Attempt}/{MaxAttempts})",
                    attempt, _settings.GlobalPayouts.RetryCount);

                if (attempt < _settings.GlobalPayouts.RetryCount)
                {
                    await Task.Delay(_settings.GlobalPayouts.RetryDelayMs * attempt);
                    continue;
                }
                return (null, null, errorMessage);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Unexpected error: {ex.Message}";
                _logger.LogError(ex, "Unexpected error setting up payout method");
                return (null, null, errorMessage);
            }
        }

        return (null, null, "Failed after all retry attempts");
    }

    /// <summary>
    /// Acknowledge Confirmation of Payee (CoP) result for UK bank accounts
    /// Required before creating outbound payments if CoP result is not a perfect match
    /// Returns (success, errorMessage) tuple
    /// Implements retry logic for transient failures
    /// </summary>
    /// <summary>
    /// Initiate Confirmation of Payee (CoP) for UK bank accounts
    /// MUST be called before AcknowledgeConfirmationOfPayeeAsync
    /// Returns (success, errorMessage) tuple
    /// </summary>
    private async Task<(bool success, string? errorMessage)> InitiateConfirmationOfPayeeAsync(string recipientAccountId, string gbBankAccountId, string accountName)
    {
        var endpoint = $"https://api.stripe.com/v2/core/vault/gb_bank_accounts/{gbBankAccountId}/initiate_confirmation_of_payee";
        var idempotencyKey = $"cop_init_{gbBankAccountId}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        _logger.LogInformation("Initiating Confirmation of Payee: RecipientAccountId={RecipientAccountId}, GbBankAccountId={GbBankAccountId}, Name={Name}",
            recipientAccountId, gbBankAccountId, accountName);

        for (int attempt = 1; attempt <= _settings.GlobalPayouts.RetryCount; attempt++)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("StripeV2Api");
                httpClient.Timeout = TimeSpan.FromSeconds(_settings.GlobalPayouts.TimeoutSeconds);

                var requestBody = new { name = accountName, business_type = "personal" };

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("Authorization", $"Bearer {StripeConfiguration.ApiKey}");
                request.Headers.Add("Stripe-Version", _settings.GlobalPayouts.ApiVersion);
                request.Headers.Add("Stripe-Context", recipientAccountId);
                request.Headers.Add("Idempotency-Key", idempotencyKey);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully initiated CoP for GbBankAccountId={GbBankAccountId}, Response={Response}",
                        gbBankAccountId, responseBody);
                    return (true, null);
                }

                if (IsRetryableStatusCode(response.StatusCode) && attempt < _settings.GlobalPayouts.RetryCount)
                {
                    _logger.LogWarning("Retryable error initiating CoP (attempt {Attempt}/{MaxAttempts}): {StatusCode}",
                        attempt, _settings.GlobalPayouts.RetryCount, response.StatusCode);
                    await Task.Delay(_settings.GlobalPayouts.RetryDelayMs * attempt);
                    continue;
                }

                var errorMessage = $"HTTP {(int)response.StatusCode}: {responseBody}";
                _logger.LogError("Failed to initiate CoP: StatusCode={StatusCode}, Response={Response}",
                    response.StatusCode, responseBody);
                return (false, errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating CoP (attempt {Attempt}/{MaxAttempts})",
                    attempt, _settings.GlobalPayouts.RetryCount);

                if (attempt < _settings.GlobalPayouts.RetryCount)
                {
                    await Task.Delay(_settings.GlobalPayouts.RetryDelayMs * attempt);
                    continue;
                }

                return (false, $"Error initiating CoP: {ex.Message}");
            }
        }

        return (false, "Failed to initiate CoP after all retry attempts");
    }

    private async Task<(bool success, string? errorMessage)> AcknowledgeConfirmationOfPayeeAsync(string recipientAccountId, string gbBankAccountId)
    {
        var endpoint = $"https://api.stripe.com/v2/core/vault/gb_bank_accounts/{gbBankAccountId}/acknowledge_confirmation_of_payee";
        var idempotencyKey = $"cop_ack_{gbBankAccountId}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        _logger.LogInformation("Acknowledging Confirmation of Payee: RecipientAccountId={RecipientAccountId}, GbBankAccountId={GbBankAccountId}",
            recipientAccountId, gbBankAccountId);

        for (int attempt = 1; attempt <= _settings.GlobalPayouts.RetryCount; attempt++)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("StripeV2Api");
                httpClient.Timeout = TimeSpan.FromSeconds(_settings.GlobalPayouts.TimeoutSeconds);

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("Authorization", $"Bearer {StripeConfiguration.ApiKey}");
                request.Headers.Add("Stripe-Version", _settings.GlobalPayouts.ApiVersion);
                request.Headers.Add("Stripe-Context", recipientAccountId); // CRITICAL: Must provide recipient account context
                request.Headers.Add("Idempotency-Key", idempotencyKey);
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully acknowledged CoP for GbBankAccountId={GbBankAccountId}", gbBankAccountId);
                    return (true, null);
                }

                // Handle retryable errors (500, 502, 503, 504)
                if (IsRetryableStatusCode(response.StatusCode) && attempt < _settings.GlobalPayouts.RetryCount)
                {
                    _logger.LogWarning("Retryable error acknowledging CoP (attempt {Attempt}/{MaxAttempts}): {StatusCode}",
                        attempt, _settings.GlobalPayouts.RetryCount, response.StatusCode);
                    await Task.Delay(_settings.GlobalPayouts.RetryDelayMs * attempt);
                    continue;
                }

                var errorMessage = $"HTTP {(int)response.StatusCode}: {responseBody}";
                _logger.LogError("Failed to acknowledge CoP: StatusCode={StatusCode}, Response={Response}",
                    response.StatusCode, responseBody);
                return (false, errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acknowledging CoP (attempt {Attempt}/{MaxAttempts})",
                    attempt, _settings.GlobalPayouts.RetryCount);

                if (attempt < _settings.GlobalPayouts.RetryCount)
                {
                    await Task.Delay(_settings.GlobalPayouts.RetryDelayMs * attempt);
                    continue;
                }

                var errorMessage = $"Error acknowledging CoP: {ex.Message}";
                return (false, errorMessage);
            }
        }

        return (false, "Failed to acknowledge CoP after all retry attempts");
    }

    /// <summary>
    /// Create outbound payment using Stripe v2 OutboundPayments API
    /// Implements retry logic for transient failures
    /// CRITICAL: Uses idempotency key to prevent duplicate payouts
    /// Returns (paymentId, errorMessage) tuple
    /// </summary>
    private async Task<(string? paymentId, string? errorMessage)> CreateOutboundPaymentAsync(string recipientAccountId, string payoutMethodId, long amountInPence, string reference)
    {
        const string endpoint = "https://api.stripe.com/v2/money_management/outbound_payments";

        // CRITICAL: Idempotency key prevents duplicate payouts if retry occurs
        var idempotencyKey = $"payout_{reference}_{amountInPence}";

        // Validate Financial Account ID is configured
        if (string.IsNullOrEmpty(_settings.GlobalPayouts.FinancialAccountId))
        {
            _logger.LogError("Stripe Global Payouts FinancialAccountId is not configured. Cannot create outbound payment.");
            return (null, "FinancialAccountId not configured");
        }

        _logger.LogInformation("Creating Stripe outbound payment: RecipientAccountId={RecipientAccountId}, PayoutMethodId={PayoutMethodId}, Amount={Amount}p, Reference={Reference}",
            recipientAccountId, payoutMethodId, amountInPence, reference);

        var requestBody = new
        {
            from = new
            {
                financial_account = _settings.GlobalPayouts.FinancialAccountId,
                currency = "gbp"
            },
            to = new
            {
                recipient = recipientAccountId,
                payout_method = payoutMethodId
            },
            amount = new
            {
                value = amountInPence,
                currency = "gbp"
            },
            description = $"Inflan withdrawal - {reference}"
        };

        for (int attempt = 1; attempt <= _settings.GlobalPayouts.RetryCount; attempt++)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("StripeV2Api");
                httpClient.Timeout = TimeSpan.FromSeconds(_settings.GlobalPayouts.TimeoutSeconds);

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("Authorization", $"Bearer {StripeConfiguration.ApiKey}");
                request.Headers.Add("Stripe-Version", _settings.GlobalPayouts.ApiVersion);
                request.Headers.Add("Idempotency-Key", idempotencyKey); // CRITICAL: Prevents duplicate payouts
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    var paymentId = result.GetProperty("id").GetString();

                    _logger.LogInformation("Successfully created outbound payment: PaymentId={PaymentId}, Amount={Amount}p",
                        paymentId, amountInPence);
                    return (paymentId, null);
                }

                // For payment creation, be very careful with retries to avoid duplicate payments
                // Only retry on 429 (rate limit) or 500/502/503/504 (server errors)
                if (IsRetryableStatusCode(response.StatusCode) && attempt < _settings.GlobalPayouts.RetryCount)
                {
                    _logger.LogWarning("Retryable error creating outbound payment (attempt {Attempt}/{MaxAttempts}): {StatusCode}",
                        attempt, _settings.GlobalPayouts.RetryCount, response.StatusCode);
                    await Task.Delay(_settings.GlobalPayouts.RetryDelayMs * attempt);
                    continue;
                }

                // Non-retryable error - log details and return error message
                var errorMessage = $"HTTP {(int)response.StatusCode}: {responseBody}";
                _logger.LogError("Failed to create outbound payment: StatusCode={StatusCode}, Response={Response}",
                    response.StatusCode, responseBody);
                return (null, errorMessage);
            }
            catch (TaskCanceledException ex)
            {
                var errorMessage = $"Request timeout after {_settings.GlobalPayouts.TimeoutSeconds}s";
                _logger.LogError(ex, "Timeout creating outbound payment (attempt {Attempt}/{MaxAttempts})",
                    attempt, _settings.GlobalPayouts.RetryCount);

                if (attempt < _settings.GlobalPayouts.RetryCount)
                {
                    await Task.Delay(_settings.GlobalPayouts.RetryDelayMs * attempt);
                    continue;
                }
                return (null, errorMessage);
            }
            catch (HttpRequestException ex)
            {
                var errorMessage = $"Network error: {ex.Message}";
                _logger.LogError(ex, "Network error creating outbound payment (attempt {Attempt}/{MaxAttempts})",
                    attempt, _settings.GlobalPayouts.RetryCount);

                if (attempt < _settings.GlobalPayouts.RetryCount)
                {
                    await Task.Delay(_settings.GlobalPayouts.RetryDelayMs * attempt);
                    continue;
                }
                return (null, errorMessage);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Unexpected error: {ex.Message}";
                _logger.LogError(ex, "Unexpected error creating outbound payment");
                return (null, errorMessage);
            }
        }

        return (null, "Failed after all retry attempts");
    }

    /// <summary>
    /// Determines if an HTTP status code indicates a retryable error
    /// </summary>
    private static bool IsRetryableStatusCode(System.Net.HttpStatusCode statusCode)
    {
        return statusCode == System.Net.HttpStatusCode.TooManyRequests || // 429 - Rate limit
               statusCode == System.Net.HttpStatusCode.InternalServerError || // 500
               statusCode == System.Net.HttpStatusCode.BadGateway || // 502
               statusCode == System.Net.HttpStatusCode.ServiceUnavailable || // 503
               statusCode == System.Net.HttpStatusCode.GatewayTimeout; // 504
    }

    /// <summary>
    /// Manually validate Stripe v2 webhook signature using HMAC-SHA256.
    /// The Stripe .NET SDK's EventUtility.ConstructEvent does NOT support v2 events.
    /// Stripe signature header format: t=timestamp,v1=hash
    /// Hash = HMAC-SHA256("{timestamp}.{payload}", webhookSecret)
    /// </summary>
    private static bool ValidateStripeV2Signature(string payload, string signatureHeader, string secret)
    {
        try
        {
            // Parse t= and v1= from header
            string? timestamp = null;
            string? signature = null;

            foreach (var part in signatureHeader.Split(','))
            {
                if (part.StartsWith("t=")) timestamp = part[2..];
                else if (part.StartsWith("v1=")) signature = part[3..];
            }

            if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
                return false;

            // Compute expected signature
            var signedPayload = $"{timestamp}.{payload}";
            var secretBytes = Encoding.UTF8.GetBytes(secret);
            var payloadBytes = Encoding.UTF8.GetBytes(signedPayload);

            using var hmac = new System.Security.Cryptography.HMACSHA256(secretBytes);
            var computedHash = hmac.ComputeHash(payloadBytes);
            var computedSignature = BitConverter.ToString(computedHash).Replace("-", "").ToLower();

            return computedSignature == signature;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get payout status from Stripe
    /// </summary>
    public async Task<StripePayoutResult> GetPayoutStatusAsync(string payoutId)
    {
        try
        {
            var payoutService = new PayoutService();
            var payout = await payoutService.GetAsync(payoutId);

            return new StripePayoutResult
            {
                Success = true,
                PayoutId = payout.Id,
                Status = MapPayoutStatus(payout.Status),
                ArrivalDate = payout.ArrivalDate,
                ErrorMessage = payout.FailureMessage
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error getting payout status: {Error}", ex.Message);
            return new StripePayoutResult
            {
                Success = false,
                Status = StripePayoutStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Map Stripe payout status to our enum
    /// </summary>
    private StripePayoutStatus MapPayoutStatus(string stripeStatus)
    {
        return stripeStatus?.ToLower() switch
        {
            "pending" => StripePayoutStatus.Pending,
            "in_transit" => StripePayoutStatus.InTransit,
            "paid" => StripePayoutStatus.Paid,
            "failed" => StripePayoutStatus.Failed,
            "canceled" => StripePayoutStatus.Canceled,
            _ => StripePayoutStatus.Pending
        };
    }

    #endregion
}

#region Stripe Payout DTOs

public class StripeBankAccountRequest
{
    public string AccountName { get; set; } = "";
    public string AccountNumber { get; set; } = ""; // 8-digit UK account number
    public string SortCode { get; set; } = ""; // 6-digit UK sort code (XX-XX-XX or XXXXXX)
}

public class StripeBankAccountResult
{
    public bool Success { get; set; }
    public string? BankAccountId { get; set; } // Stripe token ID or external account ID (legacy)
    public string? RecipientAccountId { get; set; } // v2 recipient account ID (acct_xxx)
    public string? PayoutMethodId { get; set; } // v2 payout method ID (pm_xxx)
    public string? Last4 { get; set; }
    public string? ErrorMessage { get; set; }
}

public class StripePayoutRequest
{
    public long AmountInPence { get; set; }
    public string Reference { get; set; } = "";
    public string SortCode { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";

    // Optional: If provided, we'll use them directly instead of creating new ones
    public string? RecipientAccountId { get; set; } // v2 recipient account ID (acct_xxx)
    public string? PayoutMethodId { get; set; } // v2 payout method ID (pm_xxx)
}

public class StripePayoutResult
{
    public bool Success { get; set; }
    public string? PayoutId { get; set; }
    public StripePayoutStatus Status { get; set; }
    public DateTime? ArrivalDate { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum StripePayoutStatus
{
    Pending,
    InTransit,
    Paid,
    Failed,
    Canceled
}

#endregion
