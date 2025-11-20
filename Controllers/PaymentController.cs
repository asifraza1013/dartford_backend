using inflan_api.Interfaces;
using inflan_api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace inflan_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ICampaignService _campaignService;

        public PaymentController(IPaymentService paymentService, ICampaignService campaignService)
        {
            _paymentService = paymentService;
            _campaignService = campaignService;
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

    }
}
