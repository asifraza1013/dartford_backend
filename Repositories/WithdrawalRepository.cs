using inflan_api.DTOs;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using Microsoft.EntityFrameworkCore;

namespace inflan_api.Repositories;

public class WithdrawalRepository : IWithdrawalRepository
{
    private readonly InflanDBContext _context;

    public WithdrawalRepository(InflanDBContext context)
    {
        _context = context;
    }

    public async Task<Withdrawal?> GetByIdAsync(int id)
    {
        return await _context.Withdrawals
            .Include(w => w.Influencer)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<Withdrawal?> GetByTransferCodeAsync(string transferCode)
    {
        return await _context.Withdrawals
            .Include(w => w.Influencer)
            .FirstOrDefaultAsync(w => w.PaystackTransferCode == transferCode);
    }

    public async Task<List<Withdrawal>> GetByInfluencerIdAsync(int influencerId)
    {
        return await _context.Withdrawals
            .Where(w => w.InfluencerId == influencerId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<(List<Withdrawal> Items, int TotalCount)> GetByInfluencerIdFilteredAsync(int influencerId, PaymentFilterDto filter)
    {
        var query = _context.Withdrawals
            .Where(w => w.InfluencerId == influencerId);

        query = ApplyFilters(query, filter);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<Withdrawal>> GetPendingWithdrawalsAsync()
    {
        return await _context.Withdrawals
            .Where(w => w.Status == (int)WithdrawalStatus.PENDING)
            .Include(w => w.Influencer)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<Withdrawal> CreateAsync(Withdrawal withdrawal)
    {
        _context.Withdrawals.Add(withdrawal);
        await _context.SaveChangesAsync();
        return withdrawal;
    }

    public async Task<Withdrawal> UpdateAsync(Withdrawal withdrawal)
    {
        _context.Withdrawals.Update(withdrawal);
        await _context.SaveChangesAsync();
        return withdrawal;
    }

    public async Task<long> GetTotalWithdrawnByInfluencerIdAsync(int influencerId)
    {
        return await _context.Withdrawals
            .Where(w => w.InfluencerId == influencerId &&
                        (w.Status == (int)WithdrawalStatus.COMPLETED ||
                         w.Status == (int)WithdrawalStatus.PROCESSING))
            .SumAsync(w => w.AmountInPence);
    }

    private static IQueryable<Withdrawal> ApplyFilters(IQueryable<Withdrawal> query, PaymentFilterDto filter)
    {
        if (filter.DateFrom.HasValue)
            query = query.Where(w => w.CreatedAt >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(w => w.CreatedAt <= filter.DateTo.Value);

        if (filter.MinAmount.HasValue)
            query = query.Where(w => w.AmountInPence >= filter.MinAmount.Value);

        if (filter.MaxAmount.HasValue)
            query = query.Where(w => w.AmountInPence <= filter.MaxAmount.Value);

        if (filter.Status.HasValue)
            query = query.Where(w => w.Status == filter.Status.Value);

        return query;
    }
}
