using dartford_api.Models;
namespace dartford_api.Interfaces
{
    public interface IAuthService
    {
        string GenerateJWTToken(LoginModel user, int userType);
        Task<User> RegisterAsync(User user);
    }
}
