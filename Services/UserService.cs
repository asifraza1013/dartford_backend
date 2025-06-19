using dartford_api.Interfaces;
using dartford_api.Models;
using dartford_api.Repositories;

namespace dartford_api.Services
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
            var existingUser = await _userRepository.GetById(id);
            if (existingUser == null) return false;

            if (user.Name != null) existingUser.Name = user.Name;
            if (user.UserName != null) existingUser.UserName = user.UserName;
            if (user.Email != null) existingUser.Email = user.Email;
            if (user.Password != null) existingUser.Password = user.Password;
            if (user.BrandName != null) existingUser.BrandName = user.BrandName;
            if (user.BrandCategory != null) existingUser.BrandCategory = user.BrandCategory;
            if (user.BrandSector != null) existingUser.BrandSector = user.BrandSector;
            if (user.Goals != null) existingUser.Goals = user.Goals;
            if (user.ProfileImage != null) existingUser.ProfileImage = user.ProfileImage;

            if (user.UserType != 0) existingUser.UserType = user.UserType;
            if (user.Status != 0) existingUser.Status = user.Status;

            await _userRepository.Update(existingUser);
            return true;
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
    }
}
