using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.Utils;
using Stripe;

namespace inflan_api.Services;

public class PaymentService : IPaymentService
{
    private readonly ITransactionRepository _transactionRepo;
    private readonly ICampaignService _campaignService;
    
    public PaymentService(ITransactionRepository transactionRepo, IConfiguration configuration,  ICampaignService campaignService)
    {
        _transactionRepo = transactionRepo;
        _campaignService = campaignService;
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
    }

    public async Task<Transaction> ProcessPaymentAsync(int userId, float amount, string currency, string paymentMethodId, int campaignId)
    {
        var transaction = new Transaction
        {
            UserId = userId,
            Amount = amount,
            Currency = currency,
            TransactionStatus = (int)PaymentStatus.PENDING,
            CreatedAt = DateTime.UtcNow,
            TransactionId = Guid.NewGuid().ToString(),
            CampaignId = campaignId
        };

        transaction = await _transactionRepo.CreateTransactionAsync(transaction);

        var paymentIntentService = new PaymentIntentService();
        try
        {
            var paymentIntent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
            {
                Amount = (long)(amount),
                Currency = currency,
                PaymentMethod = paymentMethodId,
                Confirm = true,
                Metadata = new Dictionary<string, string>
                {
                    { "TransactionId", transaction.TransactionId },
                    { "UserId", userId.ToString() }
                }
            });

            transaction.StripePaymentIntentId = paymentIntent.Id;

            if (paymentIntent.Status == "succeeded")
            {
                transaction.TransactionStatus = (int)PaymentStatus.COMPLETED;
                transaction.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                transaction.TransactionStatus = (int)PaymentStatus.FAILED;
                transaction.FailureMessage = "Payment not succeeded.";
            }
            await _campaignService.UpdateCampaign(transaction.CampaignId, new Campaign{PaymentStatus = paymentIntent.Status == "succeeded" ? 2 : 3});
            
        }
        catch (StripeException ex)
        {
            transaction.TransactionStatus = (int)PaymentStatus.FAILED;
            transaction.FailureMessage = ex.StripeError.Message;
        }

        await _transactionRepo.UpdateTransactionAsync(transaction);
        return transaction;
    }
}