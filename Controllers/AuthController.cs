using inflan_api.Services;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.DTOs;
using inflan_api.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace inflan_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly IInfluencerService _influencerService;
        private readonly IPlanService _planService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public AuthController(IUserService userService, IAuthService authService, IInfluencerService influencerService, IPlanService planService, IEmailService emailService, IConfiguration configuration)
        {
            _userService = userService;
            _authService = authService;
            _influencerService = influencerService;
            _planService = planService;
            _emailService = emailService;
            _configuration = configuration;
        }

        [HttpGet("getUser/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _userService.GetUserById(id);
            if (user == null)
                return StatusCode(404, new {
                    message = "User not found",
                    code = Message.USER_NOT_FOUND
                });

            return Ok(user);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUserProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return StatusCode(401, new {
                    message = "Unauthorized: Please login again",
                    code = "INVALID_TOKEN"
                });

            int userId = int.Parse(userIdClaim.Value);
            var user = await _userService.GetUserById(userId);

            if (user == null)
                return StatusCode(404, new {
                    message = "User not found",
                    code = Message.USER_NOT_FOUND
                });

            if (!user.IsEmailVerified)
            {
                return StatusCode(403, new
                {
                    message = MessageHelper.GetMessageText(Message.EMAIL_NOT_VERIFIED),
                    code = MessageHelper.GetMessageCode(Message.EMAIL_NOT_VERIFIED),
                    email = user.Email,
                    requiresVerification = true
                });
            }

            int userType = user.UserType;

            // For influencer, get complete influencer details
            if (userType == (int)UserType.INFLUENCER)
            {
                var influencer = await _influencerService.GetInfluencerBasicByUserId(user.Id);
                if (influencer != null)
                {
                    return Ok(new
                    {
                        user = user,
                        influencer = new
                        {
                            id = influencer.Id,
                            userId = influencer.UserId,
                            instagram = influencer.Instagram,
                            instagramFollower = influencer.InstagramFollower,
                            youtube = influencer.YouTube,
                            youtubeFollower = influencer.YouTubeFollower,
                            tiktok = influencer.TikTok,
                            tiktokFollower = influencer.TikTokFollower,
                            facebook = influencer.Facebook,
                            facebookFollower = influencer.FacebookFollower,
                            bio = influencer.Bio
                        }
                    });
                }
                else
                {
                    // No influencer profile yet
                    return Ok(new
                    {
                        user = user,
                        influencer = (object?)null,
                        message = "Please add your social media accounts",
                        code = Message.INFLUENCER_INFO_NOT_FILLED
                    });
                }
            }
            // For brand, return user with brand details
            else if (userType == (int)UserType.BRAND)
            {
                bool brandInfoFilled = !string.IsNullOrWhiteSpace(user.BrandCategory)
                                       && !string.IsNullOrWhiteSpace(user.BrandSector)
                                       && user.Goals != null
                                       && user.Goals.Any();

                if (!brandInfoFilled)
                {
                    return Ok(new
                    {
                        user = user,
                        message = "Please complete your brand profile",
                        code = Message.BRAND_INFO_NOT_FILLED,
                        missingStep = "Goals, Sector or Category missing"
                    });
                }

                return Ok(new
                {
                    user = user
                });
            }

            // Default case
            return Ok(new { user = user });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel login)
        {
            User? user = await _userService.ValidateUserAsync(login.Email, login.Password);
            if (user == null)
                return StatusCode(401, new {
                    message = MessageHelper.GetMessageText(Message.INVALID_EMAIL_PASSWORD),
                    code = MessageHelper.GetMessageCode(Message.INVALID_EMAIL_PASSWORD)
                });

            if (!user.IsEmailVerified)
            {
                // Trigger a fresh code so the verification page works immediately.
                await _authService.SendVerificationCodeAsync(user);
                return StatusCode(403, new
                {
                    message = MessageHelper.GetMessageText(Message.EMAIL_NOT_VERIFIED),
                    code = MessageHelper.GetMessageCode(Message.EMAIL_NOT_VERIFIED),
                    email = user.Email,
                    requiresVerification = true
                });
            }

            // Check if user is suspended (status = 2 means INACTIVE/SUSPENDED)
            if (user.Status == (int)Status.INACTIVE)
                return StatusCode(403, new {
                    message = "Your account has been suspended. Please contact support.",
                    code = "ACCOUNT_SUSPENDED"
                });

            var token = _authService.GenerateJwtToken(user);
            int userType = user.UserType;

            if (userType == (int)UserType.BRAND)
            {
                bool brandInfoFilled = !string.IsNullOrWhiteSpace(user.BrandCategory)
                                       && !string.IsNullOrWhiteSpace(user.BrandSector)
                                       && user.Goals != null
                                       && user.Goals.Any();

                if (!brandInfoFilled)
                {
                    return StatusCode(200, new
                    {
                        token,
                        user,
                        message = "Please complete your brand profile",
                        code = Message.BRAND_INFO_NOT_FILLED,
                        missingStep = "Goals, Sector or Category missing"
                    });
                }
            }else if (user.UserType == (int)UserType.INFLUENCER)
            {
                var influencer = await _influencerService.GetInfluencerBasicByUserId(user.Id);
                if (influencer == null)
                {
                    return StatusCode(200, new
                    {
                        token,
                        user,
                        message = "Please add your social media accounts",
                        code = Message.INFLUENCER_INFO_NOT_FILLED,
                        missingStep = "Socials missing"
                    });
                }
                // Note: We don't check for plans here because user might be in the process of creating them
                // The frontend will handle navigation to package creation if needed
            }
            
            return Ok(new
            {
                token = token,
                user = user
            });
        }

        [HttpPost("check-email")]
        public async Task<IActionResult> CheckEmail([FromBody] CheckEmailRequest request)
        {
            var existing = await _userService.GetByEmailAsync(request.Email);
            return Ok(new
            {
                exists = existing != null,
                verified = existing?.IsEmailVerified ?? false
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var existing = await _userService.GetByEmailAsync(request.Email);
            if (existing != null)
            {
                if (existing.IsEmailVerified)
                {
                    return StatusCode(409, new
                    {
                        message = MessageHelper.GetMessageText(Message.EMAIL_ALREADY_REGISTERED),
                        code = MessageHelper.GetMessageCode(Message.EMAIL_ALREADY_REGISTERED)
                    });
                }

                // Email exists but not verified yet — resend the code instead of creating a duplicate.
                var (resent, resendError, retryAfter) = await _authService.SendVerificationCodeAsync(existing);
                if (!resent && resendError == Message.VERIFICATION_RESEND_TOO_SOON)
                {
                    return StatusCode(429, new
                    {
                        message = MessageHelper.GetMessageText(resendError.Value),
                        code = MessageHelper.GetMessageCode(resendError.Value),
                        email = existing.Email,
                        retryAfterSeconds = retryAfter,
                        requiresVerification = true
                    });
                }

                return Ok(new
                {
                    message = MessageHelper.GetMessageText(Message.REGISTRATION_PENDING_VERIFICATION),
                    code = MessageHelper.GetMessageCode(Message.REGISTRATION_PENDING_VERIFICATION),
                    email = existing.Email,
                    requiresVerification = true
                });
            }

            var newUser = new User
            {
                Name = request.Name,
                Email = request.Email,
                Password = request.Password,
                UserType = request.UserType,
                BrandName = request.BrandName,
                Location = string.IsNullOrWhiteSpace(request.Location) ? "NG" : request.Location,
                Currency = string.IsNullOrWhiteSpace(request.Currency)
                    ? (string.Equals(request.Location, "GB", StringComparison.OrdinalIgnoreCase) ? "GBP" : "NGN")
                    : request.Currency,
                Status = (int)Status.INACTIVE,
                IsEmailVerified = false
            };

            try
            {
                newUser.UserName = await _userService.GenerateUniqueUsernameFromNameAsync(request.Name);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to generate username. Please try again.",
                    code = "USERNAME_GENERATION_ERROR",
                    details = ex.Message
                });
            }

            var created = await _authService.RegisterAsync(newUser);
            await _authService.SendVerificationCodeAsync(created);

            return Ok(new
            {
                message = MessageHelper.GetMessageText(Message.REGISTRATION_PENDING_VERIFICATION),
                code = MessageHelper.GetMessageCode(Message.REGISTRATION_PENDING_VERIFICATION),
                email = created.Email,
                requiresVerification = true
            });
        }

        [HttpPost("send-verification")]
        public async Task<IActionResult> SendVerification([FromBody] SendVerificationRequest request)
        {
            var user = await _userService.GetByEmailAsync(request.Email);
            if (user == null)
            {
                // Don't reveal whether the email exists — return the normal success envelope.
                return Ok(new
                {
                    message = MessageHelper.GetMessageText(Message.VERIFICATION_CODE_SENT),
                    code = MessageHelper.GetMessageCode(Message.VERIFICATION_CODE_SENT),
                    email = request.Email
                });
            }

            if (user.IsEmailVerified)
            {
                return StatusCode(409, new
                {
                    message = MessageHelper.GetMessageText(Message.EMAIL_ALREADY_VERIFIED),
                    code = MessageHelper.GetMessageCode(Message.EMAIL_ALREADY_VERIFIED),
                    email = user.Email
                });
            }

            var (sent, errorCode, retryAfter) = await _authService.SendVerificationCodeAsync(user);
            if (!sent && errorCode == Message.VERIFICATION_RESEND_TOO_SOON)
            {
                return StatusCode(429, new
                {
                    message = MessageHelper.GetMessageText(errorCode.Value),
                    code = MessageHelper.GetMessageCode(errorCode.Value),
                    email = user.Email,
                    retryAfterSeconds = retryAfter
                });
            }

            return Ok(new
            {
                message = MessageHelper.GetMessageText(Message.VERIFICATION_CODE_SENT),
                code = MessageHelper.GetMessageCode(Message.VERIFICATION_CODE_SENT),
                email = user.Email
            });
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            var (user, errorCode) = await _authService.VerifyCodeAsync(request.Email, request.Code);
            if (user == null)
            {
                var code = errorCode ?? Message.VERIFICATION_CODE_INVALID;
                var status = code switch
                {
                    Message.USER_NOT_FOUND => 404,
                    Message.EMAIL_ALREADY_VERIFIED => 409,
                    Message.VERIFICATION_CODE_EXPIRED => 410,
                    Message.VERIFICATION_TOO_MANY_ATTEMPTS => 429,
                    _ => 400
                };
                return StatusCode(status, new
                {
                    message = MessageHelper.GetMessageText(code),
                    code = MessageHelper.GetMessageCode(code)
                });
            }

            var token = _authService.GenerateJwtToken(user);
            return Ok(new
            {
                token,
                user,
                message = MessageHelper.GetMessageText(Message.EMAIL_VERIFIED_SUCCESSFULLY),
                code = MessageHelper.GetMessageCode(Message.EMAIL_VERIFIED_SUCCESSFULLY)
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordModel model)
        {
            var user = await _userService.GetByEmailAsync(model.Email);
            if (user == null)
                return StatusCode(404, new
                {
                    message = "No account is registered with this email address.",
                    code = "EMAIL_NOT_FOUND"
                });

            int expiresInMinutes = 30;
            var resetToken = await _userService.CreatePasswordResetTokenAsync(user.Id, expiresInMinutes);

            var frontendBaseUrl = _configuration["App:FrontendBaseUrl"] ?? "https://dev.inflan.com";
            var resetUrl = $"{frontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(resetToken.Token)}";

            try
            {
                await _emailService.SendPasswordResetAsync(user.Email!, user.Name ?? "", resetUrl, expiresInMinutes);
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    message = "We couldn't send the reset email right now. Please try again in a few minutes.",
                    code = "EMAIL_SEND_FAILED"
                });
            }

            return Ok(new
            {
                message = $"A password reset link has been sent to {user.Email}. The link will expire in {expiresInMinutes} minutes.",
                code = "PASSWORD_RESET_SENT"
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model)
        {
            var success = await _userService.ResetPasswordAsync(model.Token, model.Password);
            if (!success)
                return StatusCode(400, new
                {
                    message = "This password reset link is invalid or has expired. Please request a new one.",
                    code = "INVALID_OR_EXPIRED_TOKEN"
                });

            return Ok(new
            {
                message = "Your password has been reset successfully. Please login with your new password.",
                code = "PASSWORD_RESET_SUCCESS"
            });
        }
    }
}
