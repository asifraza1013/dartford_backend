using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface IPdfGenerationService
{
    /// <summary>
    /// Generates a contract PDF for a campaign
    /// </summary>
    /// <param name="campaign">Campaign details</param>
    /// <param name="brand">Brand user information</param>
    /// <param name="influencer">Influencer user information</param>
    /// <param name="influencerProfile">Influencer profile with social media details</param>
    /// <param name="plan">Plan details</param>
    /// <returns>Path to the generated PDF file</returns>
    Task<string> GenerateContractPdfAsync(Campaign campaign, User brand, User influencer, Influencer? influencerProfile, Plan plan);
}
