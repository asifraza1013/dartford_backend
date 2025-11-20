using inflan_api.Models;

namespace inflan_api.Interfaces;

public interface ICampaignService
{
    Task<IEnumerable<Campaign>> GetAllCampaigns();
    Task<Campaign?> GetCampaignById(int id);
    Task<Campaign?> CreateCampaign(Campaign campaign);
    Task<bool> UpdateCampaign(int id, Campaign campaign);
    Task<bool> DeleteCampaign(int id);
    Task<IEnumerable<Campaign>> GetCampaignsByInfluencerId(int influencerId);
    Task<IEnumerable<Campaign>> GetCampaignsByBrandId(int brandId);
    Task<IEnumerable<Campaign>> GetCampaignsByInfluencerAndStatus(int influencerId, int campaignStatus);
    Task<List<string>> SaveCampaignDocumentsAsync(List<IFormFile> files);
    Task<List<string>> SaveContentFilesAsync(List<IFormFile> files);
    Task<IEnumerable<Campaign>> GetCompletedPaymentCampaignsByBrandId(int brandId);
    Task<bool> DeleteCampaignDocumentsAsync(List<string> filePaths);

    // New Booking Workflow Methods
    Task<(bool Success, string Message, Campaign? Campaign)> AcceptCampaignAsync(int campaignId, int influencerId);
    Task<(bool Success, string Message)> RejectCampaignAsync(int campaignId, int influencerId);
    Task<(bool Success, string Message)> UploadSignedContractAsync(int campaignId, int brandId, IFormFile signedContract);
    Task<(bool Success, string Message)> ApproveSignedContractAsync(int campaignId, int influencerId);
    Task<(bool Success, string Message)> RejectSignedContractAsync(int campaignId, int influencerId, string? reason = null);
    Task<(bool Success, string Message)> ActivateCampaignAfterPaymentAsync(int campaignId);
}