using System.ComponentModel.DataAnnotations;

namespace inflan_api.DTOs
{
    public class UpdateUserDto
    {
        // All fields are optional for updates
        public string? Name { get; set; }

        public string? UserName { get; set; }

        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string? Email { get; set; }

        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string? Password { get; set; }

        public string? BrandName { get; set; }

        public string? BrandCategory { get; set; }

        public string? BrandSector { get; set; }

        public List<string>? Goals { get; set; }

        // UserType and Status are optional for updates
        // If provided as 0, they won't be updated
        public int? UserType { get; set; }

        public int? Status { get; set; }

        public string? ProfileImage { get; set; }
    }
}
