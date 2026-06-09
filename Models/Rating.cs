using System.ComponentModel.DataAnnotations;

namespace inflan_api.Models
{
    /// <summary>
    /// A 1–5 star rating one party leaves for the other on a completed campaign.
    /// Brands rate influencers and influencers rate brands, so RateeId can be either.
    /// </summary>
    public class Rating
    {
        [Key]
        public int Id { get; set; }

        public int CampaignId { get; set; }
        public Campaign? Campaign { get; set; }

        /// <summary>User who gave the rating.</summary>
        public int RaterId { get; set; }

        /// <summary>User being rated (the influencer or the brand).</summary>
        public int RateeId { get; set; }

        /// <summary>UserType of the ratee (2 = Brand, 3 = Influencer) — denormalised for fast reporting.</summary>
        public int RateeUserType { get; set; }

        [Range(1, 5)]
        public int Stars { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
