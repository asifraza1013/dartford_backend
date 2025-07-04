using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction> CreateTransactionAsync(Transaction transaction);
    Task UpdateTransactionAsync(Transaction transaction);
    Task<Transaction?> GetTransactionByIdAsync(int id);
}