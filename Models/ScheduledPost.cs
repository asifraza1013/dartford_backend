using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace inflan_api.Models;

public class ScheduledPost
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int InfluencerId { get; set; }

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

    // One or more of: instagram, youtube, tiktok, facebook.
    public List<string> Platforms { get; set; } = new();

    // 1 = scheduled (default), 2 = posted, 3 = cancelled.
    public int Status { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Set by ScheduledPostReminderBackgroundService once the "post goes live
    // soon" reminder has been dispatched, so each post is reminded exactly
    // once even if the sweep runs every 5 minutes.
    public DateTime? ReminderSentAt { get; set; }

    [ForeignKey("CampaignId")]
    public virtual Campaign? Campaign { get; set; }
}
