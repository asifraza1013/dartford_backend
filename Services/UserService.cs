using System.Security.Cryptography;
using System.Text;
using inflan_api.Repositories;
using inflan_api.Interfaces;
using inflan_api.Models;

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

        public async Task<bool> UpdateUser(int id, User user)
        {
            try
            {
                Console.WriteLine($"UpdateUser called for ID: {id}");
                Console.WriteLine($"User data received: Name={user.Name}, Email={user.Email}, BrandCategory={user.BrandCategory}, BrandSector={user.BrandSector}, Goals={user.Goals?.Count}");
                
                var existingUser = await _userRepository.GetById(id);
                if (existingUser == null) 
                {
                    Console.WriteLine($"User with ID {id} not found");
                    return false;
                }

                if (user.Name != null) existingUser.Name = user.Name;
                if (user.UserName != null) existingUser.UserName = user.UserName;
                if (user.Email != null)
                {
                    var userExist = await GetByEmailAsync(user.Email);
                    // Only check for duplicate email if it's a different user (not the current one)
                    if (userExist != null && userExist.Id != id)
                    {
                        Console.WriteLine($"Email {user.Email} already exists for another user");
                        return false;
                    }
                    existingUser.Email = user.Email;
                }
                if (user.Password != null) existingUser.Password = user.Password;
                if (user.BrandName != null) existingUser.BrandName = user.BrandName;
                if (user.BrandCategory != null) existingUser.BrandCategory = user.BrandCategory;
                if (user.BrandSector != null) existingUser.BrandSector = user.BrandSector;
                if (user.Goals != null) existingUser.Goals = user.Goals;
                if (user.ProfileImage != null) existingUser.ProfileImage = user.ProfileImage;

                if (user.UserType != 0) existingUser.UserType = user.UserType;
                if (user.Status != 0) existingUser.Status = user.Status;

                Console.WriteLine("About to call repository update...");
                await _userRepository.Update(existingUser);
                Console.WriteLine("Repository update completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateUser: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
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
