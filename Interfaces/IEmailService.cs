namespace inflan_api.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Sends email notification to brand requesting contract signature
    /// </summary>
    Task SendContractSignatureRequestAsync(string brandEmail, string brandName, int campaignId, string contractUrl);

    /// <summary>
    /// Sends email notification to brand requesting payment after contract is signed
    /// </summary>
    Task SendPaymentRequestAsync(string brandEmail, string brandName, int campaignId, decimal amount, string currency);

    /// <summary>
    /// Sends email notification to influencer when campaign is accepted and activated
    /// </summary>
    Task SendCampaignActivatedAsync(string influencerEmail, string influencerName, int campaignId, string projectName);

    /// <summary>
    /// Sends email notification to brand when influencer accepts/rejects campaign
    /// </summary>
    Task SendInfluencerResponseNotificationAsync(string brandEmail, string brandName, int campaignId, string projectName, bool accepted, string? contractPdfPath = null);

    /// <summary>
    /// Sends email notification to influencer when a new campaign booking is created
    /// </summary>
    Task SendNewCampaignNotificationAsync(string influencerEmail, string influencerName, int campaignId, string projectName, string brandName);
}
