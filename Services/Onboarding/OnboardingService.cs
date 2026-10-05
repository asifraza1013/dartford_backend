using inflan_api.DTOs;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.Utils;

namespace inflan_api.Services.Onboarding
{
    /// <summary>
    /// Centralizes the signup step order and completeness rules. This is the ONLY place
    /// that should decide "what step is next" — controllers must not re-derive it.
    /// </summary>
    public class OnboardingService : IOnboardingService
    {
        private readonly IInfluencerService _influencerService;
        private readonly IPlanService _planService;

        public OnboardingService(IInfluencerService influencerService, IPlanService planService)
        {
            _influencerService = influencerService;
            _planService = planService;
        }

        public async Task<OnboardingStatusDto> GetStatusAsync(User user)
        {
            var status = new OnboardingStatusDto
            {
                IsEmailVerified = user.IsEmailVerified
            };

            if (!user.IsEmailVerified)
            {
                status.IsComplete = false;
                status.NextStep = "EMAIL_VERIFICATION";
                status.NextStepRoute = "/verify-email";
                status.MissingRequirements.Add("Email verification");
                return status;
            }

            if (user.UserType == (int)UserType.INFLUENCER)
            {
                return await GetInfluencerStatusAsync(user, status);
            }

            if (user.UserType == (int)UserType.BRAND)
            {
                return GetBrandStatus(user, status);
            }

            // Admins and any other user type have no further onboarding steps.
            status.IsComplete = true;
            return status;
        }

        private async Task<OnboardingStatusDto> GetInfluencerStatusAsync(User user, OnboardingStatusDto status)
        {
            var influencer = await _influencerService.GetInfluencerBasicByUserId(user.Id);
            var hasRequiredSocials = influencer != null
                && !string.IsNullOrWhiteSpace(influencer.Instagram)
                && !string.IsNullOrWhiteSpace(influencer.TikTok);

            if (!hasRequiredSocials)
            {
                status.IsComplete = false;
                status.NextStep = "SOCIALS";
                status.NextStepRoute = "/influencer/socials";
                if (influencer == null || string.IsNullOrWhiteSpace(influencer.Instagram))
                    status.MissingRequirements.Add("Instagram handle");
                if (influencer == null || string.IsNullOrWhiteSpace(influencer.TikTok))
                    status.MissingRequirements.Add("TikTok handle");
                return status;
            }

            var plans = await _planService.GetPlansByUserId(user.Id);
            if (!plans.Any())
            {
                status.IsComplete = false;
                status.NextStep = "PACKAGE";
                status.NextStepRoute = "/influencer/package";
                status.MissingRequirements.Add("At least one service plan");
                return status;
            }

            status.IsComplete = true;
            return status;
        }

        private OnboardingStatusDto GetBrandStatus(User user, OnboardingStatusDto status)
        {
            var brandInfoFilled = !string.IsNullOrWhiteSpace(user.BrandCategory)
                && !string.IsNullOrWhiteSpace(user.BrandSector)
                && user.Goals != null
                && user.Goals.Any();

            if (!brandInfoFilled)
            {
                status.IsComplete = false;
                status.NextStep = "GOALS";
                status.NextStepRoute = "/brand/goals";
                if (string.IsNullOrWhiteSpace(user.BrandCategory))
                    status.MissingRequirements.Add("Brand sector");
                if (string.IsNullOrWhiteSpace(user.BrandSector))
                    status.MissingRequirements.Add("Brand category");
                if (user.Goals == null || !user.Goals.Any())
                    status.MissingRequirements.Add("At least one goal");
                return status;
            }

            status.IsComplete = true;
            return status;
        }
    }
}
