using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace inflan_api.Models;

public class Plan
{
    [Key]
    public int Id { get; set; }
    public string? PlanName { get; set; }
    public string? Currency { get; set; }
    public string? Interval { get; set; }
    public float Price { get; set; }
    public int NumberOfMonths { get; set; }
    public List<string>? PlanDetails { get; set; }
    public int Status { get; set; }
    public int UserId { get; set; }
}