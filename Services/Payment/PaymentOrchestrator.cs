using inflan_api.DTOs;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.Utils;
using Microsoft.Extensions.Logging;

namespace inflan_api.Services.Payment;

public class PaymentOrchestrator : IPaymentOrchestrator
{
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly ITransactionRepository _transactionRepo;
    private readonly IPaymentMilestoneRepository _milestoneRepo;
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly IInfluencerPayoutRepository _payoutRepo;
    private readonly ICampaignRepository _campaignRepo;
    private readonly IUserRepository _userRepo;
    private readonly IPlatformSettingsService _settingsService;
    private readonly INotificationService _notificationService;
    private readonly IWithdrawalRepository _withdrawalRepo;
    private readonly IInfluencerBankAccountRepository _bankAccountRepo;
    private readonly PaystackGateway _paystackGateway;
    private readonly TrueLayerGateway _trueLayerGateway;
    private readonly ILogger<PaymentOrchestrator> _logger;

    public PaymentOrchestrator(
        IPaymentGatewayFactory gatewayFactory,
        ITransactionRepository transactionRepo,
        IPaymentMilestoneRepository milestoneRepo,
        IPaymentMethodRepository paymentMethodRepo,
        IInvoiceRepository invoiceRepo,
        IInfluencerPayoutRepository payoutRepo,
        ICampaignRepository campaignRepo,
        IUserRepository userRepo,
        IPlatformSettingsService settingsService,
        INotificationService notificationService,
        IWithdrawalRepository withdrawalRepo,
        IInfluencerBankAccountRepository bankAccountRepo,
        PaystackGateway paystackGateway,
        TrueLayerGateway trueLayerGateway,
        ILogger<PaymentOrchestrator> logger)
    {
        _gatewayFactory = gatewayFactory;
        _transactionRepo = transactionRepo;
        _milestoneRepo = milestoneRepo;
        _paymentMethodRepo = paymentMethodRepo;
        _invoiceRepo = invoiceRepo;
        _payoutRepo = payoutRepo;
        _campaignRepo = campaignRepo;
        _userRepo = userRepo;
        _settingsService = settingsService;
        _notificationService = notificationService;
        _withdrawalRepo = withdrawalRepo;
        _bankAccountRepo = bankAccountRepo;
        _paystackGateway = paystackGateway;
        _trueLayerGateway = trueLayerGateway;
        _logger = logger;
    }

    public async Task<PaymentInitiationResponse> InitiatePaymentAsync(InitiatePaymentRequest request)
    {
        try
        {
            var campaign = await _campaignRepo.GetById(request.CampaignId);
            if (campaign == null)
                return new PaymentInitiationResponse { Success = false, ErrorMessage = "Campaign not found" };

            var brand = await _userRepo.GetById(campaign.BrandId);
            if (brand == null)
                return new PaymentInitiationResponse { Success = false, ErrorMessage = "Brand not found" };

            // Ensure milestones exist for this campaign
            var existingMilestones = await _milestoneRepo.GetByCampaignIdAsync(request.CampaignId);
            if (existingMilestones == null || existingMilestones.Count == 0)
            {
                _logger.LogInformation("No milestones found for campaign {CampaignId}, they will be created on milestone fetch", request.CampaignId);
            }

            // Determine amount
            long amountInPence;
            PaymentMilestone? milestone = null;

            if (request.MilestoneId.HasValue)
            {
                milestone = await _milestoneRepo.GetByIdAsync(request.MilestoneId.Value);
                if (milestone == null)
                    return new PaymentInitiationResponse { Success = false, ErrorMessage = "Milestone not found" };

                if (milestone.Status != (int)MilestoneStatus.PENDING && milestone.Status != (int)MilestoneStatus.OVERDUE)
                    return new PaymentInitiationResponse { Success = false, ErrorMessage = "Milestone already paid or cancelled" };

                amountInPence = milestone.AmountInPence;
            }
            else if (request.AmountInPence.HasValue)
            {
                // Amount specified directly (used for one-time full payment or calculated milestone amount)
                amountInPence = request.AmountInPence.Value;
            }
            else
            {
                // Full payment attempt - check if any milestones have already been paid
                var milestones = await _milestoneRepo.GetByCampaignIdAsync(request.CampaignId);
                var hasPaidMilestones = milestones.Any(m => m.Status == (int)MilestoneStatus.PAID);

                if (hasPaidMilestones)
                {
                    return new PaymentInitiationResponse
                    {
                        Success = false,
                        ErrorMessage = "Full payment is not available because one or more milestones have already been paid. Please pay the remaining milestones individually."
                    };
                }

                // Full payment - use remaining balance or total if not set
                if (campaign.TotalAmountInPence > 0)
                {
                    amountInPence = campaign.TotalAmountInPence - campaign.PaidAmountInPence;
                }
                else
                {
                    // TotalAmountInPence not set yet, calculate from plan amount
                    amountInPence = (long)(campaign.Amount * 100); // Convert to pence
                }
            }

            // Add platform fee
            var feePercent = await _settingsService.GetBrandPlatformFeePercentAsync();
            var platformFee = (long)(amountInPence * feePercent / 100);
            var totalAmount = amountInPence + platformFee;

            // Generate transaction reference
            var transactionRef = $"INF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

            // Use brand's currency, defaulting to NGN if not set
            var currency = brand.Currency ?? CurrencyConstants.PrimaryCurrency;

            // Determine payment gateway based on currency
            // GBP uses TrueLayer, NGN uses Paystack
            var gatewayName = currency.ToUpper() switch
            {
                "GBP" => "truelayer",
                "NGN" => "paystack",
                _ => request.Gateway // Use requested gateway if currency doesn't match
            };

            // Override the requested gateway with the currency-determined one
            request.Gateway = gatewayName;

            _logger.LogInformation("Payment routing: Currency={Currency}, Gateway={Gateway}", currency, gatewayName);

            // Create transaction record
            var transaction = new Transaction
            {
                TransactionReference = transactionRef,
                UserId = request.UserId,
                CampaignId = request.CampaignId,
                MilestoneId = request.MilestoneId,
                AmountInPence = amountInPence,
                PlatformFeeInPence = platformFee,
                TotalAmountInPence = totalAmount,
                Currency = currency,
                Gateway = request.Gateway,
                TransactionStatus = (int)PaymentStatus.PENDING,
                CreatedAt = DateTime.UtcNow
            };

            transaction = await _transactionRepo.CreateTransactionAsync(transaction);

            // Get gateway and initiate payment
            var gateway = _gatewayFactory.GetGateway(request.Gateway);
            var initiationRequest = new PaymentInitiationRequest
            {
                TransactionReference = transactionRef,
                AmountInPence = totalAmount,
                Currency = currency,
                CustomerEmail = brand.Email ?? "",
                CustomerName = brand.BrandName ?? brand.Name ?? "",
                Description = $"Campaign: {campaign.ProjectName}",
                SuccessRedirectUrl = request.SuccessUrl,
                FailureRedirectUrl = request.FailureUrl,
                SavePaymentMethod = request.SavePaymentMethod,
                Metadata = new Dictionary<string, string>
                {
                    { "campaign_id", request.CampaignId.ToString() },
                    { "milestone_id", request.MilestoneId?.ToString() ?? "" },
                    { "user_id", request.UserId.ToString() }
                }
            };

            var result = await gateway.InitiatePaymentAsync(initiationRequest);

            if (result.Success)
            {
                // Update transaction with gateway details
                transaction.GatewayPaymentId = result.GatewayPaymentId;
                transaction.RedirectUrl = result.AuthorizationUrl;
                await _transactionRepo.UpdateTransactionAsync(transaction);

                _logger.LogInformation("Payment initiated: {TransactionRef} via {Gateway}",
                    transactionRef, request.Gateway);

                return new PaymentInitiationResponse
                {
                    Success = true,
                    RedirectUrl = result.AuthorizationUrl,
                    TransactionReference = transactionRef
                };
            }

            // Payment initiation failed
            transaction.TransactionStatus = (int)PaymentStatus.FAILED;
            transaction.FailureMessage = result.ErrorMessage;
            await _transactionRepo.UpdateTransactionAsync(transaction);

            return new PaymentInitiationResponse
            {
                Success = false,
                ErrorMessage = result.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating payment for campaign {CampaignId}", request.CampaignId);
            return new PaymentInitiationResponse
            {
                Success = false,
                ErrorMessage = "An error occurred while initiating payment"
            };
        }
    }

    public async Task<bool> ProcessWebhookAsync(string gateway, string payload, string? signature)
    {
        try
        {
            _logger.LogInformation("Processing webhook from gateway: {Gateway}", gateway);

            var paymentGateway = _gatewayFactory.GetGateway(gateway);
            var result = await paymentGateway.ProcessWebhookAsync(payload, signature);

            _logger.LogInformation("Gateway webhook result - Success: {Success}, Status: {Status}, Ref: {Ref}",
                result.Success, result.Status, result.TransactionReference);

            if (!result.Success)
            {
                _logger.LogWarning("Webhook processing failed: {Error}", result.ErrorMessage);
                return false;
            }

            // Handle transfer events for withdrawals
            if (result.IsTransferEvent)
            {
                return await HandleTransferWebhookAsync(result);
            }

            if (string.IsNullOrEmpty(result.TransactionReference))
            {
                _logger.LogWarning("Webhook missing transaction reference");
                return false;
            }

            // Find transaction
            _logger.LogInformation("Looking up transaction: {Ref}", result.TransactionReference);
            var transaction = await _transactionRepo.GetByTransactionReferenceAsync(result.TransactionReference);
            if (transaction == null)
            {
                _logger.LogWarning("Transaction not found: {Ref}", result.TransactionReference);
                return false;
            }

            _logger.LogInformation("Found transaction ID: {Id}, Current Status: {Status}", transaction.Id, transaction.TransactionStatus);

            // Check if already completed (idempotency check to prevent duplicate processing)
            if (transaction.TransactionStatus == (int)PaymentStatus.COMPLETED)
            {
                _logger.LogInformation("Transaction {Ref} already completed, skipping duplicate webhook", result.TransactionReference);
                return true; // Return success but don't process again
            }

            // Update transaction status
            transaction.TransactionStatus = result.Status switch
            {
                PaymentStatusType.Successful => (int)PaymentStatus.COMPLETED,
                PaymentStatusType.Failed => (int)PaymentStatus.FAILED,
                PaymentStatusType.Cancelled => (int)PaymentStatus.FAILED,
                PaymentStatusType.Abandoned => (int)PaymentStatus.FAILED,
                _ => (int)PaymentStatus.PENDING
            };
            transaction.WebhookPayload = payload;
            transaction.UpdatedAt = DateTime.UtcNow;

            if (result.Status == PaymentStatusType.Successful)
            {
                transaction.CompletedAt = DateTime.UtcNow;
                await HandleSuccessfulPayment(transaction, result);
            }
            else if (result.Status == PaymentStatusType.Failed)
            {
                transaction.FailureMessage = result.ErrorMessage;
            }

            await _transactionRepo.UpdateTransactionAsync(transaction);

            _logger.LogInformation("Webhook processed: {Ref} Status: {Status}",
                result.TransactionReference, result.Status);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook from {Gateway}", gateway);
            return false;
        }
    }

    private async Task HandleSuccessfulPayment(Transaction transaction, WebhookProcessResult result)
    {
        var campaign = await _campaignRepo.GetById(transaction.CampaignId);
        if (campaign == null) return;

        // Update campaign paid amount
        campaign.PaidAmountInPence += transaction.AmountInPence;

        // If milestone payment, mark specific milestone as paid
        if (transaction.MilestoneId.HasValue)
        {
            var milestone = await _milestoneRepo.GetByIdAsync(transaction.MilestoneId.Value);
            if (milestone != null)
            {
                milestone.Status = (int)MilestoneStatus.PAID;
                milestone.PaidAt = DateTime.UtcNow;
                milestone.TransactionId = transaction.Id;
                await _milestoneRepo.UpdateAsync(milestone);
            }
        }
        else
        {
            // Full payment - mark ALL pending milestones as paid
            var milestones = await _milestoneRepo.GetByCampaignIdAsync(transaction.CampaignId);
            foreach (var milestone in milestones)
            {
                if (milestone.Status == (int)MilestoneStatus.PENDING || milestone.Status == (int)MilestoneStatus.OVERDUE)
                {
                    milestone.Status = (int)MilestoneStatus.PAID;
                    milestone.PaidAt = DateTime.UtcNow;
                    milestone.TransactionId = transaction.Id;
                    await _milestoneRepo.UpdateAsync(milestone);
                }
            }
            _logger.LogInformation("Full payment: Marked all {Count} milestones as paid for campaign {CampaignId}",
                milestones.Count, transaction.CampaignId);
        }

        // Activate campaign after first payment (milestone or one-time)
        if (campaign.CampaignStatus == (int)CampaignStatus.AWAITING_PAYMENT)
        {
            campaign.CampaignStatus = (int)CampaignStatus.ACTIVE;
            _logger.LogInformation("Campaign {CampaignId} activated after payment", campaign.Id);
        }

        // Update payment status based on paid amount
        if (campaign.PaidAmountInPence >= campaign.TotalAmountInPence && campaign.TotalAmountInPence > 0)
        {
            // Fully paid
            campaign.PaymentStatus = (int)PaymentStatus.COMPLETED;
            campaign.PaymentCompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Campaign {CampaignId} fully paid", campaign.Id);
        }
        else if (campaign.PaidAmountInPence > 0)
        {
            // Partially paid (some milestones paid)
            campaign.PaymentStatus = (int)PaymentStatus.PARTIAL;
            _logger.LogInformation("Campaign {CampaignId} partially paid: {Paid}/{Total}",
                campaign.Id, campaign.PaidAmountInPence, campaign.TotalAmountInPence);
        }

        await _campaignRepo.Update(campaign);

        // Save payment method if card was saved (Paystack)
        if (!string.IsNullOrEmpty(result.AuthorizationCode) && result.Card != null)
        {
            var existingMethod = await _paymentMethodRepo.GetByAuthorizationCodeAsync(result.AuthorizationCode);
            if (existingMethod == null)
            {
                var paymentMethod = new PaymentMethod
                {
                    UserId = transaction.UserId,
                    Gateway = transaction.Gateway,
                    AuthorizationCode = result.AuthorizationCode,
                    CardType = result.Card.CardType,
                    Last4 = result.Card.Last4,
                    ExpiryMonth = result.Card.ExpiryMonth,
                    ExpiryYear = result.Card.ExpiryYear,
                    Bank = result.Card.Bank,
                    IsDefault = true
                };
                await _paymentMethodRepo.CreateAsync(paymentMethod);
            }
        }

        // Create invoice
        await CreateInvoiceForPayment(transaction, campaign);

        // Create payout record for influencer
        await CreatePayoutRecord(transaction, campaign);

        // Send notification to influencer about payment received
        await SendPaymentNotificationToInfluencer(transaction, campaign);
    }

    private async Task SendPaymentNotificationToInfluencer(Transaction transaction, Campaign campaign)
    {
        try
        {
            var amountFormatted = FormatAmount(transaction.AmountInPence, transaction.Currency);
            var brand = await _userRepo.GetById(campaign.BrandId);
            var brandName = brand?.BrandName ?? brand?.Name ?? "A brand";

            var milestoneInfo = "";
            if (transaction.MilestoneId.HasValue)
            {
                var milestone = await _milestoneRepo.GetByIdAsync(transaction.MilestoneId.Value);
                if (milestone != null)
                {
                    milestoneInfo = $" (Milestone {milestone.MilestoneNumber})";
                }
            }

            await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
            {
                UserId = campaign.InfluencerId,
                Type = NotificationType.Payment,
                Title = "Payment Received",
                Message = $"{brandName} has paid {amountFormatted}{milestoneInfo} for campaign \"{campaign.ProjectName}\". The payment is pending release.",
                ReferenceId = campaign.Id,
                ReferenceType = "campaign"
            });

            _logger.LogInformation("Payment notification sent to influencer {InfluencerId} for campaign {CampaignId}",
                campaign.InfluencerId, campaign.Id);
        }
        catch (Exception ex)
        {
            // Don't fail the payment flow if notification fails
            _logger.LogError(ex, "Failed to send payment notification to influencer {InfluencerId}", campaign.InfluencerId);
        }
    }

    private static string FormatAmount(long amountInPence, string currency)
    {
        var amount = amountInPence / 100.0m;
        return currency.ToUpper() switch
        {
            "GBP" => $"£{amount:N2}",
            "NGN" => $"₦{amount:N2}",
            "USD" => $"${amount:N2}",
            _ => $"{currency} {amount:N2}"
        };
    }

    private async Task CreateInvoiceForPayment(Transaction transaction, Campaign campaign)
    {
        var invoiceNumber = await _invoiceRepo.GenerateInvoiceNumberAsync();

        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            CampaignId = campaign.Id,
            BrandId = campaign.BrandId,
            InfluencerId = campaign.InfluencerId,
            MilestoneId = transaction.MilestoneId,
            TransactionId = transaction.Id,
            SubtotalInPence = transaction.AmountInPence,
            PlatformFeeInPence = transaction.PlatformFeeInPence,
            TotalAmountInPence = transaction.TotalAmountInPence,
            Currency = transaction.Currency,
            Status = (int)InvoiceStatus.PAID,
            IssuedAt = DateTime.UtcNow,
            PaidAt = DateTime.UtcNow
        };

        await _invoiceRepo.CreateAsync(invoice);
    }

    private async Task CreatePayoutRecord(Transaction transaction, Campaign campaign)
    {
        var influencerFeePercent = await _settingsService.GetInfluencerPlatformFeePercentAsync();
        var grossAmount = transaction.AmountInPence;
        var platformFee = (long)(grossAmount * influencerFeePercent / 100);
        var netAmount = grossAmount - platformFee;

        // Auto-release the payout when milestone is paid - funds immediately available for withdrawal
        var payout = new InfluencerPayout
        {
            CampaignId = campaign.Id,
            InfluencerId = campaign.InfluencerId,
            MilestoneId = transaction.MilestoneId,
            GrossAmountInPence = grossAmount,
            PlatformFeeInPence = platformFee,
            NetAmountInPence = netAmount,
            Currency = transaction.Currency,
            Status = (int)PayoutStatus.RELEASED, // Auto-release on payment
            ReleasedAt = DateTime.UtcNow
        };

        await _payoutRepo.CreateAsync(payout);

        // Update campaign released amount
        campaign.ReleasedToInfluencerInPence += netAmount;
        await _campaignRepo.Update(campaign);

        _logger.LogInformation("Payout auto-released: {Amount} pence to influencer {InfluencerId} for campaign {CampaignId}",
            netAmount, campaign.InfluencerId, campaign.Id);

        // Auto-initiate withdrawal to influencer's bank account
        await InitiateAutoWithdrawalAsync(campaign.InfluencerId, netAmount, transaction.Currency, payout.Id);
    }

    /// <summary>
    /// Automatically initiate withdrawal to influencer's bank account when brand pays
    /// </summary>
    private async Task InitiateAutoWithdrawalAsync(int influencerId, long amountInPence, string currency, int payoutId)
    {
        try
        {
            // Get influencer's default bank account for this currency
            var bankAccount = await _bankAccountRepo.GetDefaultByInfluencerIdAndCurrencyAsync(influencerId, currency);

            if (bankAccount == null)
            {
                _logger.LogWarning("Auto-withdrawal skipped: No bank account found for influencer {InfluencerId} with currency {Currency}. " +
                    "Funds are available for manual withdrawal once bank account is added.",
                    influencerId, currency);

                // Send notification to influencer to add bank account
                await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
                {
                    UserId = influencerId,
                    Type = NotificationType.Payment,
                    Title = "Add Bank Account to Receive Payment",
                    Message = $"You have received a payment of {FormatAmount(amountInPence, currency)}. " +
                        "Please add your bank account details to automatically receive funds.",
                    ReferenceType = "payout",
                    ReferenceId = payoutId
                });
                return;
            }

            // Create withdrawal record
            var withdrawal = new Withdrawal
            {
                InfluencerId = influencerId,
                AmountInPence = amountInPence,
                Currency = currency,
                PaymentGateway = bankAccount.PaymentGateway,
                BankName = bankAccount.BankName,
                BankCode = bankAccount.BankCode,
                AccountNumber = bankAccount.AccountNumberLast4,
                AccountName = bankAccount.AccountName,
                Status = (int)WithdrawalStatus.PROCESSING,
                CreatedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow
            };

            // Route to appropriate gateway
            if (currency == "GBP" && bankAccount.PaymentGateway == "truelayer")
            {
                await ProcessAutoWithdrawalTrueLayer(withdrawal, bankAccount);
            }
            else if (currency == "NGN" && bankAccount.PaymentGateway == "paystack")
            {
                await ProcessAutoWithdrawalPaystack(withdrawal, bankAccount);
            }
            else
            {
                _logger.LogWarning("Auto-withdrawal skipped: Unsupported currency/gateway combination - Currency: {Currency}, Gateway: {Gateway}",
                    currency, bankAccount.PaymentGateway);
                return;
            }

            await _withdrawalRepo.CreateAsync(withdrawal);

            // Send notification based on withdrawal status
            var formattedAmount = FormatAmount(amountInPence, currency);
            if (withdrawal.Status == (int)WithdrawalStatus.COMPLETED)
            {
                await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
                {
                    UserId = influencerId,
                    Type = NotificationType.Payment,
                    Title = "Payment Sent to Your Bank",
                    Message = $"{formattedAmount} has been sent to your bank account ({bankAccount.BankName} ****{bankAccount.AccountNumberLast4}).",
                    ReferenceId = withdrawal.Id,
                    ReferenceType = "withdrawal"
                });
            }
            else if (withdrawal.Status == (int)WithdrawalStatus.PROCESSING)
            {
                await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
                {
                    UserId = influencerId,
                    Type = NotificationType.Payment,
                    Title = "Payment Processing",
                    Message = $"{formattedAmount} is being transferred to your bank account. You'll be notified when complete.",
                    ReferenceId = withdrawal.Id,
                    ReferenceType = "withdrawal"
                });
            }
            else if (withdrawal.Status == (int)WithdrawalStatus.FAILED)
            {
                await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
                {
                    UserId = influencerId,
                    Type = NotificationType.Payment,
                    Title = "Withdrawal Failed",
                    Message = $"Failed to send {formattedAmount} to your bank. " +
                        $"Reason: {withdrawal.FailureReason ?? "Unknown error"}. Please check your bank details.",
                    ReferenceId = withdrawal.Id,
                    ReferenceType = "withdrawal"
                });
            }

            _logger.LogInformation("Auto-withdrawal initiated for influencer {InfluencerId}: {Amount} {Currency} via {Gateway}",
                influencerId, amountInPence, currency, bankAccount.PaymentGateway);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating auto-withdrawal for influencer {InfluencerId}", influencerId);
            // Don't fail the payment flow - influencer can still manually withdraw
        }
    }

    private async Task ProcessAutoWithdrawalPaystack(Withdrawal withdrawal, InfluencerBankAccount bankAccount)
    {
        if (string.IsNullOrEmpty(bankAccount.PaystackRecipientCode))
        {
            withdrawal.Status = (int)WithdrawalStatus.FAILED;
            withdrawal.FailureReason = "Bank account not configured for Paystack withdrawals";
            return;
        }

        var transferReference = $"AUTO-WD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var transferResult = await _paystackGateway.InitiateTransferAsync(new TransferRequest
        {
            RecipientCode = bankAccount.PaystackRecipientCode,
            AmountInPence = withdrawal.AmountInPence,
            Reason = $"Inflan auto-payout",
            Reference = transferReference,
            Currency = "NGN"
        });

        if (!transferResult.Success)
        {
            withdrawal.Status = (int)WithdrawalStatus.FAILED;
            withdrawal.FailureReason = transferResult.ErrorMessage ?? "Failed to initiate transfer";
            return;
        }

        withdrawal.PaystackTransferCode = transferResult.TransferCode;

        if (transferResult.Status == TransferStatus.Success)
        {
            withdrawal.Status = (int)WithdrawalStatus.COMPLETED;
            withdrawal.CompletedAt = DateTime.UtcNow;
        }
        else if (transferResult.Status == TransferStatus.Failed || transferResult.Status == TransferStatus.Reversed)
        {
            withdrawal.Status = (int)WithdrawalStatus.FAILED;
            withdrawal.FailureReason = transferResult.ErrorMessage ?? "Transfer failed";
        }
        // Otherwise keep as PROCESSING - webhook will update status
    }

    private async Task ProcessAutoWithdrawalTrueLayer(Withdrawal withdrawal, InfluencerBankAccount bankAccount)
    {
        // TrueLayer requires sort code and account number which we don't store (only last 4)
        // For TrueLayer auto-payouts, we need to use the beneficiary ID if available
        if (string.IsNullOrEmpty(bankAccount.TrueLayerBeneficiaryId))
        {
            // Without stored full bank details or beneficiary ID, we can't auto-withdraw for GBP
            // Mark as processing and the influencer will need to manually confirm
            withdrawal.Status = (int)WithdrawalStatus.PENDING;
            withdrawal.FailureReason = "GBP auto-withdrawal requires bank account verification. Please manually request withdrawal.";
            _logger.LogWarning("Auto-withdrawal for GBP skipped: No TrueLayer beneficiary ID for influencer bank account {BankAccountId}",
                bankAccount.Id);
            return;
        }

        var payoutReference = $"AUTO-TL-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var payoutResult = await _trueLayerGateway.InitiatePayoutAsync(new TrueLayerPayoutRequest
        {
            AmountInPence = withdrawal.AmountInPence,
            Reference = payoutReference,
            ExternalAccountId = bankAccount.TrueLayerBeneficiaryId
        });

        if (!payoutResult.Success)
        {
            withdrawal.Status = (int)WithdrawalStatus.FAILED;
            withdrawal.FailureReason = payoutResult.ErrorMessage ?? "Failed to initiate TrueLayer payout";
            return;
        }

        withdrawal.TrueLayerPayoutId = payoutResult.PayoutId;

        if (payoutResult.Status == TrueLayerPayoutStatus.Executed)
        {
            withdrawal.Status = (int)WithdrawalStatus.COMPLETED;
            withdrawal.CompletedAt = DateTime.UtcNow;
        }
        else if (payoutResult.Status == TrueLayerPayoutStatus.Failed)
        {
            withdrawal.Status = (int)WithdrawalStatus.FAILED;
            withdrawal.FailureReason = payoutResult.ErrorMessage ?? "TrueLayer payout failed";
        }
        // Otherwise keep as PROCESSING - webhook will update status
    }

    public async Task<PaymentInitiationResponse> ChargeRecurringPaymentAsync(int milestoneId, int paymentMethodId)
    {
        try
        {
            var milestone = await _milestoneRepo.GetByIdAsync(milestoneId);
            if (milestone == null)
                return new PaymentInitiationResponse { Success = false, ErrorMessage = "Milestone not found" };

            var paymentMethod = await _paymentMethodRepo.GetByIdAsync(paymentMethodId);
            if (paymentMethod == null || paymentMethod.Gateway != "paystack")
                return new PaymentInitiationResponse { Success = false, ErrorMessage = "Invalid payment method" };

            var campaign = await _campaignRepo.GetById(milestone.CampaignId);
            if (campaign == null)
                return new PaymentInitiationResponse { Success = false, ErrorMessage = "Campaign not found" };

            var brand = await _userRepo.GetById(campaign.BrandId);
            if (brand == null)
                return new PaymentInitiationResponse { Success = false, ErrorMessage = "Brand not found" };

            // Calculate amounts
            var feePercent = await _settingsService.GetBrandPlatformFeePercentAsync();
            var platformFee = (long)(milestone.AmountInPence * feePercent / 100);
            var totalAmount = milestone.AmountInPence + platformFee;

            // Generate transaction reference
            var transactionRef = $"INF-REC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

            // Create transaction record (Paystack uses NGN)
            var transaction = new Transaction
            {
                TransactionReference = transactionRef,
                UserId = campaign.BrandId,
                CampaignId = campaign.Id,
                MilestoneId = milestoneId,
                AmountInPence = milestone.AmountInPence,
                PlatformFeeInPence = platformFee,
                TotalAmountInPence = totalAmount,
                Currency = "NGN",
                Gateway = "paystack",
                PaymentMethodId = paymentMethodId,
                TransactionStatus = (int)PaymentStatus.PROCESSING,
                CreatedAt = DateTime.UtcNow
            };

            transaction = await _transactionRepo.CreateTransactionAsync(transaction);

            // Charge authorization
            var gateway = _gatewayFactory.GetGateway("paystack");
            var chargeRequest = new ChargeAuthorizationRequest
            {
                AuthorizationCode = paymentMethod.AuthorizationCode,
                Email = paymentMethod.Email ?? brand.Email ?? "",
                AmountInPence = totalAmount,
                Currency = "NGN",
                TransactionReference = transactionRef
            };

            var result = await gateway.ChargeAuthorizationAsync(chargeRequest);

            if (result.Success && result.Status == PaymentStatusType.Successful)
            {
                transaction.TransactionStatus = (int)PaymentStatus.COMPLETED;
                transaction.CompletedAt = DateTime.UtcNow;
                transaction.GatewayTransactionId = result.GatewayReference;

                // Handle successful payment
                var webhookResult = new WebhookProcessResult
                {
                    Success = true,
                    Status = PaymentStatusType.Successful,
                    TransactionReference = transactionRef
                };
                await HandleSuccessfulPayment(transaction, webhookResult);

                await _transactionRepo.UpdateTransactionAsync(transaction);

                return new PaymentInitiationResponse
                {
                    Success = true,
                    TransactionReference = transactionRef
                };
            }

            // Payment failed
            transaction.TransactionStatus = (int)PaymentStatus.FAILED;
            transaction.FailureMessage = result.ErrorMessage;
            await _transactionRepo.UpdateTransactionAsync(transaction);

            return new PaymentInitiationResponse
            {
                Success = false,
                ErrorMessage = result.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error charging recurring payment for milestone {MilestoneId}", milestoneId);
            return new PaymentInitiationResponse
            {
                Success = false,
                ErrorMessage = "An error occurred while processing recurring payment"
            };
        }
    }

    public async Task<Transaction?> GetPaymentStatusAsync(string transactionReference)
    {
        return await _transactionRepo.GetByTransactionReferenceAsync(transactionReference);
    }

    public async Task<InfluencerPayout> ReleasePaymentToInfluencerAsync(int payoutId, int brandUserId)
    {
        var payout = await _payoutRepo.GetByIdAsync(payoutId);
        if (payout == null)
            throw new ArgumentException("Payout not found");

        var campaign = await _campaignRepo.GetById(payout.CampaignId);
        if (campaign == null || campaign.BrandId != brandUserId)
            throw new UnauthorizedAccessException("Not authorized to release this payment");

        if (payout.Status != (int)PayoutStatus.PENDING_RELEASE)
            throw new InvalidOperationException("Payout is not in pending release status");

        payout.Status = (int)PayoutStatus.RELEASED;
        payout.ReleasedAt = DateTime.UtcNow;

        // Update campaign released amount
        campaign.ReleasedToInfluencerInPence += payout.NetAmountInPence;
        await _campaignRepo.Update(campaign);

        var updated = await _payoutRepo.UpdateAsync(payout);

        _logger.LogInformation("Payment released to influencer: Payout {PayoutId}, Amount {Amount}",
            payoutId, payout.NetAmountInPence);

        return updated;
    }

    public async Task<CampaignPaymentSummary> GetCampaignPaymentSummaryAsync(int campaignId)
    {
        var campaign = await _campaignRepo.GetById(campaignId);
        if (campaign == null)
            throw new ArgumentException("Campaign not found");

        var milestones = await _milestoneRepo.GetByCampaignIdAsync(campaignId);
        var payouts = await _payoutRepo.GetByCampaignIdAsync(campaignId);

        var pendingReleaseAmount = payouts
            .Where(p => p.Status == (int)PayoutStatus.PENDING_RELEASE)
            .Sum(p => p.NetAmountInPence);

        var nextDueMilestone = milestones
            .Where(m => m.Status == (int)MilestoneStatus.PENDING || m.Status == (int)MilestoneStatus.OVERDUE)
            .OrderBy(m => m.DueDate)
            .FirstOrDefault();

        return new CampaignPaymentSummary
        {
            CampaignId = campaignId,
            TotalAmountInPence = campaign.TotalAmountInPence,
            PaidAmountInPence = campaign.PaidAmountInPence,
            OutstandingAmountInPence = campaign.TotalAmountInPence - campaign.PaidAmountInPence,
            ReleasedToInfluencerInPence = campaign.ReleasedToInfluencerInPence,
            PendingReleaseInPence = pendingReleaseAmount,
            TotalMilestones = milestones.Count,
            PaidMilestones = milestones.Count(m => m.Status == (int)MilestoneStatus.PAID),
            PendingMilestones = milestones.Count(m => m.Status == (int)MilestoneStatus.PENDING || m.Status == (int)MilestoneStatus.OVERDUE),
            NextDueMilestone = nextDueMilestone
        };
    }

    public async Task<long> GetBrandOutstandingBalanceAsync(int brandId)
    {
        // Only return OVERDUE milestones as "outstanding" - not future scheduled payments
        var overdueMilestones = await _milestoneRepo.GetOverdueMilestonesAsync();

        // Filter to only this brand's milestones
        var brandOverdueMilestones = overdueMilestones
            .Where(m => m.Campaign?.BrandId == brandId)
            .ToList();

        return brandOverdueMilestones.Sum(m => m.AmountInPence + m.PlatformFeeInPence);
    }

    public async Task<BrandOutstandingBalanceDto> GetBrandOutstandingBalanceDetailedAsync(int brandId)
    {
        var campaigns = await _campaignRepo.GetCampaignsByBrandId(brandId);
        var activeCampaigns = campaigns.Where(c => c.CampaignStatus != (int)CampaignStatus.CANCELLED).ToList();

        // Get all overdue milestones for this brand
        var overdueMilestones = await _milestoneRepo.GetOverdueMilestonesAsync();
        var brandOverdueMilestones = overdueMilestones
            .Where(m => m.Campaign?.BrandId == brandId)
            .ToList();

        var overdueAmount = brandOverdueMilestones.Sum(m => m.AmountInPence + m.PlatformFeeInPence);
        var totalRemaining = activeCampaigns.Sum(c => c.TotalAmountInPence - c.PaidAmountInPence);
        var totalPaid = activeCampaigns.Sum(c => c.PaidAmountInPence);

        return new BrandOutstandingBalanceDto
        {
            OverdueAmountInPence = overdueAmount,
            TotalRemainingInPence = totalRemaining,
            TotalPaidInPence = totalPaid,
            HasOverdueMilestones = brandOverdueMilestones.Count > 0,
            OverdueMilestoneCount = brandOverdueMilestones.Count
        };
    }

    public async Task<PaymentVerificationResult> VerifyAndProcessPaymentAsync(string transactionReference)
    {
        try
        {
            // Find transaction
            var transaction = await _transactionRepo.GetByTransactionReferenceAsync(transactionReference);
            if (transaction == null)
            {
                return new PaymentVerificationResult
                {
                    Success = false,
                    ErrorMessage = "Transaction not found"
                };
            }

            // Already completed?
            if (transaction.TransactionStatus == (int)PaymentStatus.COMPLETED)
            {
                return new PaymentVerificationResult
                {
                    Success = true,
                    Status = "already_completed"
                };
            }

            // Get gateway and verify payment status
            // For TrueLayer, we need to use the GatewayPaymentId, for Paystack we use the transaction reference
            var gateway = _gatewayFactory.GetGateway(transaction.Gateway);
            var gatewayReference = transaction.Gateway.ToLower() == "truelayer"
                ? transaction.GatewayPaymentId ?? transactionReference
                : transactionReference;
            var statusResult = await gateway.GetPaymentStatusAsync(gatewayReference);

            if (!statusResult.Success)
            {
                return new PaymentVerificationResult
                {
                    Success = false,
                    ErrorMessage = statusResult.ErrorMessage ?? "Failed to verify payment with gateway"
                };
            }

            // Update transaction based on gateway response
            if (statusResult.Status == PaymentStatusType.Successful)
            {
                transaction.TransactionStatus = (int)PaymentStatus.COMPLETED;
                transaction.CompletedAt = DateTime.UtcNow;
                transaction.UpdatedAt = DateTime.UtcNow;

                // Create webhook-like result to process successful payment
                var webhookResult = new WebhookProcessResult
                {
                    Success = true,
                    Status = PaymentStatusType.Successful,
                    TransactionReference = transactionReference,
                    AuthorizationCode = statusResult.AuthorizationCode,
                    Card = statusResult.Card
                };

                await HandleSuccessfulPayment(transaction, webhookResult);
                await _transactionRepo.UpdateTransactionAsync(transaction);

                _logger.LogInformation("Payment verified and processed: {TransactionRef}", transactionReference);

                return new PaymentVerificationResult
                {
                    Success = true,
                    Status = "success"
                };
            }
            else if (statusResult.Status == PaymentStatusType.Failed ||
                     statusResult.Status == PaymentStatusType.Abandoned ||
                     statusResult.Status == PaymentStatusType.Cancelled)
            {
                transaction.TransactionStatus = (int)PaymentStatus.FAILED;
                transaction.FailureMessage = statusResult.ErrorMessage ?? "Payment failed";
                transaction.UpdatedAt = DateTime.UtcNow;
                await _transactionRepo.UpdateTransactionAsync(transaction);

                return new PaymentVerificationResult
                {
                    Success = false,
                    Status = statusResult.Status.ToString().ToLower(),
                    ErrorMessage = statusResult.ErrorMessage ?? "Payment was not successful"
                };
            }

            // Still pending
            return new PaymentVerificationResult
            {
                Success = true,
                Status = "pending"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment {TransactionRef}", transactionReference);
            return new PaymentVerificationResult
            {
                Success = false,
                ErrorMessage = "An error occurred while verifying payment"
            };
        }
    }

    private async Task<bool> HandleTransferWebhookAsync(WebhookProcessResult result)
    {
        try
        {
            _logger.LogInformation("Processing transfer webhook - TransferCode: {TransferCode}, Status: {Status}",
                result.TransferCode, result.Status);

            // Find withdrawal by transfer code
            Withdrawal? withdrawal = null;
            if (!string.IsNullOrEmpty(result.TransferCode))
            {
                withdrawal = await _withdrawalRepo.GetByTransferCodeAsync(result.TransferCode);
            }

            if (withdrawal == null)
            {
                _logger.LogWarning("Withdrawal not found for transfer code: {TransferCode}", result.TransferCode);
                return false;
            }

            var previousStatus = withdrawal.Status;

            // Update withdrawal status based on transfer result
            if (result.Status == PaymentStatusType.Successful)
            {
                withdrawal.Status = (int)WithdrawalStatus.COMPLETED;
                withdrawal.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("Withdrawal {WithdrawalId} completed via webhook", withdrawal.Id);
            }
            else if (result.Status == PaymentStatusType.Failed)
            {
                withdrawal.Status = (int)WithdrawalStatus.FAILED;
                withdrawal.FailureReason = result.ErrorMessage ?? "Transfer failed";
                _logger.LogWarning("Withdrawal {WithdrawalId} failed via webhook: {Error}",
                    withdrawal.Id, result.ErrorMessage);
            }

            await _withdrawalRepo.UpdateAsync(withdrawal);

            // Send notification if status changed
            if (previousStatus != withdrawal.Status)
            {
                var formattedAmount = FormatAmount(withdrawal.AmountInPence, withdrawal.Currency);

                if (withdrawal.Status == (int)WithdrawalStatus.COMPLETED)
                {
                    await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
                    {
                        UserId = withdrawal.InfluencerId,
                        Type = NotificationType.Payment,
                        Title = "Withdrawal Successful",
                        Message = $"Your withdrawal of {formattedAmount} has been successfully transferred to your bank account.",
                        ReferenceId = withdrawal.Id,
                        ReferenceType = "withdrawal"
                    });
                }
                else if (withdrawal.Status == (int)WithdrawalStatus.FAILED)
                {
                    await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
                    {
                        UserId = withdrawal.InfluencerId,
                        Type = NotificationType.Payment,
                        Title = "Withdrawal Failed",
                        Message = $"Your withdrawal of {formattedAmount} could not be completed. Reason: {withdrawal.FailureReason}. The funds are back in your available balance.",
                        ReferenceId = withdrawal.Id,
                        ReferenceType = "withdrawal"
                    });
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling transfer webhook");
            return false;
        }
    }

    public async Task<Transaction?> GetPaymentByGatewayIdAsync(string gatewayPaymentId)
    {
        return await _transactionRepo.GetByGatewayPaymentIdAsync(gatewayPaymentId);
    }

    public async Task<PaymentVerificationResult> VerifyPaymentByGatewayIdAsync(string gatewayPaymentId)
    {
        try
        {
            // Find transaction by gateway payment ID
            var transaction = await _transactionRepo.GetByGatewayPaymentIdAsync(gatewayPaymentId);
            if (transaction == null)
            {
                return new PaymentVerificationResult
                {
                    Success = false,
                    ErrorMessage = "Transaction not found for this payment ID"
                };
            }

            // Already completed?
            if (transaction.TransactionStatus == (int)PaymentStatus.COMPLETED)
            {
                return new PaymentVerificationResult
                {
                    Success = true,
                    Status = "success",
                    TransactionReference = transaction.TransactionReference
                };
            }

            // Get gateway and verify payment status
            var gateway = _gatewayFactory.GetGateway(transaction.Gateway);
            var statusResult = await gateway.GetPaymentStatusAsync(gatewayPaymentId);

            _logger.LogInformation("Gateway status for {GatewayPaymentId}: Success={Success}, Status={Status}",
                gatewayPaymentId, statusResult.Success, statusResult.Status);

            if (!statusResult.Success)
            {
                return new PaymentVerificationResult
                {
                    Success = false,
                    ErrorMessage = statusResult.ErrorMessage ?? "Failed to verify payment with gateway",
                    TransactionReference = transaction.TransactionReference
                };
            }

            // Update transaction based on gateway response
            if (statusResult.Status == PaymentStatusType.Successful)
            {
                transaction.TransactionStatus = (int)PaymentStatus.COMPLETED;
                transaction.CompletedAt = DateTime.UtcNow;
                transaction.UpdatedAt = DateTime.UtcNow;

                // Create webhook-like result to process successful payment
                var webhookResult = new WebhookProcessResult
                {
                    Success = true,
                    Status = PaymentStatusType.Successful,
                    TransactionReference = transaction.TransactionReference,
                    AuthorizationCode = statusResult.AuthorizationCode,
                    Card = statusResult.Card
                };

                await HandleSuccessfulPayment(transaction, webhookResult);
                await _transactionRepo.UpdateTransactionAsync(transaction);

                _logger.LogInformation("Payment verified and processed by gateway ID: {GatewayPaymentId}", gatewayPaymentId);

                return new PaymentVerificationResult
                {
                    Success = true,
                    Status = "success",
                    TransactionReference = transaction.TransactionReference
                };
            }
            else if (statusResult.Status == PaymentStatusType.Failed ||
                     statusResult.Status == PaymentStatusType.Abandoned ||
                     statusResult.Status == PaymentStatusType.Cancelled)
            {
                transaction.TransactionStatus = (int)PaymentStatus.FAILED;
                transaction.FailureMessage = statusResult.ErrorMessage ?? "Payment failed";
                transaction.UpdatedAt = DateTime.UtcNow;
                await _transactionRepo.UpdateTransactionAsync(transaction);

                return new PaymentVerificationResult
                {
                    Success = false,
                    Status = statusResult.Status.ToString().ToLower(),
                    ErrorMessage = statusResult.ErrorMessage ?? "Payment was not successful",
                    TransactionReference = transaction.TransactionReference
                };
            }

            // Still pending
            return new PaymentVerificationResult
            {
                Success = true,
                Status = "pending",
                TransactionReference = transaction.TransactionReference
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment by gateway ID {GatewayPaymentId}", gatewayPaymentId);
            return new PaymentVerificationResult
            {
                Success = false,
                ErrorMessage = "An error occurred while verifying payment"
            };
        }
    }
}
