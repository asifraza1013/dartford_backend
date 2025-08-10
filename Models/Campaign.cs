using System.ComponentModel.DataAnnotations;

namespace inflan_api.Models;

public class Campaign
{
    [Key]
    public int Id { get; set; }

    public int PlanId { get; set; }

    public string? CampaignName { get; set; }

    public DateOnly CampaignStartDate { get; set; }

    public DateOnly CampaignEndDate { get; set; }

    public int BrandId { get; set; }

    public List<string>? InstructionDocuments { get; set; }

    public int CampaignStatus { get; set; } = 1;

    public int InfluencerId { get; set; }

    public string? Currency { get; set; }

    public float Amount { get; set; }

    public int PaymentStatus { get; set; } = 1;
}