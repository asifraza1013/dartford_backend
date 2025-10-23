using System.Security.Cryptography;
using System.Text;
using inflan_api.Repositories;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.DTOs;

namespace inflan_api.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<IEnumerable<User>> GetAllUsers()
        {
            return await _userRepository.GetAll();
        }

        public async Task<User?> GetUserById(int id)
        {
            return await _userRepository.GetById(id);
        }

        public async Task<User> CreateUser(User user)
        {
            return await _userRepository.Create(user);
        }

        public async Task<(bool success, string? errorMessage)> UpdateUser(int id, UpdateUserDto userDto)
        {
            try
            {
                Console.WriteLine($"UpdateUser called for ID: {id}");
                Console.WriteLine($"User data received: Name={userDto.Name}, Email={userDto.Email}, BrandCategory={userDto.BrandCategory}, BrandSector={userDto.BrandSector}, Goals={userDto.Goals?.Count}");

                var existingUser = await _userRepository.GetById(id);
                if (existingUser == null)
                {
                    Console.WriteLine($"User with ID {id} not found");
                    return (false, "User not found");
                }

                // Only update fields that are provided (not null)
                if (!string.IsNullOrWhiteSpace(userDto.Name))
                    existingUser.Name = userDto.Name;

                if (!string.IsNullOrWhiteSpace(userDto.UserName))
                    existingUser.UserName = userDto.UserName;

                if (!string.IsNullOrWhiteSpace(userDto.Email))
                {
                    // Validate email format (already done by DTO validation attribute)
                    var userExist = await GetByEmailAsync(userDto.Email);
                    // Only check for duplicate email if it's a different user (not the current one)
                    if (userExist != null && userExist.Id != id)
                    {
                        Console.WriteLine($"Email {userDto.Email} already exists for another user");
                        return (false, "Email already exists");
                    }
                    existingUser.Email = userDto.Email;
                }

                if (!string.IsNullOrWhiteSpace(userDto.Password))
                {
                    // Password validation (min 6 chars) is already done by DTO validation attribute
                    existingUser.Password = userDto.Password;
                }

                if (!string.IsNullOrWhiteSpace(userDto.BrandName))
                    existingUser.BrandName = userDto.BrandName;

                if (!string.IsNullOrWhiteSpace(userDto.BrandCategory))
                    existingUser.BrandCategory = userDto.BrandCategory;

                if (!string.IsNullOrWhiteSpace(userDto.BrandSector))
                    existingUser.BrandSector = userDto.BrandSector;

                if (userDto.Goals != null)
                    existingUser.Goals = userDto.Goals;

                if (!string.IsNullOrWhiteSpace(userDto.ProfileImage))
                    existingUser.ProfileImage = userDto.ProfileImage;

                // Handle optional integer fields
                if (userDto.UserType.HasValue && userDto.UserType.Value != 0)
                    existingUser.UserType = userDto.UserType.Value;

                if (userDto.Status.HasValue)
                    existingUser.Status = userDto.Status.Value;

                Console.WriteLine("About to call repository update...");
                await _userRepository.Update(existingUser);
                Console.WriteLine("Repository update completed successfully");
                return (true, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateUser: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return (false, $"An error occurred: {ex.Message}");
            }
        }

        public async Task<bool> DeleteUser(int id)
        {
            var existingUser = await _userRepository.GetById(id);
            if (existingUser == null) return false;

            await _userRepository.Delete(id);
            return true;
        }
        public async Task<User?> GetByEmailAsync(string email)
           => await _userRepository.GetByEmailAsync(email);

        public async Task<User?> ValidateUserAsync(string email, string password)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null || user.Password != password)
                return null;
            return user;
        }
        private string CleanName(string name)
        {
            var parts = name.Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "user",
                1 => parts[0],
                _ => parts[0] + parts[^1]
            };
        }

        private string GenerateHashSuffix(int length = 6)
        {
            return Guid.NewGuid().ToString("N").Substring(0, length).ToLower();
        }
        public async Task<string> GenerateUniqueUsernameFromNameAsync(string name)
        {
            string baseUsername = CleanName(name);


            string suffix = GenerateHashSuffix(6);
            string username = baseUsername + suffix;

            var exists = await _userRepository.GetByUsernameAsync(username);
            if (exists == null)
            {
                return username;
            }

            throw new Exception("Failed to generate a unique username.");
        }
        public async Task<string?> SaveOrUpdateProfilePictureAsync(int userId, IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return null;

            var uploadsFolder = Path.Combine("wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);
            
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                return null;

            var fileName = $"{userId}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/{fileName}";
        }

    }
}
