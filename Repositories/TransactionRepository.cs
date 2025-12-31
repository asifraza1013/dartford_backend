using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using Microsoft.EntityFrameworkCore;

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

    public async Task<Transaction?> GetByTransactionReferenceAsync(string transactionReference)
    {
        return await _context.Transactions
            .Include(t => t.Campaign)
            .Include(t => t.Milestone)
            .Include(t => t.PaymentMethod)
            .FirstOrDefaultAsync(t => t.TransactionReference == transactionReference);
    }

    public async Task<Transaction?> GetByGatewayPaymentIdAsync(string gatewayPaymentId)
    {
        return await _context.Transactions
            .Include(t => t.Campaign)
            .Include(t => t.Milestone)
            .Include(t => t.PaymentMethod)
            .FirstOrDefaultAsync(t => t.GatewayPaymentId == gatewayPaymentId);
    }

    public async Task<List<Transaction>> GetByCampaignIdAsync(int campaignId)
    {
        return await _context.Transactions
            .Include(t => t.Milestone)
            .Where(t => t.CampaignId == campaignId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Transaction>> GetByUserIdAsync(int userId, int page = 1, int pageSize = 20)
    {
        return await _context.Transactions
            .Include(t => t.Campaign)
            .Include(t => t.Milestone)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}