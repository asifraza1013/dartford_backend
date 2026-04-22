using System.ComponentModel.DataAnnotations;

namespace inflan_api.Models
{
    public class ForgotPasswordModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public required string Email { get; set; }
    }

    public class ResetPasswordModel
    {
        [Required(ErrorMessage = "Token is required")]
        public required string Token { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public required string Password { get; set; }
    }
}
