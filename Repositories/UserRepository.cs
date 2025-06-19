using dartford_api.Interfaces;
using dartford_api.Models;
using Microsoft.EntityFrameworkCore;
using dartford_api.MyDBContext;

namespace dartford_api.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly DartfordDBContext _context;

        public UserRepository()
        {
            _context = new DartfordDBContext();
        }

        public async Task<IEnumerable<User>> GetAll()
        {
            return await _context.Users.ToListAsync<User>();
        }

        public async Task<User?> GetById(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User> Create(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task Update(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
        }
        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

    }
}
