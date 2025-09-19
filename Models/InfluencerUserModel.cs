namespace inflan_api.Models;

public class InfluencerUserModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? YouTube { get; set; }
    public string? Instagram { get; set; }
    public string? Facebook { get; set; }
    public string? TikTok { get; set; }
    public int YouTubeFollower { get; set; }
    public int InstagramFollower { get; set; }
    public int FacebookFollower { get; set; }
    public int TikTokFollower { get; set; }
    public string? Bio { get; set; } = "";
    public User? User { get; set; }
}