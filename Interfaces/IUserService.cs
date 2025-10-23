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
        Task<string> GenerateUniqueUsernameFromNameAsync(string name);
        Task<string?> SaveOrUpdateProfilePictureAsync(int userId, IFormFile? file);

    }
}
