using inflan_api.DTOs;
using inflan_api.Interfaces;
using inflan_api.Services.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace inflan_api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class InvoiceController : ControllerBase
{
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly IInvoicePdfService _pdfService;
    private readonly ILogger<InvoiceController> _logger;

    public InvoiceController(
        IInvoiceRepository invoiceRepo,
        IInvoicePdfService pdfService,
        ILogger<InvoiceController> logger)
    {
        _invoiceRepo = invoiceRepo;
        _pdfService = pdfService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("id")?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    /// <summary>
    /// Get invoices for a campaign
    /// </summary>
    [HttpGet("campaign/{campaignId}")]
    public async Task<IActionResult> GetCampaignInvoices(int campaignId)
    {
        var invoices = await _invoiceRepo.GetByCampaignIdAsync(campaignId);
        return Ok(invoices.Select(i => new
        {
            i.Id,
            i.InvoiceNumber,
            i.CampaignId,
            i.MilestoneId,
            i.SubtotalInPence,
            i.PlatformFeeInPence,
            i.TotalAmountInPence,
            i.Currency,
            i.Status,
            i.IssuedAt,
            i.PaidAt,
            i.PdfPath
        }));
    }

    /// <summary>
    /// Get brand's payment history (invoices) with optional filters
    /// </summary>
    [HttpGet("brand/history")]
    public async Task<IActionResult> GetBrandHistory(
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

        var (invoices, totalCount) = await _invoiceRepo.GetByBrandIdFilteredAsync(userId, filter);

        return Ok(new
        {
            items = invoices.Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.CampaignId,
                campaignName = i.Campaign?.ProjectName,
                influencerName = i.Influencer?.Name,
                i.MilestoneId,
                milestoneNumber = i.Milestone?.MilestoneNumber,
                i.SubtotalInPence,
                i.PlatformFeeInPence,
                i.TotalAmountInPence,
                i.Currency,
                i.Status,
                i.IssuedAt,
                i.PaidAt,
                i.PdfPath
            }),
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Get influencer's payment history (incoming) with optional filters
    /// </summary>
    [HttpGet("influencer/history")]
    public async Task<IActionResult> GetInfluencerHistory(
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

        var (invoices, totalCount) = await _invoiceRepo.GetByInfluencerIdFilteredAsync(userId, filter);

        return Ok(new
        {
            items = invoices.Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.CampaignId,
                campaignName = i.Campaign?.ProjectName,
                brandName = i.Brand?.BrandName ?? i.Brand?.Name,
                i.MilestoneId,
                milestoneNumber = i.Milestone?.MilestoneNumber,
                i.SubtotalInPence,
                i.PlatformFeeInPence,
                i.TotalAmountInPence,
                i.Currency,
                i.Status,
                i.IssuedAt,
                i.PaidAt,
                i.PdfPath
            }),
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Download invoice PDF
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadInvoice(int id)
    {
        try
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id);
            if (invoice == null)
                return NotFound(new { message = "Invoice not found" });

            var userId = GetCurrentUserId();
            if (invoice.BrandId != userId && invoice.InfluencerId != userId)
                return Forbid();

            // Generate PDF on the fly
            var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(invoice);

            return File(pdfBytes, "application/pdf", $"{invoice.InvoiceNumber}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating invoice PDF for invoice {InvoiceId}", id);
            return StatusCode(500, new { message = "Failed to generate invoice PDF" });
        }
    }

    /// <summary>
    /// Get single invoice details
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetInvoice(int id)
    {
        var invoice = await _invoiceRepo.GetByIdAsync(id);
        if (invoice == null)
            return NotFound(new { message = "Invoice not found" });

        var userId = GetCurrentUserId();
        if (invoice.BrandId != userId && invoice.InfluencerId != userId)
            return Forbid();

        return Ok(new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.CampaignId,
            campaignName = invoice.Campaign?.ProjectName,
            brandId = invoice.BrandId,
            brandName = invoice.Brand?.BrandName ?? invoice.Brand?.Name,
            influencerId = invoice.InfluencerId,
            influencerName = invoice.Influencer?.Name,
            invoice.MilestoneId,
            milestoneNumber = invoice.Milestone?.MilestoneNumber,
            invoice.TransactionId,
            invoice.SubtotalInPence,
            invoice.PlatformFeeInPence,
            invoice.TotalAmountInPence,
            invoice.Currency,
            invoice.Status,
            invoice.IssuedAt,
            invoice.PaidAt,
            invoice.PdfPath
        });
    }
}
