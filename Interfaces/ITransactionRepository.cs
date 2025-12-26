using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction> CreateTransactionAsync(Transaction transaction);
    Task UpdateTransactionAsync(Transaction transaction);
    Task<Transaction?> GetTransactionByIdAsync(int id);
    Task<Transaction?> GetByTransactionReferenceAsync(string transactionReference);
    Task<List<Transaction>> GetByCampaignIdAsync(int campaignId);
    Task<List<Transaction>> GetByUserIdAsync(int userId, int page = 1, int pageSize = 20);
}