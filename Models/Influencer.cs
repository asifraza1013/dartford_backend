using System.ComponentModel.DataAnnotations;

namespace dartford_api.Models
{
    public class Influencer
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int UserId { get; set; }
        [Required]
        public string Twitter {  get; set; }
        [Required]
        public string Instagram { get; set; }
        [Required]
        public string Facebook { get; set; }
        [Required]
        public string TikTok { get; set; }
        public string TwitterFollower { get; set; }
        public string InstagramFollower { get; set; }
        public string FacebookFollower { get; set; }
        public string TikTokFollower { get; set; }
        public string Bio {  get; set; }

    }
}
