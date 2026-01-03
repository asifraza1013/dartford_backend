using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface IInfluencerBankAccountRepository
{
    Task<InfluencerBankAccount?> GetByIdAsync(int id);
    Task<List<InfluencerBankAccount>> GetByInfluencerIdAsync(int influencerId);
    Task<InfluencerBankAccount?> GetDefaultByInfluencerIdAsync(int influencerId);
    Task<InfluencerBankAccount?> GetDefaultByInfluencerIdAndCurrencyAsync(int influencerId, string currency);
    Task<InfluencerBankAccount?> GetByRecipientCodeAsync(string recipientCode);
    Task<InfluencerBankAccount> CreateAsync(InfluencerBankAccount account);
    Task<InfluencerBankAccount> UpdateAsync(InfluencerBankAccount account);
    Task DeleteAsync(int id);
    Task SetDefaultAsync(int influencerId, int accountId);
}
