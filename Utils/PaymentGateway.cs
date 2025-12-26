namespace inflan_api.Utils;

public enum PaymentGateway
{
    TRUELAYER = 1,
    PAYSTACK = 2
}

public static class PaymentGatewayExtensions
{
    public static string ToGatewayString(this PaymentGateway gateway)
    {
        return gateway switch
        {
            PaymentGateway.TRUELAYER => "truelayer",
            PaymentGateway.PAYSTACK => "paystack",
            _ => throw new ArgumentOutOfRangeException(nameof(gateway))
        };
    }

    public static PaymentGateway FromString(string gateway)
    {
        return gateway.ToLower() switch
        {
            "truelayer" => PaymentGateway.TRUELAYER,
            "paystack" => PaymentGateway.PAYSTACK,
            _ => throw new ArgumentException($"Unknown payment gateway: {gateway}")
        };
    }
}
