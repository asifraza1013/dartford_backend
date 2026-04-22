using inflan_api.Models;
using inflan_api.Utils;

namespace inflan_api.Interfaces
{
    public interface IAuthService
    {
        string GenerateJwtToken(User user);
        Task<User> RegisterAsync(User user);
        Task<(bool Sent, Message? ErrorCode, int? RetryAfterSeconds)> SendVerificationCodeAsync(User user);
        Task<(User? User, Message? ErrorCode)> VerifyCodeAsync(string email, string code);
    }
}
