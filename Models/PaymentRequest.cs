using System.ComponentModel.DataAnnotations;

namespace inflan_api.Models;

public class PaymentRequest
{
    [Required]
    public int CampaignId { get; set; }
    [Required]
    public string PaymentMethodId { get; set; }
}