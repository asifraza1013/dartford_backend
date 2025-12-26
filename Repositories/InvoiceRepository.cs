using inflan_api.DTOs;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using Microsoft.EntityFrameworkCore;

namespace inflan_api.Repositories;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly InflanDBContext _context;

    public InvoiceRepository(InflanDBContext context)
    {
        _context = context;
    }

    public async Task<Invoice?> GetByIdAsync(int id)
    {
        return await _context.Invoices
            .Include(i => i.Campaign)
            .Include(i => i.Brand)
            .Include(i => i.Influencer)
            .Include(i => i.Milestone)
            .Include(i => i.Transaction)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber)
    {
        return await _context.Invoices
            .Include(i => i.Campaign)
            .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber);
    }

    public async Task<List<Invoice>> GetByCampaignIdAsync(int campaignId)
    {
        return await _context.Invoices
            .Include(i => i.Milestone)
            .Where(i => i.CampaignId == campaignId)
            .OrderByDescending(i => i.IssuedAt)
            .ToListAsync();
    }

    public async Task<List<Invoice>> GetByBrandIdAsync(int brandId, int page = 1, int pageSize = 20)
    {
        return await _context.Invoices
            .Include(i => i.Campaign)
            .Include(i => i.Influencer)
            .Where(i => i.BrandId == brandId)
            .OrderByDescending(i => i.IssuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<Invoice>> GetByInfluencerIdAsync(int influencerId, int page = 1, int pageSize = 20)
    {
        return await _context.Invoices
            .Include(i => i.Campaign)
            .Include(i => i.Brand)
            .Where(i => i.InfluencerId == influencerId)
            .OrderByDescending(i => i.IssuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<(List<Invoice> Items, int TotalCount)> GetByBrandIdFilteredAsync(int brandId, PaymentFilterDto filter)
    {
        var query = _context.Invoices
            .Include(i => i.Campaign)
            .Include(i => i.Influencer)
            .Include(i => i.Milestone)
            .Where(i => i.BrandId == brandId);

        query = ApplyFilters(query, filter);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.IssuedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(List<Invoice> Items, int TotalCount)> GetByInfluencerIdFilteredAsync(int influencerId, PaymentFilterDto filter)
    {
        var query = _context.Invoices
            .Include(i => i.Campaign)
            .Include(i => i.Brand)
            .Include(i => i.Milestone)
            .Where(i => i.InfluencerId == influencerId);

        query = ApplyFilters(query, filter);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.IssuedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    private static IQueryable<Invoice> ApplyFilters(IQueryable<Invoice> query, PaymentFilterDto filter)
    {
        if (filter.DateFrom.HasValue)
            query = query.Where(i => i.IssuedAt >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(i => i.IssuedAt <= filter.DateTo.Value);

        if (filter.MinAmount.HasValue)
            query = query.Where(i => i.TotalAmountInPence >= filter.MinAmount.Value);

        if (filter.MaxAmount.HasValue)
            query = query.Where(i => i.TotalAmountInPence <= filter.MaxAmount.Value);

        if (filter.CampaignId.HasValue)
            query = query.Where(i => i.CampaignId == filter.CampaignId.Value);

        if (filter.Status.HasValue)
            query = query.Where(i => i.Status == filter.Status.Value);

        return query;
    }

    public async Task<Invoice> CreateAsync(Invoice invoice)
    {
        invoice.CreatedAt = DateTime.UtcNow;
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();
        return invoice;
    }

    public async Task<Invoice> UpdateAsync(Invoice invoice)
    {
        _context.Invoices.Update(invoice);
        await _context.SaveChangesAsync();
        return invoice;
    }

    public async Task<string> GenerateInvoiceNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INV-{year}-";

        // Get the last invoice number for this year
        var lastInvoice = await _context.Invoices
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .FirstOrDefaultAsync();

        int nextNumber = 1;
        if (lastInvoice != null)
        {
            var lastNumberStr = lastInvoice.InvoiceNumber.Replace(prefix, "");
            if (int.TryParse(lastNumberStr, out int lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        return $"{prefix}{nextNumber:D6}"; // e.g., INV-2024-000001
    }
}
