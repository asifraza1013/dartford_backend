using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface IPaymentMethodRepository
{
    Task<PaymentMethod?> GetByIdAsync(int id);
    Task<List<PaymentMethod>> GetByUserIdAsync(int userId);
    Task<PaymentMethod?> GetDefaultByUserIdAsync(int userId);
    Task<PaymentMethod?> GetByAuthorizationCodeAsync(string authorizationCode);
    Task<PaymentMethod> CreateAsync(PaymentMethod paymentMethod);
    Task<PaymentMethod> UpdateAsync(PaymentMethod paymentMethod);
    Task DeleteAsync(int id);
    Task SetDefaultAsync(int userId, int paymentMethodId);
}
