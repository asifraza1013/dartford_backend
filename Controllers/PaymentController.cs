using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.Services.Payment;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace inflan_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ICampaignService _campaignService;
        private readonly PaystackGateway _paystackGateway;
        private readonly IWithdrawalRepository _withdrawalRepo;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentService paymentService,
            ICampaignService campaignService,
            PaystackGateway paystackGateway,
            IWithdrawalRepository withdrawalRepo,
            ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _campaignService = campaignService;
            _paystackGateway = paystackGateway;
            _withdrawalRepo = withdrawalRepo;
            _logger = logger;
        }

        [HttpPost("charge")]
        public async Task<IActionResult> Charge([FromBody] PaymentRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(new {
                message = "Invalid payment request",
                code = "INVALID_PAYMENT_REQUEST"
            });
            var campaign = await _campaignService.GetCampaignById(request.CampaignId);
            if (campaign == null) return NotFound(new {
                message = "Campaign not found",
                code = "CAMPAIGN_NOT_FOUND"
            });

            // Verify campaign is in correct state for payment (awaiting payment after contract signed)
            if (campaign.CampaignStatus != 4) // AWAITING_PAYMENT
            {
                return BadRequest(new {
                    message = "Campaign is not ready for payment. Please ensure the contract has been signed.",
                    code = "CAMPAIGN_NOT_READY_FOR_PAYMENT"
                });
            }

            var transaction = await _paymentService.ProcessPaymentAsync(
                campaign.BrandId,
                campaign.Amount,
                campaign.Currency,
                request.PaymentMethodId,
                campaign.Id
            );

            if (transaction == null)
                return StatusCode(500, new {
                    message = "Payment processing failed. Please try again.",
                    code = "PAYMENT_FAILED"
                });

            // Activate the campaign after successful payment
            var (success, message) = await _campaignService.ActivateCampaignAfterPaymentAsync(campaign.Id);

            if (!success)
            {
                // Payment succeeded but campaign activation failed - log this as it needs attention
                return Ok(new {
                    message = "Payment processed successfully, but campaign activation encountered an issue. Please contact support.",
                    transaction = transaction,
                    code = "PAYMENT_SUCCESS_ACTIVATION_ISSUE",
                    activationError = message
                });
            }

            return Ok(new {
                message = "Payment processed successfully and campaign activated!",
                transaction = transaction,
                code = "PAYMENT_SUCCESS_CAMPAIGN_ACTIVE"
            });
        }

        /// <summary>
        /// Paystack webhook handler for payment and transfer events
        /// </summary>
        [HttpPost("webhook/paystack")]
        public async Task<IActionResult> PaystackWebhook()
        {
            try
            {
                // Read the raw body
                using var reader = new StreamReader(Request.Body);
                var payload = await reader.ReadToEndAsync();

                // Get signature from header
                var signature = Request.Headers["x-paystack-signature"].FirstOrDefault();

                _logger.LogInformation("Received Paystack webhook. Event payload length: {Length}", payload.Length);

                // Validate signature
                if (!_paystackGateway.ValidateWebhookSignature(payload, signature))
                {
                    _logger.LogWarning("Invalid Paystack webhook signature");
                    return Unauthorized();
                }

                // Parse the event
                var webhookEvent = JsonSerializer.Deserialize<PaystackWebhookEvent>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (webhookEvent == null)
                {
                    _logger.LogWarning("Failed to parse Paystack webhook payload");
                    return BadRequest();
                }

                _logger.LogInformation("Paystack webhook event: {Event}", webhookEvent.Event);

                // Handle transfer events
                if (webhookEvent.Event?.StartsWith("transfer.") == true)
                {
                    await HandleTransferWebhookAsync(webhookEvent);
                }
                // Handle charge events (for payments)
                else if (webhookEvent.Event?.StartsWith("charge.") == true)
                {
                    // Existing charge handling logic can go here
                    _logger.LogInformation("Received charge event: {Event}", webhookEvent.Event);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Paystack webhook");
                return StatusCode(500);
            }
        }

        private async Task HandleTransferWebhookAsync(PaystackWebhookEvent webhookEvent)
        {
            var data = webhookEvent.Data;
            if (data == null)
            {
                _logger.LogWarning("Transfer webhook has no data");
                return;
            }

            // Extract transfer code from the event
            var transferCode = data.TransferCode;
            if (string.IsNullOrEmpty(transferCode))
            {
                _logger.LogWarning("Transfer webhook has no transfer code");
                return;
            }

            // Find the withdrawal by transfer code
            var withdrawal = await _withdrawalRepo.GetByTransferCodeAsync(transferCode);
            if (withdrawal == null)
            {
                _logger.LogWarning("No withdrawal found for transfer code: {TransferCode}", transferCode);
                return;
            }

            _logger.LogInformation("Processing transfer webhook for withdrawal {WithdrawalId}, event: {Event}",
                withdrawal.Id, webhookEvent.Event);

            switch (webhookEvent.Event)
            {
                case "transfer.success":
                    withdrawal.Status = (int)WithdrawalStatus.COMPLETED;
                    withdrawal.CompletedAt = DateTime.UtcNow;
                    _logger.LogInformation("Withdrawal {WithdrawalId} completed successfully via webhook", withdrawal.Id);
                    break;

                case "transfer.failed":
                    withdrawal.Status = (int)WithdrawalStatus.FAILED;
                    withdrawal.FailureReason = data.Reason ?? "Transfer failed";
                    _logger.LogWarning("Withdrawal {WithdrawalId} failed via webhook: {Reason}",
                        withdrawal.Id, withdrawal.FailureReason);
                    break;

                case "transfer.reversed":
                    withdrawal.Status = (int)WithdrawalStatus.FAILED;
                    withdrawal.FailureReason = "Transfer was reversed";
                    _logger.LogWarning("Withdrawal {WithdrawalId} was reversed", withdrawal.Id);
                    break;

                default:
                    _logger.LogInformation("Unhandled transfer event: {Event}", webhookEvent.Event);
                    return;
            }

            await _withdrawalRepo.UpdateAsync(withdrawal);
        }
    }

    // Paystack webhook event DTO
    public class PaystackWebhookEvent
    {
        public string? Event { get; set; }
        public PaystackWebhookData? Data { get; set; }
    }

    public class PaystackWebhookData
    {
        public string? TransferCode { get; set; }
        public string? Reference { get; set; }
        public string? Status { get; set; }
        public string? Reason { get; set; }
        public long Amount { get; set; }
        public string? Currency { get; set; }
    }
}
