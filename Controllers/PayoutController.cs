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
    private readonly IUserService _userService;
    private readonly IInfluencerBankAccountRepository _bankAccountRepo;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PayoutController> _logger;

    public PayoutController(
        IInfluencerPayoutRepository payoutRepo,
        IWithdrawalRepository withdrawalRepo,
        IPaymentMilestoneRepository milestoneRepo,
        ICampaignRepository campaignRepo,
        IPlatformSettingsService settingsService,
        PaystackGateway paystackGateway,
        IUserService userService,
        IInfluencerBankAccountRepository bankAccountRepo,
        INotificationService notificationService,
        ILogger<PayoutController> logger)
    {
        _payoutRepo = payoutRepo;
        _withdrawalRepo = withdrawalRepo;
        _milestoneRepo = milestoneRepo;
        _campaignRepo = campaignRepo;
        _settingsService = settingsService;
        _paystackGateway = paystackGateway;
        _userService = userService;
        _bankAccountRepo = bankAccountRepo;
        _notificationService = notificationService;
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
        // Get all unpaid milestones for this influencer's campaigns
        var unpaidMilestones = await _milestoneRepo.GetUpcomingByInfluencerIdAsync(userId);
        var totalPending = unpaidMilestones.Sum(m => m.AmountInPence);

        return Ok(new
        {
            pendingAmountInPence = totalPending,
            currency = CurrencyConstants.PrimaryCurrency
        });
    }

    /// <summary>
    /// Get total released balance for influencer (available for withdrawal)
    /// </summary>
    [HttpGet("balance/released")]
    public async Task<IActionResult> GetReleasedBalance()
    {
        var userId = GetCurrentUserId();
        var totalReleased = await _payoutRepo.GetTotalReleasedByInfluencerIdAsync(userId);

        return Ok(new
        {
            releasedAmountInPence = totalReleased,
            currency = CurrencyConstants.PrimaryCurrency
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

        return Ok(milestones.Select(m => new
        {
            m.Id,
            m.CampaignId,
            campaignName = m.Campaign?.ProjectName,
            brandName = m.Campaign?.Brand?.BrandName ?? m.Campaign?.Brand?.Name,
            m.MilestoneNumber,
            m.AmountInPence,
            m.PlatformFeeInPence,
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
    /// Get available balance for withdrawal (released - already withdrawn)
    /// </summary>
    [HttpGet("withdraw/available")]
    public async Task<IActionResult> GetAvailableForWithdrawal()
    {
        var userId = GetCurrentUserId();
        var totalReleased = await _payoutRepo.GetTotalReleasedByInfluencerIdAsync(userId);
        var totalWithdrawn = await _withdrawalRepo.GetTotalWithdrawnByInfluencerIdAsync(userId);
        var available = totalReleased - totalWithdrawn;

        return Ok(new
        {
            releasedAmountInPence = totalReleased,
            withdrawnAmountInPence = totalWithdrawn,
            availableAmountInPence = available > 0 ? available : 0,
            currency = CurrencyConstants.PrimaryCurrency
        });
    }

    /// <summary>
    /// Request a withdrawal - automatically processed via Paystack
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

        // Check available balance
        var totalReleased = await _payoutRepo.GetTotalReleasedByInfluencerIdAsync(userId);
        var totalWithdrawn = await _withdrawalRepo.GetTotalWithdrawnByInfluencerIdAsync(userId);
        var available = totalReleased - totalWithdrawn;

        if (request.AmountInPence > available)
        {
            return BadRequest(new { message = "Insufficient balance for withdrawal" });
        }

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
                Currency = CurrencyConstants.PrimaryCurrency
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
            Currency = CurrencyConstants.PrimaryCurrency,
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
            Currency = CurrencyConstants.PrimaryCurrency
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
    /// Get list of supported banks
    /// </summary>
    [HttpGet("banks")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBanks([FromQuery] string? country = null)
    {
        var banks = await _paystackGateway.GetBanksAsync(country ?? CurrencyConstants.PrimaryCountry);
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
    /// Add a new bank account - creates recipient on Paystack
    /// </summary>
    [HttpPost("bank-accounts")]
    public async Task<IActionResult> AddBankAccount([FromBody] AddBankAccountDto request)
    {
        var userId = GetCurrentUserId();

        // Validate inputs
        if (string.IsNullOrWhiteSpace(request.BankCode))
            return BadRequest(new { message = "Bank code is required" });

        if (string.IsNullOrWhiteSpace(request.AccountNumber))
            return BadRequest(new { message = "Account number is required" });

        if (string.IsNullOrWhiteSpace(request.AccountName))
            return BadRequest(new { message = "Account name is required" });

        // Verify password
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Password is required to add bank account" });

        var isPasswordValid = await _userService.VerifyPasswordAsync(userId, request.Password);
        if (!isPasswordValid)
            return BadRequest(new { message = "Invalid password" });

        // Create transfer recipient on Paystack
        var recipientResult = await _paystackGateway.CreateTransferRecipientAsync(new TransferRecipientRequest
        {
            AccountName = request.AccountName,
            AccountNumber = request.AccountNumber,
            BankCode = request.BankCode,
            Currency = CurrencyConstants.PrimaryCurrency
        });

        if (!recipientResult.Success)
        {
            _logger.LogError("Failed to create Paystack recipient for user {UserId}: {Error}",
                userId, recipientResult.ErrorMessage);
            return BadRequest(new { message = "Failed to verify bank account: " + recipientResult.ErrorMessage });
        }

        // Check if this is the first account (make it default)
        var existingAccounts = await _bankAccountRepo.GetByInfluencerIdAsync(userId);
        var isFirstAccount = !existingAccounts.Any();

        // Store only reference, not full account number
        var bankAccount = new InfluencerBankAccount
        {
            InfluencerId = userId,
            BankName = request.BankName ?? "",
            BankCode = request.BankCode,
            AccountNumberLast4 = request.AccountNumber.Length >= 4
                ? request.AccountNumber[^4..]
                : request.AccountNumber,
            AccountName = request.AccountName,
            PaystackRecipientCode = recipientResult.RecipientCode!,
            IsDefault = isFirstAccount,
            CreatedAt = DateTime.UtcNow
        };

        await _bankAccountRepo.CreateAsync(bankAccount);

        _logger.LogInformation("Bank account added for user {UserId}, recipient code: {RecipientCode}",
            userId, recipientResult.RecipientCode);

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
    public string BankCode { get; set; } = "";
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
    public string? BankCode { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? Password { get; set; }
}
