using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace inflan_api.Models;

public class Plan
{
    [Key]
    public int Id { get; set; }
    public string? PlanName { get; set; }
    public string? Currency { get; set; }
    public float Price { get; set; }

    /// <summary>Duration unit: "week" or "month". Null/empty is treated as "month" for
    /// plans created before this field existed.</summary>
    public string? Interval { get; set; }

    /// <summary>The duration's numeric value in whatever unit <see cref="Interval"/> is
    /// (e.g. 2 weeks, or 6 months) — not necessarily literal calendar months.</summary>
    public int NumberOfMonths { get; set; }
    public List<string>? PlanDetails { get; set; }
    public int Status { get; set; }
    public int UserId { get; set; }
}