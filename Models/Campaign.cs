using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace inflan_api.Models;

public class Campaign
{
    [Key]
    public int Id { get; set; }

    public int PlanId { get; set; }

    // Screen 2 Fields - Project Details
    [Required]
    public string ProjectName { get; set; } = string.Empty;

    public string? AboutProject { get; set; }

    public DateOnly CampaignStartDate { get; set; }

    public DateOnly CampaignEndDate { get; set; }

    // Content files (images, documents for the campaign brief)
    public List<string>? ContentFiles { get; set; }

    // Instruction documents (campaign brief documents)
    public List<string>? InstructionDocuments { get; set; }

    // Campaign Relations
    public int BrandId { get; set; }
    public User? Brand { get; set; }

    public int InfluencerId { get; set; }
    public User? Influencer { get; set; }

    // Status Management
    public int CampaignStatus { get; set; } = 1; // DRAFT by default

    public int PaymentStatus { get; set; } = 1; // PENDING by default

    // Contract Management
    public string? GeneratedContractPdfPath { get; set; }

    public string? SignedContractPdfPath { get; set; }

    public DateTime? ContractSignedAt { get; set; }

    public DateTime? SignatureApprovedAt { get; set; }

    // Pricing (legacy - kept for backward compatibility)
    public string? Currency { get; set; }

    public float Amount { get; set; }

    // Payment Configuration
    public int PaymentType { get; set; } = 1; // PaymentType: ONE_TIME = 1, MILESTONE = 2

    public bool IsRecurringEnabled { get; set; } = false; // If brand enabled auto-pay (Paystack only)

    // Payment Tracking (amounts in pence for precision)
    public long TotalAmountInPence { get; set; } = 0;

    public long PaidAmountInPence { get; set; } = 0;

    public long ReleasedToInfluencerInPence { get; set; } = 0;

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? InfluencerAcceptedAt { get; set; }

    public DateTime? PaymentCompletedAt { get; set; }

    // Set when the brand marks the campaign COMPLETED. Anchors the review "double-blind" window.
    public DateTime? CompletedAt { get; set; }

    // Legacy field for backward compatibility
    [Obsolete("Use ProjectName instead")]
    public string? CampaignName { get; set; }

    // --- Campaign-level payment deadline tracking (CampaignPaymentDeadlineBackgroundService) ---
    // Covers the portion of the balance not already tracked by a PaymentMilestone row —
    // i.e. ONE_TIME campaigns (which never have milestones) and any gap left by a
    // MILESTONE campaign whose scheduled milestones don't add up to the full total.
    //
    // Only state that genuinely can't be derived gets persisted here: which pre-due
    // reminder tier has already been sent, and when the last overdue email went out
    // (both needed so the 6-hourly sweep doesn't resend the same email). Whether the
    // campaign IS overdue is computed below from CampaignEndDate + the amounts already
    // on this row — storing that separately would just be a second copy that can drift
    // stale between sweeps.

    /// <summary>Tightest pre-due reminder tier already sent — 7, 3, or 1 (days before the
    /// end date), or null if none yet. Decreases monotonically as the deadline approaches,
    /// so comparing against the current tier tells the sweep whether a tighter window
    /// still needs its own send.</summary>
    public int? EndDateLastReminderDaysSent { get; set; }

    /// <summary>Last time an overdue notice/escalation email was sent for this campaign's
    /// end date. Reused for both the first notice and every weekly repeat — same email,
    /// so one timestamp is enough to drive the "has it been N days" cadence check.</summary>
    public DateTime? EndDateLastOverdueNoticeSentAt { get; set; }

    /// <summary>True once CampaignEndDate has passed with the balance still outstanding.
    /// Computed from existing columns — not stored, so it's always accurate and never
    /// needs a background sweep to "clear" it when a payment comes in.</summary>
    [NotMapped]
    public bool IsPaymentOverdue =>
        PaidAmountInPence < TotalAmountInPence
        && CampaignEndDate.ToDateTime(TimeOnly.MinValue) < DateTime.UtcNow;

    /// <summary>When the overdue window started (== CampaignEndDate), or null if the
    /// campaign isn't currently overdue.</summary>
    [NotMapped]
    public DateTime? OverdueSince =>
        IsPaymentOverdue ? CampaignEndDate.ToDateTime(TimeOnly.MinValue) : null;

    /// <summary>True once CampaignEndDate has passed while CampaignStatus is still
    /// ACTIVE(6) — the contract period is over but nothing has moved the campaign to
    /// COMPLETED yet. This is a display-only signal: CampaignStatus itself is left
    /// untouched, so everything that gates on it being exactly ACTIVE (chat, post
    /// scheduling, the campaign payment-deadline sweep, CompleteCampaignAsync) keeps
    /// working unchanged. Computed, not stored, so it's always accurate.</summary>
    [NotMapped]
    public bool IsExpired =>
        CampaignStatus == 6 // ACTIVE
        && CampaignEndDate.ToDateTime(TimeOnly.MinValue) < DateTime.UtcNow;
}