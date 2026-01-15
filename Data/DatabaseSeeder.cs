using inflan_api.Models;
using inflan_api.MyDBContext;
using inflan_api.Utils;
using Microsoft.EntityFrameworkCore;

namespace inflan_api.Data
{
    public class DatabaseSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<InflanDBContext>();

            // Ensure database is created
            await context.Database.MigrateAsync();

            // Check if admin user already exists
            var adminEmail = "admin@dartford.com";
            var existingAdmin = await context.Users
                .FirstOrDefaultAsync(u => u.Email == adminEmail && u.UserType == (int)UserType.ADMIN);

            if (existingAdmin == null)
            {
                // Create default admin user
                var adminUser = new User
                {
                    Name = "System Admin",
                    Email = adminEmail,
                    UserName = "admin",
                    // NOTE: Password is stored in plain text (not hashed)
                    // Change this password immediately after first login!
                    Password = "Admin@123",
                    UserType = (int)UserType.ADMIN,
                    Status = (int)Status.ACTIVE,
                    Currency = "GBP",
                    Location = "GB"
                };

                context.Users.Add(adminUser);
                await context.SaveChangesAsync();

                Console.WriteLine("✓ Default admin user created successfully!");
                Console.WriteLine($"  Email: {adminEmail}");
                Console.WriteLine("  Password: Admin@123");
                Console.WriteLine("  IMPORTANT: Please change this password after first login!");
            }
            else
            {
                Console.WriteLine("✓ Admin user already exists.");
            }
        }
    }
}
