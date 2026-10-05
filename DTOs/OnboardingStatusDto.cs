namespace inflan_api.DTOs
{
    /// <summary>
    /// Single source of truth for "what, if anything, is left to finish signup."
    /// Returned by login, the current-user endpoints, and GET api/Auth/onboarding-status
    /// so the frontend never has to infer progress from free-text messages.
    /// </summary>
    public class OnboardingStatusDto
    {
        public bool IsEmailVerified { get; set; }
        public bool IsComplete { get; set; }

        /// <summary>
        /// "EMAIL_VERIFICATION" | "SOCIALS" | "PACKAGE" | "GOALS" | null (when complete).
        /// </summary>
        public string? NextStep { get; set; }

        /// <summary>
        /// Frontend route to send the user to for NextStep, e.g. "/influencer/socials".
        /// Null when IsComplete is true.
        /// </summary>
        public string? NextStepRoute { get; set; }

        /// <summary>
        /// Human-readable list of what's still missing for the current step,
        /// e.g. ["Instagram handle", "TikTok handle"].
        /// </summary>
        public List<string> MissingRequirements { get; set; } = new();
    }
}
