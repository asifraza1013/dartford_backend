using inflan_api.Models;

namespace inflan_api.Interfaces
{
    public interface IAuthService
    {
        string GenerateJwtToken(User user);
        Task<User> RegisterAsync(User user);
    }
}
