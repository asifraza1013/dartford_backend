﻿using System.ComponentModel.DataAnnotations;
using System.Security;

namespace inflan_api.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? UserName { get; set; }
        [EmailAddress]
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? BrandName { get; set; }
        public string? BrandCategory { get; set; }
        public string? BrandSector { get; set; }
        public List<string>? Goals { get; set; }
        public int UserType { get; set; }
        public string? ProfileImage { get; set; }
        public int Status { get; set; }

    }
}
