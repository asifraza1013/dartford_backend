using System.ComponentModel.DataAnnotations;

namespace inflan_api.DTOs;

public class CreateScheduledPostDto
{
    [Required]
    public int CampaignId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [Required]
    public DateTime ScheduledAt { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Select at least one platform")]
    public List<string> Platforms { get; set; } = new();
}

public class UpdateScheduledPostDto
{
    public int? CampaignId { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public List<string>? Platforms { get; set; }

    public int? Status { get; set; }
}
