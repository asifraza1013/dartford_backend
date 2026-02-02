using System.ComponentModel.DataAnnotations;
using System.Security;

namespace inflan_api.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage = "Name is required")]
        public string? Name { get; set; }
        public string? UserName { get; set; }
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string? Email { get; set; }
        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string? Password { get; set; }
        public string? BrandName { get; set; }
        public string? BrandCategory { get; set; }
        public string? BrandSector { get; set; }
        public List<string>? Goals { get; set; }
        [Required(ErrorMessage = "User type is required")]
        [Range(2, 3, ErrorMessage = "User type must be either 2 (Brand) or 3 (Influencer)")]
        public int UserType { get; set; }
        public string? ProfileImage { get; set; }
        public int Status { get; set; }

        /// <summary>
        /// Currency preference for the user (NGN or GBP). Derived from Location.
        /// </summary>
        public string? Currency { get; set; } = "NGN";

        /// <summary>
        /// User's location (NG for Nigeria, GB for United Kingdom).
        /// This determines the currency and payment gateway used.
        /// </summary>
        public string? Location { get; set; } = "NG";

        /// <summary>
        /// Stripe Customer ID (cus_xxx) for UK-based users.
        /// Used for storing payment methods and processing recurring payments.
        /// </summary>
        [MaxLength(255)]
        public string? StripeCustomerId { get; set; }

    }
}
