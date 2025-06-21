using System.ComponentModel.DataAnnotations;

namespace dartford_api.Models;

public class UpdateModel
{
    public string? Name { get; set; }
    public string? UserName { get; set; }
    [EmailAddress]
    public string? Email { get; set; }
    public string? Password { get; set; } 
    public string? Bio {  get; set; }
    
}