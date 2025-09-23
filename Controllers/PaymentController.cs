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
            
            return Ok(new { 
                message = "Payment processed successfully",
                transaction = transaction 
            });
        }

    }
}
