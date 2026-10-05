using System.Security.Claims;
using inflan_api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace inflan_api.Attributes
{
    /// <summary>
    /// Blocks an endpoint until the current user has finished signup (email verified +
    /// required social accounts + at least one plan for influencers, or goals for brands).
    /// Apply only to endpoints that represent "real" platform usage (creating a booking,
    /// requesting a withdrawal) — not to the onboarding-step endpoints themselves.
    /// </summary>
    public class RequireOnboardingCompleteAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                context.Result = new JsonResult(new
                {
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN"
                })
                { StatusCode = 401 };
                return;
            }

            var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
            var onboardingService = context.HttpContext.RequestServices.GetRequiredService<IOnboardingService>();

            var user = await userService.GetUserById(userId);
            if (user == null)
            {
                context.Result = new JsonResult(new
                {
                    message = "User not found",
                    code = "USER_NOT_FOUND"
                })
                { StatusCode = 404 };
                return;
            }

            var onboarding = await onboardingService.GetStatusAsync(user);
            if (!onboarding.IsComplete)
            {
                context.Result = new JsonResult(new
                {
                    message = "Please finish setting up your account before continuing.",
                    code = "ONBOARDING_INCOMPLETE",
                    onboarding
                })
                { StatusCode = 403 };
            }
        }
    }
}
