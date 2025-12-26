using inflan_api.DTOs;
using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(int id);
    Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber);
    Task<List<Invoice>> GetByCampaignIdAsync(int campaignId);
    Task<List<Invoice>> GetByBrandIdAsync(int brandId, int page = 1, int pageSize = 20);
    Task<List<Invoice>> GetByInfluencerIdAsync(int influencerId, int page = 1, int pageSize = 20);
    Task<(List<Invoice> Items, int TotalCount)> GetByBrandIdFilteredAsync(int brandId, PaymentFilterDto filter);
    Task<(List<Invoice> Items, int TotalCount)> GetByInfluencerIdFilteredAsync(int influencerId, PaymentFilterDto filter);
    Task<Invoice> CreateAsync(Invoice invoice);
    Task<Invoice> UpdateAsync(Invoice invoice);
    Task<string> GenerateInvoiceNumberAsync();
}
