namespace inflan_api.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Sends email notification to brand requesting contract signature
    /// </summary>
    Task SendContractSignatureRequestAsync(string brandEmail, string brandName, int campaignId, string contractUrl);

    /// <summary>
    /// Sends email notification to brand requesting payment after contract is signed
    /// </summary>
    Task SendPaymentRequestAsync(string brandEmail, string brandName, int campaignId, decimal amount, string currency);

    /// <summary>
    /// Sends email notification to influencer when campaign is accepted and activated
    /// </summary>
    Task SendCampaignActivatedAsync(string influencerEmail, string influencerName, int campaignId, string projectName);

    /// <summary>
    /// Sends email notification to brand when influencer accepts/rejects campaign
    /// </summary>
    Task SendInfluencerResponseNotificationAsync(string brandEmail, string brandName, int campaignId, string projectName, bool accepted, string? contractPdfPath = null);

    /// <summary>
    /// Sends email notification to influencer when a new campaign booking is created
    /// </summary>
    Task SendNewCampaignNotificationAsync(string influencerEmail, string influencerName, int campaignId, string projectName, string brandName);

    /// <summary>
    /// Sends email notification to influencer requesting review and approval of signed contract
    /// </summary>
    Task SendSignedContractReviewRequestAsync(string influencerEmail, string influencerName, int campaignId, string projectName, string brandName);

    /// <summary>
    /// Sends email notification to brand requesting contract revision after influencer rejection
    /// </summary>
    Task SendContractRevisionRequestAsync(string brandEmail, string brandName, int campaignId, string projectName, string? reason = null);

    /// <summary>
    /// Sends email notification to influencer when withdrawal is successful
    /// </summary>
    Task SendWithdrawalSuccessAsync(string influencerEmail, string influencerName, long amountInPence, string currency, string bankName, string accountNumberLast4);

    /// <summary>
    /// Sends email notification to influencer when withdrawal fails
    /// </summary>
    Task SendWithdrawalFailedAsync(string influencerEmail, string influencerName, long amountInPence, string currency, string? failureReason);

    /// <summary>
    /// Sends email notification to influencer when withdrawal is processing
    /// </summary>
    Task SendWithdrawalProcessingAsync(string influencerEmail, string influencerName, long amountInPence, string currency, string bankName, string accountNumberLast4);

    /// <summary>
    /// Sends email notification to influencer when a milestone payment is released to their balance
    /// </summary>
    Task SendPaymentReleasedAsync(string influencerEmail, string influencerName, long amountInPence, string currency, string campaignName, int milestoneNumber);

    /// <summary>
    /// Sends a 6-digit email verification code to a newly registered user.
    /// </summary>
    Task SendVerificationCodeAsync(string toEmail, string recipientName, string code, int expiresInMinutes);

    /// <summary>
    /// Sends a milestone payment reminder to the brand. <paramref name="daysUntilDue"/>
    /// is the count of days until the milestone's due date — pass a non-positive
    /// value (e.g. <c>-1</c>) to render the "overdue" variant of the email.
    /// </summary>
    Task SendMilestoneReminderAsync(
        string brandEmail,
        string brandName,
        int campaignId,
        string projectName,
        int milestoneNumber,
        long amountInPence,
        long platformFeeInPence,
        string currency,
        DateTime dueDate,
        int daysUntilDue);

    /// <summary>
    /// Reminder fired ~30 minutes before a scheduled post goes live, so the
    /// influencer has time to prep / publish the asset.
    /// </summary>
    Task SendScheduledPostReminderAsync(
        string influencerEmail,
        string influencerName,
        int campaignId,
        string? projectName,
        string postTitle,
        DateTime scheduledAt,
        int minutesUntilLive,
        IEnumerable<string> platforms);

    /// <summary>
    /// Sends a password reset link to the user with a time-limited token.
    /// </summary>
    Task SendPasswordResetAsync(string toEmail, string recipientName, string resetUrl, int expiresInMinutes);
}
