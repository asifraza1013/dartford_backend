using System.ComponentModel.DataAnnotations;

namespace inflan_api.DTOs
{
    public class CheckEmailRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "User type is required")]
        [Range(2, 3, ErrorMessage = "User type must be either 2 (Brand) or 3 (Influencer)")]
        public int UserType { get; set; }

        public string? BrandName { get; set; }
        public string? Location { get; set; }
        public string? Currency { get; set; }
    }

    public class SendVerificationRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;
    }

    public class VerifyEmailRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Verification code is required")]
        [RegularExpression("^[0-9]{6}$", ErrorMessage = "Verification code must be 6 digits")]
        public string Code { get; set; } = string.Empty;
    }
}
