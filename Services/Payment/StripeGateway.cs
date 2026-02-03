using System.Text.Json;
using inflan_api.Interfaces;
using inflan_api.Utils;
using Stripe;
using Stripe.Checkout;

namespace inflan_api.Services.Payment;

/// <summary>
/// Stripe payment gateway implementation for UK (GBP) payments
/// Supports one-time payments, card saving, and recurring charges
/// </summary>
public class StripeGateway : IPaymentGateway
{
    private readonly ILogger<StripeGateway> _logger;
    private readonly string _secretKey;
    private readonly string _publishableKey;
    private readonly string _webhookSecret;
    private readonly string _successUrl;
    private readonly string _cancelUrl;
    private readonly bool _enabled;

    public string GatewayName => "stripe";

    public StripeGateway(IConfiguration configuration, ILogger<StripeGateway> logger)
    {
        _logger = logger;
        _secretKey = configuration["Stripe:SecretKey"] ?? throw new ArgumentNullException("Stripe:SecretKey not configured");
        _publishableKey = configuration["Stripe:PublishableKey"] ?? throw new ArgumentNullException("Stripe:PublishableKey not configured");
        _webhookSecret = configuration["Stripe:WebhookSecret"] ?? "";
        _successUrl = configuration["Stripe:SuccessUrl"] ?? "https://dev.inflan.com/payment/callback?status=success";
        _cancelUrl = configuration["Stripe:CancelUrl"] ?? "https://dev.inflan.com/payment/callback?status=cancel";
        _enabled = configuration.GetValue<bool>("Stripe:Enabled", true);

        if (!_enabled)
        {
            _logger.LogWarning("Stripe gateway is disabled in configuration");
        }

        // Set Stripe API key
        StripeConfiguration.ApiKey = _secretKey;
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
}
