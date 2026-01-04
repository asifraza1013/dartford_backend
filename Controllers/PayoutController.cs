using inflan_api.DTOs;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.Services;
using inflan_api.Services.Payment;
using inflan_api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace inflan_api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PayoutController : ControllerBase
{
    private readonly IInfluencerPayoutRepository _payoutRepo;
    private readonly IWithdrawalRepository _withdrawalRepo;
    private readonly IPaymentMilestoneRepository _milestoneRepo;
    private readonly ICampaignRepository _campaignRepo;
    private readonly IPlatformSettingsService _settingsService;
    private readonly PaystackGateway _paystackGateway;
    private readonly TrueLayerGateway _trueLayerGateway;
    private readonly IUserService _userService;
    private readonly IInfluencerBankAccountRepository _bankAccountRepo;
    private readonly INotificationService _notificationService;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<PayoutController> _logger;

    public PayoutController(
        IInfluencerPayoutRepository payoutRepo,
        IWithdrawalRepository withdrawalRepo,
        IPaymentMilestoneRepository milestoneRepo,
        ICampaignRepository campaignRepo,
        IPlatformSettingsService settingsService,
        PaystackGateway paystackGateway,
        TrueLayerGateway trueLayerGateway,
        IUserService userService,
        IInfluencerBankAccountRepository bankAccountRepo,
        INotificationService notificationService,
        IUserRepository userRepo,
        ILogger<PayoutController> logger)
    {
        _payoutRepo = payoutRepo;
        _withdrawalRepo = withdrawalRepo;
        _milestoneRepo = milestoneRepo;
        _campaignRepo = campaignRepo;
        _settingsService = settingsService;
        _paystackGateway = paystackGateway;
        _trueLayerGateway = trueLayerGateway;
        _userService = userService;
        _bankAccountRepo = bankAccountRepo;
        _notificationService = notificationService;
        _userRepo = userRepo;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("id")?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    /// <summary>
    /// Get pending payouts for influencer
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingPayouts()
    {
        var userId = GetCurrentUserId();
        var payouts = await _payoutRepo.GetPendingByInfluencerIdAsync(userId);

        return Ok(payouts.Select(p => new
        {
            p.Id,
            p.CampaignId,
            campaignName = p.Campaign?.ProjectName,
            p.MilestoneId,
            milestoneNumber = p.Milestone?.MilestoneNumber,
            p.GrossAmountInPence,
            p.PlatformFeeInPence,
            p.NetAmountInPence,
            p.Currency,
            p.Status,
            statusText = GetStatusText(p.Status),
            p.CreatedAt
        }));
    }

    /// <summary>
    /// Get payout history for influencer with optional filters
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetPayoutHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] long? minAmount = null,
        [FromQuery] long? maxAmount = null,
        [FromQuery] int? campaignId = null,
        [FromQuery] int? status = null)
    {
        var userId = GetCurrentUserId();
        var filter = new PaymentFilterDto
        {
            Page = page,
            PageSize = pageSize,
            DateFrom = dateFrom,
            DateTo = dateTo,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            CampaignId = campaignId,
            Status = status
        };

        var (payouts, totalCount) = await _payoutRepo.GetByInfluencerIdFilteredAsync(userId, filter);

        return Ok(new
        {
            items = payouts.Select(p => new
            {
                p.Id,
                p.CampaignId,
                campaignName = p.Campaign?.ProjectName,
                p.MilestoneId,
                milestoneNumber = p.Milestone?.MilestoneNumber,
                p.GrossAmountInPence,
                p.PlatformFeeInPence,
                p.NetAmountInPence,
                p.Currency,
                p.Status,
                statusText = GetStatusText(p.Status),
                p.ReleasedAt,
                p.PaidAt,
                p.CreatedAt
            }),
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Get total pending balance for influencer (sum of unpaid milestones that brand still needs to pay)
    /// </summary>
    [HttpGet("balance/pending")]
    public async Task<IActionResult> GetPendingBalance()
    {
        var userId = GetCurrentUserId();

        // Get user's currency based on location
        var (userCurrency, _) = await GetUserCurrencyAndGatewayAsync(userId);

        // Get all unpaid milestones for this influencer's campaigns
        var unpaidMilestones = await _milestoneRepo.GetUpcomingByInfluencerIdAsync(userId);

        // Filter milestones by the user's currency (based on campaign/transaction currency)
        var filteredMilestones = unpaidMilestones.Where(m =>
        {
            // Get the campaign's currency (from the brand's location or transaction)
            var campaignCurrency = m.Campaign?.Currency ?? userCurrency;
            return campaignCurrency == userCurrency;
        }).ToList();

        var totalPending = filteredMilestones.Sum(m => m.AmountInPence);

        return Ok(new
        {
            pendingAmountInPence = totalPending,
            currency = userCurrency
        });
    }

    /// <summary>
    /// Get total released balance for influencer (available for withdrawal)
    /// </summary>
    [HttpGet("balance/released")]
    public async Task<IActionResult> GetReleasedBalance()
    {
        var userId = GetCurrentUserId();

        // Get user's currency based on location
        var (userCurrency, _) = await GetUserCurrencyAndGatewayAsync(userId);

        // Filter by currency
        var totalReleased = await _payoutRepo.GetTotalReleasedByInfluencerIdAsync(userId, userCurrency);

        return Ok(new
        {
            releasedAmountInPence = totalReleased,
            currency = userCurrency
        });
    }

    /// <summary>
    /// Get payouts for a campaign
    /// </summary>
    [HttpGet("campaign/{campaignId}")]
    public async Task<IActionResult> GetCampaignPayouts(int campaignId)
    {
        var payouts = await _payoutRepo.GetByCampaignIdAsync(campaignId);

        return Ok(payouts.Select(p => new
        {
            p.Id,
            p.MilestoneId,
            milestoneNumber = p.Milestone?.MilestoneNumber,
            p.GrossAmountInPence,
            p.PlatformFeeInPence,
            p.NetAmountInPence,
            p.Currency,
            p.Status,
            statusText = GetStatusText(p.Status),
            p.ReleasedAt,
            p.PaidAt,
            p.CreatedAt
        }));
    }

    /// <summary>
    /// Get platform fee percentage for influencers
    /// </summary>
    [HttpGet("fee-percentage")]
    public async Task<IActionResult> GetFeePercentage()
    {
        var feePercent = await _settingsService.GetInfluencerPlatformFeePercentAsync();
        return Ok(new { feePercentage = feePercent });
    }

    /// <summary>
    /// Get upcoming milestones for influencer (payments the brand still needs to pay)
    /// </summary>
    [HttpGet("upcoming")]
    public async Task<IActionResult> GetUpcomingMilestones()
    {
        var userId = GetCurrentUserId();
        var milestones = await _milestoneRepo.GetUpcomingByInfluencerIdAsync(userId);

        // Get user's currency based on location
        var (userCurrency, _) = await GetUserCurrencyAndGatewayAsync(userId);

        return Ok(milestones.Select(m => new
        {
            m.Id,
            m.CampaignId,
            campaignName = m.Campaign?.ProjectName,
            brandName = m.Campaign?.Brand?.BrandName ?? m.Campaign?.Brand?.Name,
            m.MilestoneNumber,
            m.AmountInPence,
            m.PlatformFeeInPence,
            currency = m.Campaign?.Currency ?? userCurrency,
            m.DueDate,
            m.Status,
            statusText = GetMilestoneStatusText(m.Status),
            isOverdue = m.DueDate < DateTime.UtcNow && m.Status == (int)MilestoneStatus.PENDING
        }));
    }

    /// <summary>
    /// Get brands that have campaigns with this influencer (for filtering)
    /// </summary>
    [HttpGet("filter-options/brands")]
    public async Task<IActionResult> GetFilterBrands()
    {
        var userId = GetCurrentUserId();
        var campaigns = await _campaignRepo.GetCampaignsByInfluencerId(userId);

        var brands = campaigns
            .Where(c => c.Brand != null)
            .Select(c => new
            {
                id = c.BrandId,
                name = c.Brand?.BrandName ?? c.Brand?.Name ?? "Unknown Brand"
            })
            .DistinctBy(b => b.id)
            .OrderBy(b => b.name)
            .ToList();

        return Ok(brands);
    }

    /// <summary>
    /// Get campaigns for this influencer (for filtering)
    /// </summary>
    [HttpGet("filter-options/campaigns")]
    public async Task<IActionResult> GetFilterCampaigns([FromQuery] int? brandId = null)
    {
        var userId = GetCurrentUserId();
        var campaigns = await _campaignRepo.GetCampaignsByInfluencerId(userId);

        if (brandId.HasValue)
        {
            campaigns = campaigns.Where(c => c.BrandId == brandId.Value).ToList();
        }

        var result = campaigns
            .Select(c => new
            {
                id = c.Id,
                name = c.ProjectName,
                brandId = c.BrandId,
                brandName = c.Brand?.BrandName ?? c.Brand?.Name ?? "Unknown Brand"
            })
            .OrderBy(c => c.name)
            .ToList();

        return Ok(result);
    }

    private static string GetMilestoneStatusText(int status)
    {
        return status switch
        {
            (int)MilestoneStatus.PENDING => "Pending",
            (int)MilestoneStatus.PAID => "Paid",
            (int)MilestoneStatus.OVERDUE => "Overdue",
            (int)MilestoneStatus.CANCELLED => "Cancelled",
            _ => "Unknown"
        };
    }

    private static string GetStatusText(int status)
    {
        return status switch
        {
            (int)PayoutStatus.PENDING_RELEASE => "Pending Release",
            (int)PayoutStatus.RELEASED => "Released",
            (int)PayoutStatus.PROCESSING => "Processing",
            (int)PayoutStatus.PAID => "Paid",
            (int)PayoutStatus.FAILED => "Failed",
            _ => "Unknown"
        };
    }

    #region Withdrawal Endpoints

    /// <summary>
    /// Get user's currency and gateway based on their location
    /// </summary>
    private async Task<(string currency, string gateway)> GetUserCurrencyAndGatewayAsync(int userId)
    {
        var user = await _userRepo.GetById(userId);
        var location = user?.Location?.ToUpperInvariant() ?? "NG";

        _logger.LogInformation("GetUserCurrencyAndGatewayAsync: UserId={UserId}, UserLocation={Location}, ResolvedLocation={ResolvedLocation}",
            userId, user?.Location, location);

        return location switch
        {
            "GB" => ("GBP", "truelayer"),
            _ => ("NGN", "paystack")
        };
    }

    /// <summary>
    /// Get available balance for withdrawal (released - already withdrawn)
    /// </summary>
    [HttpGet("withdraw/available")]
    public async Task<IActionResult> GetAvailableForWithdrawal()
    {
        var userId = GetCurrentUserId();

        // Get user's currency based on location
        var (userCurrency, _) = await GetUserCurrencyAndGatewayAsync(userId);

        // Filter by currency to ensure correct balance calculation
        var totalReleased = await _payoutRepo.GetTotalReleasedByInfluencerIdAsync(userId, userCurrency);
        var totalWithdrawn = await _withdrawalRepo.GetTotalWithdrawnByInfluencerIdAsync(userId, userCurrency);
        var available = totalReleased - totalWithdrawn;

        return Ok(new
        {
            releasedAmountInPence = totalReleased,
            withdrawnAmountInPence = totalWithdrawn,
            availableAmountInPence = available > 0 ? available : 0,
            currency = userCurrency
        });
    }

    /// <summary>
    /// Request a withdrawal - processed via Paystack (NGN) or TrueLayer (GBP)
    /// </summary>
    [HttpPost("withdraw/request")]
    public async Task<IActionResult> RequestWithdrawal([FromBody] WithdrawalRequestDto request)
    {
        var userId = GetCurrentUserId();

        // Verify password first
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Password is required to confirm withdrawal" });
        }

        var isPasswordValid = await _userService.VerifyPasswordAsync(userId, request.Password);
        if (!isPasswordValid)
        {
            return BadRequest(new { message = "Invalid password" });
        }

        // Validate amount
        if (request.AmountInPence <= 0)
        {
            return BadRequest(new { message = "Withdrawal amount must be greater than 0" });
        }

        // Get user's currency and gateway based on location
        var (userCurrency, gateway) = await GetUserCurrencyAndGatewayAsync(userId);

        // Check available balance (filtered by currency)
        var totalReleased = await _payoutRepo.GetTotalReleasedByInfluencerIdAsync(userId, userCurrency);
        var totalWithdrawn = await _withdrawalRepo.GetTotalWithdrawnByInfluencerIdAsync(userId, userCurrency);
        var available = totalReleased - totalWithdrawn;

        if (request.AmountInPence > available)
        {
            return BadRequest(new { message = "Insufficient balance for withdrawal" });
        }

        // Route to appropriate gateway based on user's currency
        if (gateway == "truelayer")
        {
            return await ProcessTrueLayerWithdrawal(userId, request, userCurrency);
        }
        else
        {
            return await ProcessPaystackWithdrawal(userId, request, userCurrency);
        }
    }

    /// <summary>
    /// Process withdrawal via Paystack (for Nigerian users - NGN)
    /// </summary>
    private async Task<IActionResult> ProcessPaystackWithdrawal(int userId, WithdrawalRequestDto request, string currency)
    {
        string recipientCode;
        string bankName;
        string bankCode;
        string accountNumberLast4;
        string accountName;

        // Check if using saved bank account or one-time details
        if (request.BankAccountId.HasValue)
        {
            // Use saved bank account
            var savedAccount = await _bankAccountRepo.GetByIdAsync(request.BankAccountId.Value);
            if (savedAccount == null)
            {
                return BadRequest(new { message = "Bank account not found" });
            }

            if (savedAccount.InfluencerId != userId)
            {
                return Forbid();
            }

            if (string.IsNullOrEmpty(savedAccount.PaystackRecipientCode))
            {
                return BadRequest(new { message = "This bank account is not configured for Paystack withdrawals" });
            }

            recipientCode = savedAccount.PaystackRecipientCode;
            bankName = savedAccount.BankName;
            bankCode = savedAccount.BankCode;
            accountNumberLast4 = savedAccount.AccountNumberLast4;
            accountName = savedAccount.AccountName;
        }
        else
        {
            // Validate required bank details for one-time use
            if (string.IsNullOrWhiteSpace(request.BankCode))
            {
                return BadRequest(new { message = "Bank code is required" });
            }

            if (string.IsNullOrWhiteSpace(request.AccountNumber))
            {
                return BadRequest(new { message = "Account number is required" });
            }

            if (string.IsNullOrWhiteSpace(request.AccountName))
            {
                return BadRequest(new { message = "Account name is required" });
            }

            // Create transfer recipient in Paystack for one-time use
            var recipientResult = await _paystackGateway.CreateTransferRecipientAsync(new TransferRecipientRequest
            {
                AccountName = request.AccountName!,
                AccountNumber = request.AccountNumber!,
                BankCode = request.BankCode!,
                Currency = currency
            });

            if (!recipientResult.Success)
            {
                _logger.LogError("Failed to create Paystack recipient for user {UserId}: {Error}",
                    userId, recipientResult.ErrorMessage);
                return BadRequest(new { message = "Failed to verify bank account: " + recipientResult.ErrorMessage });
            }

            recipientCode = recipientResult.RecipientCode!;
            bankName = request.BankName ?? "";
            bankCode = request.BankCode!;
            accountNumberLast4 = request.AccountNumber!.Length >= 4
                ? request.AccountNumber[^4..]
                : request.AccountNumber;
            accountName = request.AccountName!;
        }

        // Create withdrawal record in PROCESSING state
        var withdrawal = new Withdrawal
        {
            InfluencerId = userId,
            AmountInPence = request.AmountInPence,
            Currency = currency,
            PaymentGateway = "paystack",
            BankName = bankName,
            BankCode = bankCode,
            AccountNumber = accountNumberLast4, // Only store last 4 digits
            AccountName = accountName,
            PaystackRecipientCode = recipientCode,
            Status = (int)WithdrawalStatus.PROCESSING,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };

        await _withdrawalRepo.CreateAsync(withdrawal);

        _logger.LogInformation("Withdrawal request created for influencer {UserId}, amount: {Amount}p, processing via Paystack",
            userId, request.AmountInPence);

        // Initiate transfer
        var transferReference = $"WD-{withdrawal.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var transferResult = await _paystackGateway.InitiateTransferAsync(new TransferRequest
        {
            RecipientCode = recipientCode,
            AmountInPence = request.AmountInPence,
            Reason = $"Inflan withdrawal #{withdrawal.Id}",
            Reference = transferReference,
            Currency = currency
        });

        if (!transferResult.Success)
        {
            withdrawal.Status = (int)WithdrawalStatus.FAILED;
            withdrawal.FailureReason = transferResult.ErrorMessage ?? "Failed to initiate transfer";
            await _withdrawalRepo.UpdateAsync(withdrawal);

            _logger.LogError("Failed to initiate Paystack transfer for withdrawal {WithdrawalId}: {Error}",
                withdrawal.Id, transferResult.ErrorMessage);

            return BadRequest(new { message = "Failed to process withdrawal: " + transferResult.ErrorMessage });
        }

        withdrawal.PaystackTransferCode = transferResult.TransferCode;

        // Update status based on transfer result
        if (transferResult.Status == TransferStatus.Success)
        {
            withdrawal.Status = (int)WithdrawalStatus.COMPLETED;
            withdrawal.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Withdrawal {WithdrawalId} completed successfully", withdrawal.Id);
        }
        else if (transferResult.Status == TransferStatus.Pending || transferResult.Status == TransferStatus.OtpRequired)
        {
            // Keep as PROCESSING - webhook will update when complete
            // OtpRequired means the business needs to approve via Paystack dashboard
            _logger.LogInformation("Withdrawal {WithdrawalId} is {Status}, waiting for Paystack webhook",
                withdrawal.Id, transferResult.Status);
        }
        else if (transferResult.Status == TransferStatus.Failed || transferResult.Status == TransferStatus.Reversed)
        {
            withdrawal.Status = (int)WithdrawalStatus.FAILED;
            withdrawal.FailureReason = transferResult.ErrorMessage ?? "Transfer failed";
            _logger.LogWarning("Withdrawal {WithdrawalId} transfer status: {Status}", withdrawal.Id, transferResult.Status);
        }
        else
        {
            // Unknown status - keep as processing and wait for webhook
            _logger.LogWarning("Withdrawal {WithdrawalId} has unknown transfer status: {Status}", withdrawal.Id, transferResult.Status);
        }

        await _withdrawalRepo.UpdateAsync(withdrawal);

        // Send notification to influencer about withdrawal status
        var formattedAmount = FormatAmount(withdrawal.AmountInPence, withdrawal.Currency);
        if (withdrawal.Status == (int)WithdrawalStatus.COMPLETED)
        {
            await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
            {
                UserId = userId,
                Type = NotificationType.Payment,
                Title = "Withdrawal Successful",
                Message = $"Your withdrawal of {formattedAmount} has been sent to your bank account ({bankName} ****{accountNumberLast4})",
                ReferenceId = withdrawal.Id,
                ReferenceType = "withdrawal"
            });
        }
        else if (withdrawal.Status == (int)WithdrawalStatus.PROCESSING)
        {
            var processingMessage = transferResult.Status == TransferStatus.OtpRequired
                ? $"Your withdrawal request of {formattedAmount} is pending approval. You'll be notified when complete."
                : $"Your withdrawal request of {formattedAmount} is being processed. You'll be notified when complete.";

            await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
            {
                UserId = userId,
                Type = NotificationType.Payment,
                Title = "Withdrawal Processing",
                Message = processingMessage,
                ReferenceId = withdrawal.Id,
                ReferenceType = "withdrawal"
            });
        }
        else if (withdrawal.Status == (int)WithdrawalStatus.FAILED)
        {
            await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
            {
                UserId = userId,
                Type = NotificationType.Payment,
                Title = "Withdrawal Failed",
                Message = $"Your withdrawal request of {formattedAmount} could not be processed. Reason: {withdrawal.FailureReason ?? "Unknown error"}",
                ReferenceId = withdrawal.Id,
                ReferenceType = "withdrawal"
            });
        }

        var responseMessage = withdrawal.Status switch
        {
            (int)WithdrawalStatus.COMPLETED => "Withdrawal processed successfully",
            (int)WithdrawalStatus.FAILED => $"Withdrawal failed: {withdrawal.FailureReason}",
            _ when transferResult.Status == TransferStatus.OtpRequired => "Withdrawal submitted and pending approval. You'll be notified when complete.",
            _ => "Withdrawal is being processed. You'll be notified when complete."
        };

        return Ok(new
        {
            message = responseMessage,
            withdrawal = new
            {
                withdrawal.Id,
                withdrawal.AmountInPence,
                withdrawal.Currency,
                status = GetWithdrawalStatusText(withdrawal.Status),
                withdrawal.CreatedAt,
                withdrawal.ProcessedAt,
                withdrawal.CompletedAt
            }
        });
    }

    /// <summary>
    /// Process withdrawal via TrueLayer (for UK users - GBP)
    /// Uses open-loop payout - bank details are sent directly with payout request
    /// </summary>
    private async Task<IActionResult> ProcessTrueLayerWithdrawal(int userId, WithdrawalRequestDto request, string currency)
    {
        string bankName;
        string sortCode;
        string accountNumber;
        string accountNumberLast4;
        string accountName;

        // Check if using saved bank account or one-time details
        if (request.BankAccountId.HasValue)
        {
            // Use saved bank account - we need to retrieve the full account details
            // Note: For security, we only store last 4 digits in the database
            // For saved accounts, we need to get the full account number from a secure source
            // In this implementation, we require users to provide full details for each withdrawal
            // or use a pre-verified account through TrueLayer's closed-loop system

            var savedAccount = await _bankAccountRepo.GetByIdAsync(request.BankAccountId.Value);
            if (savedAccount == null)
            {
                return BadRequest(new { message = "Bank account not found" });
            }

            if (savedAccount.InfluencerId != userId)
            {
                return Forbid();
            }

            // For saved accounts, we need the user to provide the full account number again for security
            // The saved account only stores last 4 digits and sort code
            if (string.IsNullOrWhiteSpace(request.AccountNumber))
            {
                return BadRequest(new { message = "Please provide your full account number to verify the withdrawal" });
            }

            // Verify the provided account number matches the saved account (last 4 digits)
            var providedLast4 = request.AccountNumber.Length >= 4
                ? request.AccountNumber[^4..]
                : request.AccountNumber;

            if (providedLast4 != savedAccount.AccountNumberLast4)
            {
                return BadRequest(new { message = "Account number doesn't match saved bank account" });
            }

            bankName = savedAccount.BankName;
            sortCode = savedAccount.BankCode; // For UK accounts, BankCode stores the sort code
            accountNumber = request.AccountNumber;
            accountNumberLast4 = savedAccount.AccountNumberLast4;
            accountName = savedAccount.AccountName;
        }
        else
        {
            // Validate required bank details for one-time use (UK format)
            if (string.IsNullOrWhiteSpace(request.SortCode))
            {
                return BadRequest(new { message = "Sort code is required for UK bank accounts" });
            }

            if (string.IsNullOrWhiteSpace(request.AccountNumber))
            {
                return BadRequest(new { message = "Account number is required" });
            }

            if (string.IsNullOrWhiteSpace(request.AccountName))
            {
                return BadRequest(new { message = "Account name is required" });
            }

            // Validate UK bank account format
            var validationResult = await _trueLayerGateway.CreateExternalAccountAsync(new TrueLayerBeneficiaryRequest
            {
                AccountName = request.AccountName!,
                AccountNumber = request.AccountNumber!,
                SortCode = request.SortCode!
            });

            if (!validationResult.Success)
            {
                return BadRequest(new { message = validationResult.ErrorMessage });
            }

            bankName = request.BankName ?? "";
            sortCode = request.SortCode!;
            accountNumber = request.AccountNumber!;
            accountNumberLast4 = request.AccountNumber!.Length >= 4
                ? request.AccountNumber[^4..]
                : request.AccountNumber;
            accountName = request.AccountName!;
        }

        // Create withdrawal record in PROCESSING state
        var withdrawal = new Withdrawal
        {
            InfluencerId = userId,
            AmountInPence = request.AmountInPence,
            Currency = currency,
            PaymentGateway = "truelayer",
            BankName = bankName,
            BankCode = sortCode, // Store sort code for UK accounts
            AccountNumber = accountNumberLast4, // Only store last 4 digits
            AccountName = accountName,
            Status = (int)WithdrawalStatus.PROCESSING,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };

        await _withdrawalRepo.CreateAsync(withdrawal);

        _logger.LogInformation("Withdrawal request created for influencer {UserId}, amount: {Amount}p, processing via TrueLayer",
            userId, request.AmountInPence);

        // Initiate open-loop payout - send bank details directly
        var payoutReference = $"WD{withdrawal.Id}";
        var payoutResult = await _trueLayerGateway.InitiatePayoutAsync(new TrueLayerPayoutRequest
        {
            SortCode = sortCode,
            AccountNumber = accountNumber,
            AccountName = accountName,
            AmountInPence = request.AmountInPence,
            Reference = payoutReference
        });

        if (!payoutResult.Success)
        {
            withdrawal.Status = (int)WithdrawalStatus.FAILED;
            withdrawal.FailureReason = payoutResult.ErrorMessage ?? "Failed to initiate payout";
            await _withdrawalRepo.UpdateAsync(withdrawal);

            _logger.LogError("Failed to initiate TrueLayer payout for withdrawal {WithdrawalId}: {Error}",
                withdrawal.Id, payoutResult.ErrorMessage);

            return BadRequest(new { message = "Failed to process withdrawal: " + payoutResult.ErrorMessage });
        }

        withdrawal.TrueLayerPayoutId = payoutResult.PayoutId;

        // Update status based on payout result
        if (payoutResult.Status == TrueLayerPayoutStatus.Executed)
        {
            withdrawal.Status = (int)WithdrawalStatus.COMPLETED;
            withdrawal.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Withdrawal {WithdrawalId} completed successfully via TrueLayer", withdrawal.Id);
        }
        else if (payoutResult.Status == TrueLayerPayoutStatus.Pending || payoutResult.Status == TrueLayerPayoutStatus.Authorized)
        {
            // Keep as PROCESSING - webhook will update when complete
            _logger.LogInformation("Withdrawal {WithdrawalId} is {Status}, waiting for TrueLayer webhook",
                withdrawal.Id, payoutResult.Status);
        }
        else if (payoutResult.Status == TrueLayerPayoutStatus.Failed)
        {
            withdrawal.Status = (int)WithdrawalStatus.FAILED;
            withdrawal.FailureReason = payoutResult.ErrorMessage ?? "Payout failed";
            _logger.LogWarning("Withdrawal {WithdrawalId} payout status: {Status}", withdrawal.Id, payoutResult.Status);
        }
        else
        {
            // Unknown status - keep as processing and wait for webhook
            _logger.LogWarning("Withdrawal {WithdrawalId} has unknown payout status: {Status}", withdrawal.Id, payoutResult.Status);
        }

        await _withdrawalRepo.UpdateAsync(withdrawal);

        // Send notification to influencer about withdrawal status
        var formattedAmount = FormatAmount(withdrawal.AmountInPence, withdrawal.Currency);
        if (withdrawal.Status == (int)WithdrawalStatus.COMPLETED)
        {
            await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
            {
                UserId = userId,
                Type = NotificationType.Payment,
                Title = "Withdrawal Successful",
                Message = $"Your withdrawal of {formattedAmount} has been sent to your bank account ({bankName} ****{accountNumberLast4})",
                ReferenceId = withdrawal.Id,
                ReferenceType = "withdrawal"
            });
        }
        else if (withdrawal.Status == (int)WithdrawalStatus.PROCESSING)
        {
            await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
            {
                UserId = userId,
                Type = NotificationType.Payment,
                Title = "Withdrawal Processing",
                Message = $"Your withdrawal request of {formattedAmount} is being processed. You'll be notified when complete.",
                ReferenceId = withdrawal.Id,
                ReferenceType = "withdrawal"
            });
        }
        else if (withdrawal.Status == (int)WithdrawalStatus.FAILED)
        {
            await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
            {
                UserId = userId,
                Type = NotificationType.Payment,
                Title = "Withdrawal Failed",
                Message = $"Your withdrawal request of {formattedAmount} could not be processed. Reason: {withdrawal.FailureReason ?? "Unknown error"}",
                ReferenceId = withdrawal.Id,
                ReferenceType = "withdrawal"
            });
        }

        var responseMessage = withdrawal.Status switch
        {
            (int)WithdrawalStatus.COMPLETED => "Withdrawal processed successfully",
            (int)WithdrawalStatus.FAILED => $"Withdrawal failed: {withdrawal.FailureReason}",
            _ => "Withdrawal is being processed. You'll be notified when complete."
        };

        return Ok(new
        {
            message = responseMessage,
            withdrawal = new
            {
                withdrawal.Id,
                withdrawal.AmountInPence,
                withdrawal.Currency,
                status = GetWithdrawalStatusText(withdrawal.Status),
                withdrawal.CreatedAt,
                withdrawal.ProcessedAt,
                withdrawal.CompletedAt
            }
        });
    }

    /// <summary>
    /// Get list of supported banks
    /// </summary>
    [HttpGet("banks")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBanks([FromQuery] string? country = null)
    {
        var countryCode = country?.ToUpperInvariant() ?? CurrencyConstants.PrimaryCountry;

        // For UK, return static list from TrueLayer (TrueLayer doesn't have bank list API)
        if (countryCode == "GB")
        {
            var ukBanks = _trueLayerGateway.GetUKBanks();
            return Ok(ukBanks);
        }

        // For Nigeria and others, use Paystack
        var banks = await _paystackGateway.GetBanksAsync(countryCode);
        return Ok(banks);
    }

    /// <summary>
    /// Verify a bank account
    /// </summary>
    [HttpGet("verify-account")]
    public async Task<IActionResult> VerifyBankAccount([FromQuery] string accountNumber, [FromQuery] string bankCode)
    {
        if (string.IsNullOrWhiteSpace(accountNumber) || string.IsNullOrWhiteSpace(bankCode))
        {
            return BadRequest(new { message = "Account number and bank code are required" });
        }

        var result = await _paystackGateway.VerifyBankAccountAsync(accountNumber, bankCode);

        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "Failed to verify account" });
        }

        return Ok(new
        {
            accountName = result.AccountName,
            accountNumber = result.AccountNumber,
            bankId = result.BankId
        });
    }

    /// <summary>
    /// Get withdrawal history
    /// </summary>
    [HttpGet("withdraw/history")]
    public async Task<IActionResult> GetWithdrawalHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] long? minAmount = null,
        [FromQuery] long? maxAmount = null,
        [FromQuery] int? status = null)
    {
        var userId = GetCurrentUserId();
        var filter = new PaymentFilterDto
        {
            Page = page,
            PageSize = pageSize,
            DateFrom = dateFrom,
            DateTo = dateTo,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            Status = status
        };

        var (withdrawals, totalCount) = await _withdrawalRepo.GetByInfluencerIdFilteredAsync(userId, filter);

        return Ok(new
        {
            items = withdrawals.Select(w => new
            {
                w.Id,
                w.AmountInPence,
                w.Currency,
                w.Status,
                statusText = GetWithdrawalStatusText(w.Status),
                w.BankName,
                accountNumber = MaskAccountNumber(w.AccountNumber),
                w.AccountName,
                w.FailureReason,
                w.CreatedAt,
                w.ProcessedAt,
                w.CompletedAt
            }),
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Cancel a pending withdrawal
    /// </summary>
    [HttpPost("withdraw/{id}/cancel")]
    public async Task<IActionResult> CancelWithdrawal(int id)
    {
        var userId = GetCurrentUserId();
        var withdrawal = await _withdrawalRepo.GetByIdAsync(id);

        if (withdrawal == null)
        {
            return NotFound(new { message = "Withdrawal not found" });
        }

        if (withdrawal.InfluencerId != userId)
        {
            return Forbid();
        }

        if (withdrawal.Status != (int)WithdrawalStatus.PENDING)
        {
            return BadRequest(new { message = "Only pending withdrawals can be cancelled" });
        }

        withdrawal.Status = (int)WithdrawalStatus.CANCELLED;
        await _withdrawalRepo.UpdateAsync(withdrawal);

        _logger.LogInformation("Withdrawal {WithdrawalId} cancelled by influencer {UserId}", id, userId);

        return Ok(new { message = "Withdrawal cancelled successfully" });
    }

    private static string GetWithdrawalStatusText(int status)
    {
        return status switch
        {
            (int)WithdrawalStatus.PENDING => "Pending",
            (int)WithdrawalStatus.PROCESSING => "Processing",
            (int)WithdrawalStatus.COMPLETED => "Completed",
            (int)WithdrawalStatus.FAILED => "Failed",
            (int)WithdrawalStatus.CANCELLED => "Cancelled",
            _ => "Unknown"
        };
    }

    private static string? MaskAccountNumber(string? accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber) || accountNumber.Length < 4)
            return accountNumber;

        return new string('*', accountNumber.Length - 4) + accountNumber[^4..];
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

    #endregion

    #region Bank Account Management

    /// <summary>
    /// Get saved bank accounts for the current influencer
    /// </summary>
    [HttpGet("bank-accounts")]
    public async Task<IActionResult> GetBankAccounts()
    {
        var userId = GetCurrentUserId();
        var accounts = await _bankAccountRepo.GetByInfluencerIdAsync(userId);

        return Ok(accounts.Select(a => new
        {
            a.Id,
            a.BankName,
            a.BankCode,
            accountNumberLast4 = a.AccountNumberLast4,
            a.AccountName,
            a.IsDefault,
            a.CreatedAt
        }));
    }

    /// <summary>
    /// Add a new bank account - creates recipient on Paystack (NGN) or TrueLayer (GBP)
    /// </summary>
    [HttpPost("bank-accounts")]
    public async Task<IActionResult> AddBankAccount([FromBody] AddBankAccountDto request)
    {
        var userId = GetCurrentUserId();

        // Verify password
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Password is required to add bank account" });

        var isPasswordValid = await _userService.VerifyPasswordAsync(userId, request.Password);
        if (!isPasswordValid)
            return BadRequest(new { message = "Invalid password" });

        if (string.IsNullOrWhiteSpace(request.AccountNumber))
            return BadRequest(new { message = "Account number is required" });

        if (string.IsNullOrWhiteSpace(request.AccountName))
            return BadRequest(new { message = "Account name is required" });

        // Get user's currency and gateway based on location
        var (userCurrency, gateway) = await GetUserCurrencyAndGatewayAsync(userId);

        _logger.LogInformation("AddBankAccount: UserId={UserId}, Gateway={Gateway}, Currency={Currency}, SortCode={SortCode}, BankCode={BankCode}",
            userId, gateway, userCurrency, request.SortCode, request.BankCode);

        string? paystackRecipientCode = null;
        string? trueLayerBeneficiaryId = null;
        string bankCode;

        if (gateway == "truelayer")
        {
            // For UK users - validate sort code
            if (string.IsNullOrWhiteSpace(request.SortCode))
                return BadRequest(new { message = "Sort code is required for UK bank accounts" });

            // Create external account on TrueLayer
            var beneficiaryResult = await _trueLayerGateway.CreateExternalAccountAsync(new TrueLayerBeneficiaryRequest
            {
                AccountName = request.AccountName,
                AccountNumber = request.AccountNumber,
                SortCode = request.SortCode
            });

            if (!beneficiaryResult.Success)
            {
                _logger.LogError("Failed to create TrueLayer beneficiary for user {UserId}: {Error}",
                    userId, beneficiaryResult.ErrorMessage);
                return BadRequest(new { message = "Failed to verify bank account: " + beneficiaryResult.ErrorMessage });
            }

            trueLayerBeneficiaryId = beneficiaryResult.BeneficiaryId;
            bankCode = request.SortCode; // Store sort code as bank code for UK accounts
        }
        else
        {
            // For Nigerian users - validate bank code
            if (string.IsNullOrWhiteSpace(request.BankCode))
                return BadRequest(new { message = "Bank code is required" });

            // Create transfer recipient on Paystack
            var recipientResult = await _paystackGateway.CreateTransferRecipientAsync(new TransferRecipientRequest
            {
                AccountName = request.AccountName,
                AccountNumber = request.AccountNumber,
                BankCode = request.BankCode,
                Currency = userCurrency
            });

            if (!recipientResult.Success)
            {
                _logger.LogError("Failed to create Paystack recipient for user {UserId}: {Error}",
                    userId, recipientResult.ErrorMessage);
                return BadRequest(new { message = "Failed to verify bank account: " + recipientResult.ErrorMessage });
            }

            paystackRecipientCode = recipientResult.RecipientCode;
            bankCode = request.BankCode;
        }

        // Check if this is the first account (make it default)
        var existingAccounts = await _bankAccountRepo.GetByInfluencerIdAsync(userId);
        var isFirstAccount = !existingAccounts.Any();

        // Store only reference, not full account number
        var bankAccount = new InfluencerBankAccount
        {
            InfluencerId = userId,
            BankName = request.BankName ?? "",
            BankCode = bankCode,
            AccountNumberLast4 = request.AccountNumber.Length >= 4
                ? request.AccountNumber[^4..]
                : request.AccountNumber,
            AccountName = request.AccountName,
            Currency = userCurrency,
            PaymentGateway = gateway,
            PaystackRecipientCode = paystackRecipientCode,
            TrueLayerBeneficiaryId = trueLayerBeneficiaryId,
            IsDefault = isFirstAccount,
            CreatedAt = DateTime.UtcNow
        };

        await _bankAccountRepo.CreateAsync(bankAccount);

        _logger.LogInformation("Bank account added for user {UserId} via {Gateway}",
            userId, gateway);

        return Ok(new
        {
            message = "Bank account added successfully",
            bankAccount = new
            {
                bankAccount.Id,
                bankAccount.BankName,
                bankAccount.BankCode,
                accountNumberLast4 = bankAccount.AccountNumberLast4,
                bankAccount.AccountName,
                bankAccount.IsDefault,
                bankAccount.CreatedAt
            }
        });
    }

    /// <summary>
    /// Set a bank account as default
    /// </summary>
    [HttpPost("bank-accounts/{id}/set-default")]
    public async Task<IActionResult> SetDefaultBankAccount(int id)
    {
        var userId = GetCurrentUserId();
        var account = await _bankAccountRepo.GetByIdAsync(id);

        if (account == null)
            return NotFound(new { message = "Bank account not found" });

        if (account.InfluencerId != userId)
            return Forbid();

        await _bankAccountRepo.SetDefaultAsync(userId, id);

        return Ok(new { message = "Default bank account updated" });
    }

    /// <summary>
    /// Delete a bank account
    /// </summary>
    [HttpDelete("bank-accounts/{id}")]
    public async Task<IActionResult> DeleteBankAccount(int id)
    {
        var userId = GetCurrentUserId();
        var account = await _bankAccountRepo.GetByIdAsync(id);

        if (account == null)
            return NotFound(new { message = "Bank account not found" });

        if (account.InfluencerId != userId)
            return Forbid();

        await _bankAccountRepo.DeleteAsync(id);

        _logger.LogInformation("Bank account {AccountId} deleted for user {UserId}", id, userId);

        return Ok(new { message = "Bank account removed" });
    }

    #endregion
}

public class AddBankAccountDto
{
    public string? BankName { get; set; }
    public string? BankCode { get; set; } // Nigerian bank code
    public string? SortCode { get; set; } // UK sort code (for TrueLayer)
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string? Password { get; set; }
}

public class WithdrawalRequestDto
{
    public long AmountInPence { get; set; }
    public int? BankAccountId { get; set; } // Use saved bank account
    // OR provide bank details for one-time use
    public string? BankName { get; set; }
    public string? BankCode { get; set; } // Nigerian bank code
    public string? SortCode { get; set; } // UK sort code (for TrueLayer)
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? Password { get; set; }
}
