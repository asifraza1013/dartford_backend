using inflan_api.Utils;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using inflan_api.Interfaces;
using inflan_api.Models;

namespace inflan_api.Services
{
    public class AuthService : IAuthService
    {
        private const int OtpExpiryMinutes = 10;
        private const int ResendCooldownSeconds = 30;
        private const int MaxVerificationAttempts = 5;

        private readonly IConfiguration _configuration;
        private readonly IUserService _userService;
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IConfiguration configuration,
            IUserService userService,
            IUserRepository userRepository,
            IEmailService emailService,
            ILogger<AuthService> logger)
        {
            _configuration = configuration;
            _userService = userService;
            _userRepository = userRepository;
            _emailService = emailService;
            _logger = logger;
        }

        public string GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim("UserType", user.UserType.ToString()),
                new Claim("email_verified", user.IsEmailVerified ? "true" : "false")
            };

            var jwtToken = new JwtSecurityToken(
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddDays(30),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"])),
                    SecurityAlgorithms.HmacSha256Signature));

            return new JwtSecurityTokenHandler().WriteToken(jwtToken);
        }

        public async Task<User> RegisterAsync(User user)
            => await _userService.CreateUser(user);

        public async Task<(bool Sent, Message? ErrorCode, int? RetryAfterSeconds)> SendVerificationCodeAsync(User user)
        {
            if (user.IsEmailVerified)
                return (false, Message.EMAIL_ALREADY_VERIFIED, null);

            if (user.VerificationLastSentAt.HasValue)
            {
                var elapsed = DateTime.UtcNow - user.VerificationLastSentAt.Value;
                var cooldown = TimeSpan.FromSeconds(ResendCooldownSeconds);
                if (elapsed < cooldown)
                {
                    var remaining = (int)Math.Ceiling((cooldown - elapsed).TotalSeconds);
                    return (false, Message.VERIFICATION_RESEND_TOO_SOON, remaining);
                }
            }

            var code = GenerateNumericCode(6);
            user.VerificationCodeHash = HashCode(code);
            user.VerificationExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes);
            user.VerificationAttempts = 0;
            user.VerificationLastSentAt = DateTime.UtcNow;
            await _userRepository.Update(user);

            try
            {
                await _emailService.SendVerificationCodeAsync(
                    user.Email!,
                    user.Name ?? string.Empty,
                    code,
                    OtpExpiryMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
            }

            return (true, null, null);
        }

        public async Task<(User? User, Message? ErrorCode)> VerifyCodeAsync(string email, string code)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
                return (null, Message.USER_NOT_FOUND);

            if (user.IsEmailVerified)
                return (null, Message.EMAIL_ALREADY_VERIFIED);

            if (string.IsNullOrEmpty(user.VerificationCodeHash) || !user.VerificationExpiresAt.HasValue)
                return (null, Message.VERIFICATION_CODE_EXPIRED);

            if (user.VerificationExpiresAt.Value < DateTime.UtcNow)
                return (null, Message.VERIFICATION_CODE_EXPIRED);

            if (user.VerificationAttempts >= MaxVerificationAttempts)
                return (null, Message.VERIFICATION_TOO_MANY_ATTEMPTS);

            var attempted = HashCode(code);
            var expected = user.VerificationCodeHash;
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(attempted),
                    Encoding.UTF8.GetBytes(expected)))
            {
                user.VerificationAttempts++;
                await _userRepository.Update(user);
                return (null, Message.VERIFICATION_CODE_INVALID);
            }

            user.IsEmailVerified = true;
            user.EmailVerifiedAt = DateTime.UtcNow;
            user.VerificationCodeHash = null;
            user.VerificationExpiresAt = null;
            user.VerificationAttempts = 0;
            user.VerificationLastSentAt = null;
            user.Status = (int)Status.ACTIVE;
            await _userRepository.Update(user);

            return (user, null);
        }

        private static string GenerateNumericCode(int digits)
        {
            var max = (int)Math.Pow(10, digits);
            var number = RandomNumberGenerator.GetInt32(0, max);
            return number.ToString(new string('0', digits));
        }

        private static string HashCode(string code)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
            return Convert.ToHexString(bytes);
        }
    }
}
