using System.ComponentModel.DataAnnotations;

namespace inflan_api.Models
{
    public class Influencer
    {
        [Key]
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? Twitter {  get; set; }
        public string? Instagram { get; set; }
        public string? Facebook { get; set; }
        public string? TikTok { get; set; }
        public int TwitterFollower { get; set; }
        public int InstagramFollower { get; set; }
        public int FacebookFollower { get; set; }
        public int TikTokFollower { get; set; }
        public string? Bio { get; set; } = "";
        public User? User { get; set; }

    }
}
