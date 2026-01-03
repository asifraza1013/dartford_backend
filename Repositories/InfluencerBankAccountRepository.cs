using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using Microsoft.EntityFrameworkCore;

namespace inflan_api.Repositories;

public class InfluencerBankAccountRepository : IInfluencerBankAccountRepository
{
    private readonly InflanDBContext _context;

    public InfluencerBankAccountRepository(InflanDBContext context)
    {
        _context = context;
    }

    public async Task<InfluencerBankAccount?> GetByIdAsync(int id)
    {
        return await _context.InfluencerBankAccounts
            .Include(a => a.Influencer)
            .FirstOrDefaultAsync(a => a.Id == id && a.IsActive);
    }

    public async Task<List<InfluencerBankAccount>> GetByInfluencerIdAsync(int influencerId)
    {
        return await _context.InfluencerBankAccounts
            .Where(a => a.InfluencerId == influencerId && a.IsActive)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<InfluencerBankAccount?> GetDefaultByInfluencerIdAsync(int influencerId)
    {
        return await _context.InfluencerBankAccounts
            .FirstOrDefaultAsync(a => a.InfluencerId == influencerId && a.IsDefault && a.IsActive);
    }

    public async Task<InfluencerBankAccount?> GetDefaultByInfluencerIdAndCurrencyAsync(int influencerId, string currency)
    {
        // First try to get default account for this currency
        var defaultAccount = await _context.InfluencerBankAccounts
            .FirstOrDefaultAsync(a => a.InfluencerId == influencerId &&
                                      a.Currency == currency &&
                                      a.IsDefault &&
                                      a.IsActive);

        if (defaultAccount != null)
            return defaultAccount;

        // If no default for this currency, get any active account for this currency
        return await _context.InfluencerBankAccounts
            .FirstOrDefaultAsync(a => a.InfluencerId == influencerId &&
                                      a.Currency == currency &&
                                      a.IsActive);
    }

    public async Task<InfluencerBankAccount?> GetByRecipientCodeAsync(string recipientCode)
    {
        return await _context.InfluencerBankAccounts
            .FirstOrDefaultAsync(a => a.PaystackRecipientCode == recipientCode && a.IsActive);
    }

    public async Task<InfluencerBankAccount> CreateAsync(InfluencerBankAccount account)
    {
        _context.InfluencerBankAccounts.Add(account);
        await _context.SaveChangesAsync();
        return account;
    }

    public async Task<InfluencerBankAccount> UpdateAsync(InfluencerBankAccount account)
    {
        account.UpdatedAt = DateTime.UtcNow;
        _context.InfluencerBankAccounts.Update(account);
        await _context.SaveChangesAsync();
        return account;
    }

    public async Task DeleteAsync(int id)
    {
        var account = await _context.InfluencerBankAccounts.FindAsync(id);
        if (account != null)
        {
            // Soft delete
            account.IsActive = false;
            account.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task SetDefaultAsync(int influencerId, int accountId)
    {
        // Remove default from all accounts for this influencer
        var accounts = await _context.InfluencerBankAccounts
            .Where(a => a.InfluencerId == influencerId && a.IsActive)
            .ToListAsync();

        foreach (var account in accounts)
        {
            account.IsDefault = account.Id == accountId;
            account.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}
