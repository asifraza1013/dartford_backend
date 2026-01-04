using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace inflan_api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentModuleController : ControllerBase
{
    private readonly IPaymentOrchestrator _paymentOrchestrator;
    private readonly IMilestoneService _milestoneService;
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly ILogger<PaymentModuleController> _logger;

    public PaymentModuleController(
        IPaymentOrchestrator paymentOrchestrator,
        IMilestoneService milestoneService,
        IPaymentMethodRepository paymentMethodRepo,
        ILogger<PaymentModuleController> logger)
    {
        _paymentOrchestrator = paymentOrchestrator;
        _milestoneService = milestoneService;
        _paymentMethodRepo = paymentMethodRepo;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("id")?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    /// <summary>
    /// Initiate a payment for a campaign
    /// </summary>
    [HttpPost("initiate")]
    [Authorize]
    public async Task<IActionResult> InitiatePayment([FromBody] InitiatePaymentDto request)
    {
        var userId = GetCurrentUserId();

        _logger.LogInformation("===== PAYMENT INITIATION REQUESTED =====");
        _logger.LogInformation("User ID: {UserId}", userId);
        _logger.LogInformation("Campaign ID: {CampaignId}", request.CampaignId);
        _logger.LogInformation("Gateway: {Gateway}", request.Gateway);
        _logger.LogInformation("Milestone ID: {MilestoneId}", request.MilestoneId);
        _logger.LogInformation("Amount in Pence: {AmountInPence}", request.AmountInPence);
        _logger.LogInformation("Save Payment Method: {SavePaymentMethod}", request.SavePaymentMethod);
        _logger.LogInformation("Success URL: {SuccessUrl}", request.SuccessUrl);
        _logger.LogInformation("Failure URL: {FailureUrl}", request.FailureUrl);

        var initiateRequest = new InitiatePaymentRequest
        {
            CampaignId = request.CampaignId,
            UserId = userId,
            Gateway = request.Gateway,
            MilestoneId = request.MilestoneId,
            AmountInPence = request.AmountInPence,
            SavePaymentMethod = request.SavePaymentMethod,
            SuccessUrl = request.SuccessUrl,
            FailureUrl = request.FailureUrl
        };

        var result = await _paymentOrchestrator.InitiatePaymentAsync(initiateRequest);

        if (result.Success)
        {
            _logger.LogInformation("Payment initiated successfully. Redirect URL: {RedirectUrl}, Transaction Ref: {TransactionRef}",
                result.RedirectUrl, result.TransactionReference);

            return Ok(new
            {
                success = true,
                redirectUrl = result.RedirectUrl,
                transactionReference = result.TransactionReference
            });
        }

        _logger.LogWarning("Payment initiation failed: {ErrorMessage}", result.ErrorMessage);
        return BadRequest(new { success = false, message = result.ErrorMessage });
    }

    /// <summary>
    /// Process TrueLayer webhook
    /// </summary>
    [HttpPost("webhook/truelayer")]
    public async Task<IActionResult> TrueLayerWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-TL-Signature"].FirstOrDefault();

        _logger.LogInformation("Received TrueLayer webhook");

        var success = await _paymentOrchestrator.ProcessWebhookAsync("truelayer", payload, signature);

        return success ? Ok() : BadRequest();
    }

    /// <summary>
    /// Process Paystack webhook
    /// </summary>
    [HttpPost("webhook/paystack")]
    public async Task<IActionResult> PaystackWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-Paystack-Signature"].FirstOrDefault();

        _logger.LogInformation("===== PAYSTACK WEBHOOK RECEIVED =====");
        _logger.LogInformation("Signature present: {HasSignature}", !string.IsNullOrEmpty(signature));
        _logger.LogInformation("Payload length: {Length}", payload?.Length ?? 0);
        _logger.LogInformation("Payload: {Payload}", payload);

        var success = await _paymentOrchestrator.ProcessWebhookAsync("paystack", payload, signature);

        _logger.LogInformation("Webhook processing result: {Success}", success);

        return success ? Ok() : BadRequest();
    }

    /// <summary>
    /// Get payment status
    /// </summary>
    [HttpGet("status/{transactionReference}")]
    [Authorize]
    public async Task<IActionResult> GetPaymentStatus(string transactionReference)
    {
        var transaction = await _paymentOrchestrator.GetPaymentStatusAsync(transactionReference);

        if (transaction == null)
            return NotFound(new { message = "Transaction not found" });

        return Ok(new
        {
            transactionReference = transaction.TransactionReference,
            status = transaction.TransactionStatus,
            amountInPence = transaction.AmountInPence,
            platformFeeInPence = transaction.PlatformFeeInPence,
            totalAmountInPence = transaction.TotalAmountInPence,
            currency = transaction.Currency,
            gateway = transaction.Gateway,
            createdAt = transaction.CreatedAt,
            completedAt = transaction.CompletedAt,
            failureMessage = transaction.FailureMessage
        });
    }

    /// <summary>
    /// Verify payment with gateway and update status (useful for local dev without webhooks)
    /// </summary>
    [HttpPost("verify/{transactionReference}")]
    [Authorize]
    public async Task<IActionResult> VerifyPayment(string transactionReference)
    {
        try
        {
            var result = await _paymentOrchestrator.VerifyAndProcessPaymentAsync(transactionReference);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    message = "Payment verified and processed successfully",
                    status = result.Status
                });
            }

            return BadRequest(new { success = false, message = result.ErrorMessage });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment {TransactionReference}", transactionReference);
            return BadRequest(new { success = false, message = "Error verifying payment" });
        }
    }

    /// <summary>
    /// Verify payment by gateway payment ID (e.g., TrueLayer payment_id from callback URL)
    /// This endpoint is used by the frontend callback page to verify payment status
    /// </summary>
    [HttpPost("verify-by-gateway/{gatewayPaymentId}")]
    [Authorize]
    public async Task<IActionResult> VerifyPaymentByGatewayId(string gatewayPaymentId)
    {
        try
        {
            _logger.LogInformation("Verifying payment by gateway ID: {GatewayPaymentId}", gatewayPaymentId);

            var result = await _paymentOrchestrator.VerifyPaymentByGatewayIdAsync(gatewayPaymentId);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    message = "Payment verified successfully",
                    status = result.Status,
                    transactionReference = result.TransactionReference
                });
            }

            return BadRequest(new { success = false, message = result.ErrorMessage });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment by gateway ID {GatewayPaymentId}", gatewayPaymentId);
            return BadRequest(new { success = false, message = "Error verifying payment" });
        }
    }

    /// <summary>
    /// Get payment status by gateway payment ID (e.g., TrueLayer payment_id)
    /// </summary>
    [HttpGet("status-by-gateway/{gatewayPaymentId}")]
    [Authorize]
    public async Task<IActionResult> GetPaymentStatusByGatewayId(string gatewayPaymentId)
    {
        try
        {
            var transaction = await _paymentOrchestrator.GetPaymentByGatewayIdAsync(gatewayPaymentId);

            if (transaction == null)
                return NotFound(new { message = "Transaction not found" });

            return Ok(new
            {
                transactionReference = transaction.TransactionReference,
                status = transaction.TransactionStatus,
                amountInPence = transaction.AmountInPence,
                platformFeeInPence = transaction.PlatformFeeInPence,
                totalAmountInPence = transaction.TotalAmountInPence,
                currency = transaction.Currency,
                gateway = transaction.Gateway,
                createdAt = transaction.CreatedAt,
                completedAt = transaction.CompletedAt,
                failureMessage = transaction.FailureMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment status by gateway ID {GatewayPaymentId}", gatewayPaymentId);
            return BadRequest(new { success = false, message = "Error getting payment status" });
        }
    }

    /// <summary>
    /// Charge saved card for recurring payment (Paystack only)
    /// </summary>
    [HttpPost("charge-saved-card")]
    [Authorize]
    public async Task<IActionResult> ChargeSavedCard([FromBody] ChargeSavedCardDto request)
    {
        var result = await _paymentOrchestrator.ChargeRecurringPaymentAsync(
            request.MilestoneId,
            request.PaymentMethodId);

        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                transactionReference = result.TransactionReference
            });
        }

        return BadRequest(new { success = false, message = result.ErrorMessage });
    }

    /// <summary>
    /// Get campaign payment summary (balance)
    /// </summary>
    [HttpGet("balance/campaign/{campaignId}")]
    [Authorize]
    public async Task<IActionResult> GetCampaignBalance(int campaignId)
    {
        try
        {
            var summary = await _paymentOrchestrator.GetCampaignPaymentSummaryAsync(campaignId);
            return Ok(summary);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get brand's outstanding balance (only overdue milestones trigger warning)
    /// </summary>
    [HttpGet("balance/brand/outstanding")]
    [Authorize]
    public async Task<IActionResult> GetBrandOutstandingBalance()
    {
        var userId = GetCurrentUserId();
        var balanceInfo = await _paymentOrchestrator.GetBrandOutstandingBalanceDetailedAsync(userId);
        return Ok(new
        {
            outstandingAmountInPence = balanceInfo.OverdueAmountInPence, // Only overdue milestones
            totalRemainingInPence = balanceInfo.TotalRemainingInPence,   // All unpaid milestones
            totalPaidInPence = balanceInfo.TotalPaidInPence,
            hasOverdueMilestones = balanceInfo.HasOverdueMilestones,
            overdueMilestoneCount = balanceInfo.OverdueMilestoneCount
        });
    }

    /// <summary>
    /// Get all campaigns with milestones for brand (for upcoming payments view)
    /// </summary>
    [HttpGet("campaigns/milestones")]
    [Authorize]
    public async Task<IActionResult> GetBrandCampaignsWithMilestones()
    {
        var userId = GetCurrentUserId();
        var campaigns = await _milestoneService.GetBrandCampaignsWithMilestonesAsync(userId);
        return Ok(campaigns);
    }

    /// <summary>
    /// Get milestones for a campaign
    /// </summary>
    [HttpGet("milestones/campaign/{campaignId}")]
    [Authorize]
    public async Task<IActionResult> GetCampaignMilestones(int campaignId)
    {
        var milestones = await _milestoneService.GetCampaignMilestonesAsync(campaignId);

        return Ok(milestones.Select(m => new
        {
            m.Id,
            m.CampaignId,
            m.MilestoneNumber,
            m.Title,
            m.Description,
            m.AmountInPence,
            m.PlatformFeeInPence,
            m.DueDate,
            m.Status,
            m.PaidAt,
            m.TransactionId
        }));
    }

    /// <summary>
    /// Create a new milestone for a campaign
    /// </summary>
    [HttpPost("milestones")]
    [Authorize]
    public async Task<IActionResult> CreateMilestone([FromBody] CreateMilestoneDto request)
    {
        try
        {
            var milestone = await _milestoneService.CreateMilestoneAsync(request);
            return Ok(new
            {
                milestone.Id,
                milestone.CampaignId,
                milestone.MilestoneNumber,
                milestone.Title,
                milestone.Description,
                milestone.AmountInPence,
                milestone.PlatformFeeInPence,
                milestone.DueDate,
                milestone.Status
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update a milestone
    /// </summary>
    [HttpPut("milestones/{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateMilestone(int id, [FromBody] UpdateMilestoneDto request)
    {
        try
        {
            var milestone = await _milestoneService.UpdateMilestoneAsync(id, request);
            return Ok(new
            {
                milestone.Id,
                milestone.CampaignId,
                milestone.MilestoneNumber,
                milestone.Title,
                milestone.Description,
                milestone.AmountInPence,
                milestone.PlatformFeeInPence,
                milestone.DueDate,
                milestone.Status
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a milestone
    /// </summary>
    [HttpDelete("milestones/{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteMilestone(int id)
    {
        try
        {
            await _milestoneService.DeleteMilestoneAsync(id);
            return Ok(new { message = "Milestone deleted successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update campaign payment configuration (payment type and auto-pay)
    /// </summary>
    [HttpPut("campaign/{campaignId}/payment-config")]
    [Authorize]
    public async Task<IActionResult> UpdatePaymentConfig(int campaignId, [FromBody] UpdatePaymentConfigDto request)
    {
        try
        {
            await _milestoneService.UpdateCampaignPaymentConfigAsync(campaignId, request.PaymentType, request.IsAutoPayEnabled);
            return Ok(new { message = "Payment configuration updated" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Pay full campaign amount (one-time payment)
    /// </summary>
    [HttpPost("pay-full")]
    [Authorize]
    public async Task<IActionResult> PayFullAmount([FromBody] PayFullAmountDto request)
    {
        var userId = GetCurrentUserId();

        var initiateRequest = new InitiatePaymentRequest
        {
            CampaignId = request.CampaignId,
            UserId = userId,
            Gateway = request.Gateway,
            AmountInPence = request.AmountInPence,
            SavePaymentMethod = request.SavePaymentMethod,
            SuccessUrl = request.SuccessUrl,
            FailureUrl = request.FailureUrl
        };

        var result = await _paymentOrchestrator.InitiatePaymentAsync(initiateRequest);

        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                redirectUrl = result.RedirectUrl,
                transactionReference = result.TransactionReference
            });
        }

        return BadRequest(new { success = false, message = result.ErrorMessage });
    }

    /// <summary>
    /// Get saved payment methods
    /// </summary>
    [HttpGet("payment-methods")]
    [Authorize]
    public async Task<IActionResult> GetPaymentMethods()
    {
        var userId = GetCurrentUserId();
        var methods = await _paymentMethodRepo.GetByUserIdAsync(userId);

        return Ok(methods.Select(m => new
        {
            m.Id,
            m.Gateway,
            m.CardType,
            m.Last4,
            m.ExpiryMonth,
            m.ExpiryYear,
            m.Bank,
            m.IsDefault,
            m.CreatedAt
        }));
    }

    /// <summary>
    /// Set default payment method
    /// </summary>
    [HttpPost("payment-methods/{id}/set-default")]
    [Authorize]
    public async Task<IActionResult> SetDefaultPaymentMethod(int id)
    {
        var userId = GetCurrentUserId();
        await _paymentMethodRepo.SetDefaultAsync(userId, id);
        return Ok(new { message = "Default payment method updated" });
    }

    /// <summary>
    /// Delete payment method
    /// </summary>
    [HttpDelete("payment-methods/{id}")]
    [Authorize]
    public async Task<IActionResult> DeletePaymentMethod(int id)
    {
        var method = await _paymentMethodRepo.GetByIdAsync(id);
        if (method == null)
            return NotFound(new { message = "Payment method not found" });

        var userId = GetCurrentUserId();
        if (method.UserId != userId)
            return Forbid();

        await _paymentMethodRepo.DeleteAsync(id);
        return Ok(new { message = "Payment method deleted" });
    }

    /// <summary>
    /// Get payment options for a campaign (checks if full payment is allowed)
    /// </summary>
    [HttpGet("payment-options/{campaignId}")]
    [Authorize]
    public async Task<IActionResult> GetPaymentOptions(int campaignId)
    {
        try
        {
            var milestones = await _milestoneService.GetCampaignMilestonesAsync(campaignId);
            var hasPaidMilestones = milestones.Any(m => m.Status == (int)MilestoneStatus.PAID);
            var pendingMilestones = milestones.Where(m => m.Status == (int)MilestoneStatus.PENDING || m.Status == (int)MilestoneStatus.OVERDUE).ToList();

            var pendingMilestonesList = pendingMilestones.Select(m => new
            {
                m.Id,
                m.MilestoneNumber,
                m.Title,
                m.Description,
                m.AmountInPence,
                m.PlatformFeeInPence,
                m.DueDate,
                m.Status,
                isOverdue = m.Status == (int)MilestoneStatus.OVERDUE
            }).ToList();

            return Ok(new
            {
                fullPaymentAllowed = !hasPaidMilestones,
                fullPaymentDisabledReason = hasPaidMilestones
                    ? "Full payment is not available because one or more milestones have already been paid. Please pay the remaining milestones individually."
                    : null,
                hasPaidMilestones,
                paidMilestoneCount = milestones.Count(m => m.Status == (int)MilestoneStatus.PAID),
                pendingMilestoneCount = pendingMilestones.Count,
                totalMilestoneCount = milestones.Count,
                pendingMilestones = pendingMilestonesList
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment options for campaign {CampaignId}", campaignId);
            return BadRequest(new { message = "Error getting payment options" });
        }
    }

    /// <summary>
    /// Release payment to influencer
    /// </summary>
    [HttpPost("payout/release/{payoutId}")]
    [Authorize]
    public async Task<IActionResult> ReleasePayment(int payoutId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var payout = await _paymentOrchestrator.ReleasePaymentToInfluencerAsync(payoutId, userId);

            return Ok(new
            {
                message = "Payment released to influencer",
                payout = new
                {
                    payout.Id,
                    payout.GrossAmountInPence,
                    payout.PlatformFeeInPence,
                    payout.NetAmountInPence,
                    payout.Status,
                    payout.ReleasedAt
                }
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Manually trigger auto-pay processing for testing purposes
    /// This endpoint allows manual testing of the auto-pay background service
    /// </summary>
    [HttpPost("trigger-autopay")]
    [Authorize]
    public async Task<IActionResult> TriggerAutoPay([FromQuery] int? campaignId = null)
    {
        try
        {
            _logger.LogInformation("Manual auto-pay trigger requested by user {UserId}, CampaignId: {CampaignId}",
                GetCurrentUserId(), campaignId);

            var result = await _paymentOrchestrator.TriggerAutoPayProcessingAsync(campaignId);

            return Ok(new
            {
                success = true,
                message = "Auto-pay processing completed",
                processedCount = result.ProcessedCount,
                errorCount = result.ErrorCount,
                reminderCount = result.ReminderCount,
                details = result.Details
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual auto-pay trigger");
            return BadRequest(new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    /// <summary>
    /// Manually trigger auto-withdrawal for an influencer (for testing)
    /// </summary>
    [HttpPost("trigger-auto-withdrawal/{milestoneId}")]
    [Authorize]
    public async Task<IActionResult> TriggerAutoWithdrawal(int milestoneId)
    {
        try
        {
            _logger.LogInformation("Manual auto-withdrawal trigger requested for milestone {MilestoneId}", milestoneId);

            var result = await _paymentOrchestrator.TriggerAutoWithdrawalAsync(milestoneId);

            return Ok(new
            {
                success = result.Success,
                message = result.Message,
                withdrawalId = result.WithdrawalId,
                gateway = result.Gateway,
                status = result.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual auto-withdrawal trigger");
            return BadRequest(new { success = false, message = $"Error: {ex.Message}" });
        }
    }
}

// DTOs
public class InitiatePaymentDto
{
    public int CampaignId { get; set; }
    public string Gateway { get; set; } = string.Empty;
    public int? MilestoneId { get; set; }
    public long? AmountInPence { get; set; }
    public bool SavePaymentMethod { get; set; } = false;
    public string SuccessUrl { get; set; } = string.Empty;
    public string FailureUrl { get; set; } = string.Empty;
}

public class ChargeSavedCardDto
{
    public int MilestoneId { get; set; }
    public int PaymentMethodId { get; set; }
}

public class UpdatePaymentConfigDto
{
    public int PaymentType { get; set; } // 1 = ONE_TIME, 2 = MILESTONE
    public bool IsAutoPayEnabled { get; set; }
}

public class PayFullAmountDto
{
    public int CampaignId { get; set; }
    public string Gateway { get; set; } = "paystack";
    public long AmountInPence { get; set; }
    public bool SavePaymentMethod { get; set; } = false;
    public string SuccessUrl { get; set; } = string.Empty;
    public string FailureUrl { get; set; } = string.Empty;
}
