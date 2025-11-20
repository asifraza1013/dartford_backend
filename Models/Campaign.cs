using System.ComponentModel.DataAnnotations;

namespace inflan_api.Models;

public class Campaign
{
    [Key]
    public int Id { get; set; }

    public int PlanId { get; set; }

    // Screen 2 Fields - Project Details
    [Required]
    public string ProjectName { get; set; } = string.Empty;

    public string? AboutProject { get; set; }

    public DateOnly CampaignStartDate { get; set; }

    public DateOnly CampaignEndDate { get; set; }

    // Content files (images, documents for the campaign brief)
    public List<string>? ContentFiles { get; set; }

    // Instruction documents (campaign brief documents)
    public List<string>? InstructionDocuments { get; set; }

    // Campaign Relations
    public int BrandId { get; set; }

    public int InfluencerId { get; set; }

    // Status Management
    public int CampaignStatus { get; set; } = 1; // DRAFT by default

    public int PaymentStatus { get; set; } = 1; // PENDING by default

    // Contract Management
    public string? GeneratedContractPdfPath { get; set; }

    public string? SignedContractPdfPath { get; set; }

    public DateTime? ContractSignedAt { get; set; }

    public DateTime? SignatureApprovedAt { get; set; }

    // Pricing
    public string? Currency { get; set; }

    public float Amount { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? InfluencerAcceptedAt { get; set; }

    public DateTime? PaymentCompletedAt { get; set; }

    // Legacy field for backward compatibility
    [Obsolete("Use ProjectName instead")]
    public string? CampaignName { get; set; }
}