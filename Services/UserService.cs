using MiddleWareWebApi.data;
using MiddleWareWebApi.Models;
using Dapper;
using System.Text;

namespace MiddleWareWebApi.Services
{
    // C#
    public class UserService
    {
        private readonly DapperContext _context;
        public UserService(DapperContext context) => _context = context;

        public async Task<User> GetUserByUsernameAsync(string username)
        {
            using var conn = _context.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE Username = @Username", new { Username = username });
        }
        // Other user-related methods...
        public async Task RegisterUserAsync(User user)
        {
            // code for password hashing
            user.PasswordHash = HashPassword(user.PasswordHash);
            using var conn = _context.CreateConnection();
            var sql = "INSERT INTO Users (Username, PasswordHash, Role) VALUES (@Username, @PasswordHash, @Role)";
            await conn.ExecuteAsync(sql, user);
        }

        public string HashPassword(string password)
        {
            // Simple hash for demonstration; use a secure hashing algorithm in production
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
        }
        // Other methods like Authenticate, ChangePassword, etc.
        // Note: In a real application, ensure to handle exceptions and edge cases.
        // Example: Password verification
        public bool VerifyPassword(string enteredPassword, string storedHash)
        {
            var enteredHash = HashPassword(enteredPassword);
            return enteredHash == storedHash;
        }

        // Additional methods for user management...

        // Example: Get all users
        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            try
            {
                using var conn = _context.CreateConnection();
                var users = await conn.QueryAsync<User>("SELECT * FROM Users");
                return users;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SQL Error: {ex.Message}");
                throw;
            }
        }
        // Example: Delete user
        public async Task DeleteUserAsync(int userId)
        {
            using var conn = _context.CreateConnection();
            await conn.ExecuteAsync("DELETE FROM Users WHERE Id = @Id", new { Id = userId });
        }

        // Example: Update user

        public async Task UpdateUserAsync(User user)
        {
            using var conn = _context.CreateConnection();
            var sql = "UPDATE Users SET Username = @Username, PasswordHash = @PasswordHash, Role = @Role WHERE Id = @Id";
            await conn.ExecuteAsync(sql, user);
        }

        // Registration, password hashing, etc.

        // action methods to check token validity
        public bool IsTokenValid(string token)
        {
            // Implement token validation logic
            return !string.IsNullOrEmpty(token); // Placeholder
        }
        public string GenerateToken(User user)
        {
            // Implement token generation logic
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(user.Username)); // Placeholder
        }
        public User GetUserFromToken(string token)
        {
            // Implement logic to extract user info from token
            var username = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token)); // Placeholder
            return new User { Username = username };
        }
        public async Task<User> AuthenticateAsync(string username, string password)
        {
            var user = await GetUserByUsernameAsync(username);
            if (user != null && VerifyPassword(password, user.PasswordHash))
                return user;
            return null;
        }
        // generatetoken for user
        public string GenerateJwtToken(User user, TaskItem taskItem, string Artist)
        {
            // Implement JWT token generation logic
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(user.Username)); // Placeholder
        }

        public string GetUserByIdAsync(User user, TaskItem taskItem)
        {
            return user.Username;
        }
    }

}
