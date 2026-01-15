using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace inflan_api.Attributes
{
    /// <summary>
    /// Authorization attribute that ensures only admin users can access the endpoint
    /// </summary>
    public class AdminOnlyAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Check if user is authenticated
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new JsonResult(new
                {
                    message = "Unauthorized. Please log in.",
                    code = "UNAUTHORIZED"
                })
                {
                    StatusCode = 401
                };
                return;
            }

            // Check if user has UserType claim
            var userTypeClaim = context.HttpContext.User.FindFirst("UserType");
            if (userTypeClaim == null)
            {
                context.Result = new JsonResult(new
                {
                    message = "Invalid token. User type not found.",
                    code = "INVALID_TOKEN"
                })
                {
                    StatusCode = 401
                };
                return;
            }

            // Check if user is admin (UserType = 1)
            if (userTypeClaim.Value != "1")
            {
                context.Result = new JsonResult(new
                {
                    message = "Access denied. Admin privileges required.",
                    code = "FORBIDDEN"
                })
                {
                    StatusCode = 403
                };
                return;
            }
        }
    }
}
