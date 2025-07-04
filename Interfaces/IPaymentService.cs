using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface IPaymentService
{
    Task<Transaction> ProcessPaymentAsync(int userId, float amount, string currency, string paymentMethodId, int campaignId);
}