using inflan_api.Models;
using inflan_api.DTOs;

namespace inflan_api.Interfaces
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetAllUsers();
        Task<User?> GetUserById(int id);
        Task<User> CreateUser(User user);
        Task<(bool success, string? errorMessage)> UpdateUser(int id, UpdateUserDto userDto);
        Task<bool> DeleteUser(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> ValidateUserAsync(string email, string password);
        Task<bool> VerifyPasswordAsync(int userId, string password);
        Task<string> GenerateUniqueUsernameFromNameAsync(string name);
        Task<string?> SaveOrUpdateProfilePictureAsync(int userId, IFormFile? file);
        Task<PasswordResetToken> CreatePasswordResetTokenAsync(int userId, int expiresInMinutes);
        Task<PasswordResetToken?> GetValidPasswordResetTokenAsync(string token);
        Task<bool> ResetPasswordAsync(string token, string newPassword);
    }
}
