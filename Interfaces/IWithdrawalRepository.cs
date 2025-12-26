using inflan_api.DTOs;
using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface IWithdrawalRepository
{
    Task<Withdrawal?> GetByIdAsync(int id);
    Task<Withdrawal?> GetByTransferCodeAsync(string transferCode);
    Task<List<Withdrawal>> GetByInfluencerIdAsync(int influencerId);
    Task<(List<Withdrawal> Items, int TotalCount)> GetByInfluencerIdFilteredAsync(int influencerId, PaymentFilterDto filter);
    Task<List<Withdrawal>> GetPendingWithdrawalsAsync();
    Task<Withdrawal> CreateAsync(Withdrawal withdrawal);
    Task<Withdrawal> UpdateAsync(Withdrawal withdrawal);
    Task<long> GetTotalWithdrawnByInfluencerIdAsync(int influencerId);
}
