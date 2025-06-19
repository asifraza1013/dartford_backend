using dartford_api.Models;

namespace dartford_api.Interfaces
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetAll();
        Task<User?> GetById(int id);
        Task<User> Create(User user);
        Task Update(User user);
        Task Delete(int id);
        Task<User?> GetByEmailAsync(string email);
    }
}
