using System.ComponentModel.DataAnnotations;

namespace inflan_api.Models;

public class PlatformSettings
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string SettingKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string SettingValue { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
