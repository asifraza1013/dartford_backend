using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;

namespace inflan_api.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly InflanDBContext _context;

    public TransactionRepository(InflanDBContext context)
    {
        _context = context;
    }

    public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
    {
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    public async Task UpdateTransactionAsync(Transaction transaction)
    {
        _context.Transactions.Update(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task<Transaction?> GetTransactionByIdAsync(int id)
    {
        return await _context.Transactions.FindAsync(id);
    }
}