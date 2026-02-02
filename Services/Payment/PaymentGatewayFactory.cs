using inflan_api.Interfaces;
using inflan_api.Utils;

namespace inflan_api.Services.Payment;

public interface IPaymentGatewayFactory
{
    IPaymentGateway GetGateway(string gatewayName);
    IPaymentGateway GetGateway(PaymentGateway gateway);
}

public class PaymentGatewayFactory : IPaymentGatewayFactory
{
    private readonly IServiceProvider _serviceProvider;

    public PaymentGatewayFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IPaymentGateway GetGateway(string gatewayName)
    {
        return gatewayName.ToLower() switch
        {
            "truelayer" => _serviceProvider.GetRequiredService<TrueLayerGateway>(),
            "paystack" => _serviceProvider.GetRequiredService<PaystackGateway>(),
            "stripe" => _serviceProvider.GetRequiredService<StripeGateway>(),
            _ => throw new ArgumentException($"Unknown payment gateway: {gatewayName}")
        };
    }

    public IPaymentGateway GetGateway(PaymentGateway gateway)
    {
        return gateway switch
        {
            Utils.PaymentGateway.TRUELAYER => _serviceProvider.GetRequiredService<TrueLayerGateway>(),
            Utils.PaymentGateway.PAYSTACK => _serviceProvider.GetRequiredService<PaystackGateway>(),
            Utils.PaymentGateway.STRIPE => _serviceProvider.GetRequiredService<StripeGateway>(),
            _ => throw new ArgumentException($"Unknown payment gateway: {gateway}")
        };
    }
}
