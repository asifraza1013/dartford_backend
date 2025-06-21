using dartford_api.Models;
namespace dartford_api.Interfaces
{
    public interface IAuthService
    {
        string GenerateJwtToken(User user);
        Task<User> RegisterAsync(User user);
    }
}
