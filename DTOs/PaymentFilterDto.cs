namespace inflan_api.DTOs;

public class PaymentFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public long? MinAmount { get; set; }
    public long? MaxAmount { get; set; }
    public int? CampaignId { get; set; }
    public int? Status { get; set; }
}
