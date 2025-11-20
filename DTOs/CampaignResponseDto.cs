namespace inflan_api.DTOs;

public class CampaignResponseDto
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? AboutProject { get; set; }
    public DateOnly CampaignStartDate { get; set; }
    public DateOnly CampaignEndDate { get; set; }
    public List<string>? ContentFiles { get; set; }
    public List<string>? InstructionDocuments { get; set; }
    public int BrandId { get; set; }
    public string? BrandName { get; set; }
    public string? BrandLogo { get; set; }
    public int InfluencerId { get; set; }
    public int? InfluencerRecordId { get; set; }
    public string? InfluencerName { get; set; }
    public int CampaignStatus { get; set; }
    public int PaymentStatus { get; set; }
    public string? GeneratedContractPdfPath { get; set; }
    public string? SignedContractPdfPath { get; set; }
    public DateTime? ContractSignedAt { get; set; }
    public string? Currency { get; set; }
    public float Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? InfluencerAcceptedAt { get; set; }
    public DateTime? PaymentCompletedAt { get; set; }
}
