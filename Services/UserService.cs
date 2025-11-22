using MiddleWareWebApi.data;
using MiddleWareWebApi.Models;
using MiddleWareWebApi.MiddleWare;
using Dapper;
using System.Text;
using System.Security.Claims;

namespace MiddleWareWebApi.Services
{
    public class UserService
    {
        private readonly DapperContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<UserService> _logger;

        public UserService(
            DapperContext context,
            ICurrentUserService currentUserService,
            ILogger<UserService> logger)
        {
            _context = context;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        // 🔓 PUBLIC METHODS (No authentication required)

        /// <summary>
        /// Check if username exists - Public method for registration validation
        /// </summary>
        public async Task<bool> CheckUsernameExistsAsync(string username)
        {
            using var conn = _context.CreateConnection();
            var count = await conn.QuerySingleAsync<int>(
                "SELECT COUNT(1) FROM Users WHERE Username = @Username",
                new { Username = username });

            _logger.LogInformation("Username availability check for: {Username}, Exists: {Exists}",
                username, count > 0);

            return count > 0;
        }

        /// <summary>
        /// Check if email exists - Public method for registration validation
        /// </summary>
        public async Task<bool> CheckEmailExistsAsync(string email)
        {
            using var conn = _context.CreateConnection();
            var count = await conn.QuerySingleAsync<int>(
                "SELECT COUNT(1) FROM Users WHERE Email = @Email",
                new { Email = email });

            _logger.LogInformation("Email availability check for: {Email}, Exists: {Exists}",
                email, count > 0);

            return count > 0;
        }

        /// <summary>
        /// Get user by username - Public method for authentication
        /// </summary>
        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            // This is a public method that can be called without authentication
            // Used by IdentityService for login validation

            _logger.LogInformation("Public user lookup for username: {Username}", username);

            using var conn = _context.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE Username = @Username", new { Username = username });
        }

        /// <summary>
        /// Get user by email - Public method for authentication
        /// </summary>
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            // Public method for login and password reset
            _logger.LogInformation("Public user lookup for email: {Email}", email);

            using var conn = _context.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE Email = @Email", new { Email = email });
        }

        /// <summary>
        /// Create new user - Public method for registration
        /// </summary>
        public async Task<User> CreateUserAsync(User user)
        {
            // Public method for registration - no authentication required
            using var conn = _context.CreateConnection();

            var sql = @"INSERT INTO Users (Username, Email, PasswordHash, FullName, PhoneNumber, Role, IsActive, CreatedAt)
                       OUTPUT INSERTED.UserId
                       VALUES (@Username, @Email, @PasswordHash, @FullName, @PhoneNumber, @Role, @IsActive, @CreatedAt)";

            user.CreatedAt = DateTime.UtcNow;
            user.IsActive = true;
            user.Role = user.Role ?? "User"; // Default role

            var userId = await conn.QuerySingleAsync<int>(sql, user);
            user.UserId = userId;

            _logger.LogInformation("New user created: UserId={UserId}, Username={Username}, Email={Email}",
                userId, user.Username, user.Email);

            return user;
        }

        /// <summary>
        /// Update user password - Public method for password reset
        /// </summary>
        public async Task<bool> UpdatePasswordAsync(int userId, string newPasswordHash)
        {
            using var conn = _context.CreateConnection();
            var result = await conn.ExecuteAsync(
                "UPDATE Users SET PasswordHash = @PasswordHash WHERE UserId = @UserId",
                new { PasswordHash = newPasswordHash, UserId = userId });

            _logger.LogInformation("Password updated for UserId: {UserId}", userId);
            return result > 0;
        }

        // 🔒 PROTECTED METHODS (Authentication required)

        /// <summary>
        /// Get all users - Admin only
        /// </summary>
        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            // Check if user is authenticated using the Principal
            if (!_currentUserService.IsAuthenticated)
            {
                _logger.LogWarning("Unauthenticated access attempt to GetAllUsersAsync");
                throw new UnauthorizedAccessException("Authentication required");
            }

            // Check if user has admin role
            if (!_currentUserService.IsInRole("Admin"))
            {
                _logger.LogWarning("User {UserId} attempted to access all users without admin role",
                    _currentUserService.UserId);
                throw new UnauthorizedAccessException("Admin role required");
            }

            try
            {
                using var conn = _context.CreateConnection();
                var users = await conn.QueryAsync<User>("SELECT * FROM Users");

                _logger.LogInformation("Admin user {UserId} retrieved all users list",
                    _currentUserService.UserId);
                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SQL Error while getting all users for user {UserId}",
                    _currentUserService.UserId);
                throw;
            }
        }

        /// <summary>
        /// Get current user's profile - Authentication required
        /// </summary>
        public async Task<User?> GetMyProfileAsync()
        {
            if (!_currentUserService.IsAuthenticated || _currentUserService.UserId == null)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            using var conn = _context.CreateConnection();
            var user = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE UserId = @UserId",
                new { UserId = _currentUserService.UserId.Value });

            _logger.LogInformation("User {UserId} retrieved their own profile",
                _currentUserService.UserId);
            return user;
        }

        /// <summary>
        /// Update user profile - Authentication required
        /// </summary>
        public async Task<bool> UpdateUserAsync(User user)
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            // Users can only update their own profile unless they're admin
            if (!_currentUserService.IsInRole("Admin") &&
                _currentUserService.UserId != user.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to update user {TargetUserId} without permission",
                    _currentUserService.UserId, user.UserId);
                throw new UnauthorizedAccessException("You can only update your own profile");
            }

            using var conn = _context.CreateConnection();
            var sql = @"UPDATE Users SET 
                        Username = @Username, 
                        Email = @Email,
                        FullName = @FullName,
                        PhoneNumber = @PhoneNumber,
                        AddressLine1 = @AddressLine1,
                        AddressLine2 = @AddressLine2,
                        City = @City,
                        State = @State,
                        PostalCode = @PostalCode,
                        Country = @Country,
                        DateOfBirth = @DateOfBirth,
                        ProfilePictureUrl = @ProfilePictureUrl
                        WHERE UserId = @UserId";

            var result = await conn.ExecuteAsync(sql, user);

            _logger.LogInformation("User profile updated: UserId={UserId}, UpdatedBy={UpdatedBy}",
                user.UserId, _currentUserService.UserId);

            return result > 0;
        }

        /// <summary>
        /// Delete user - Admin only
        /// </summary>
        public async Task<bool> DeleteUserAsync(int userId)
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            if (!_currentUserService.IsInRole("Admin"))
            {
                throw new UnauthorizedAccessException("Admin role required");
            }

            using var conn = _context.CreateConnection();
            var result = await conn.ExecuteAsync("DELETE FROM Users WHERE UserId = @UserId",
                new { UserId = userId });

            _logger.LogInformation("User {UserId} deleted by admin {AdminId}",
                userId, _currentUserService.UserId);
            return result > 0;
        }

        /// <summary>
        /// Get users by role - Authentication required
        /// </summary>
        public async Task<IEnumerable<User>> GetUsersByRoleAsync(string role)
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            // Only admins can see admin users
            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
                !_currentUserService.IsInRole("Admin"))
            {
                throw new UnauthorizedAccessException("Only admins can view admin users");
            }

            using var conn = _context.CreateConnection();
            var users = await conn.QueryAsync<User>(
                "SELECT * FROM Users WHERE Role = @Role AND IsActive = 1",
                new { Role = role });

            _logger.LogInformation("User {UserId} retrieved users with role {Role}",
                _currentUserService.UserId, role);

            return users;
        }

        /// <summary>
        /// Search users - Authentication required
        /// </summary>
        public async Task<IEnumerable<User>> SearchUsersAsync(string searchTerm)
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            using var conn = _context.CreateConnection();

            // Regular users can only search for other regular users
            // Admins can search all users
            string sql;
            if (_currentUserService.IsInRole("Admin"))
            {
                sql = @"SELECT * FROM Users 
                        WHERE (Username LIKE @SearchTerm OR Email LIKE @SearchTerm OR FullName LIKE @SearchTerm)
                        AND IsActive = 1";
            }
            else
            {
                sql = @"SELECT * FROM Users 
                        WHERE (Username LIKE @SearchTerm OR Email LIKE @SearchTerm OR FullName LIKE @SearchTerm)
                        AND Role = 'User' AND IsActive = 1";
            }

            var users = await conn.QueryAsync<User>(sql, new { SearchTerm = $"%{searchTerm}%" });

            _logger.LogInformation("User {UserId} searched for users with term: {SearchTerm}",
                _currentUserService.UserId, searchTerm);

            return users;
        }

        /// <summary>
        /// Get current user's activity log - Authentication required
        /// </summary>
        public async Task<IEnumerable<object>> GetMyActivityLogAsync()
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            // This would typically come from an audit/activity table
            var activity = new[]
            {
                new
                {
                    Timestamp = DateTime.UtcNow.AddHours(-1),
                    Action = "Profile Updated",
                    Details = "Updated profile information",
                    UserId = _currentUserService.UserId
                },
                new
                {
                    Timestamp = DateTime.UtcNow.AddHours(-2),
                    Action = "Login",
                    Details = "Successful login",
                    UserId = _currentUserService.UserId
                }
            };

            _logger.LogInformation("User {UserId} retrieved their activity log",
                _currentUserService.UserId);

            return activity;
        }

        // 🔧 UTILITY METHODS (Used by IdentityService)

        /// <summary>
        /// Hash password - Utility method
        /// </summary>
        public string HashPassword(string password)
        {
            // In production, use BCrypt or similar
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
        }

        /// <summary>
        /// Verify password - Utility method
        /// </summary>
        public bool VerifyPassword(string enteredPassword, string storedHash)
        {
            var enteredHash = HashPassword(enteredPassword);
            return enteredHash == storedHash;
        }

        /// <summary>
        /// Authenticate user - Used by IdentityService
        /// </summary>
        public async Task<User?> AuthenticateAsync(string username, string password)
        {
            var user = await GetUserByUsernameAsync(username);
            if (user != null && VerifyPassword(password, user.PasswordHash))
            {
                _logger.LogInformation("User authenticated successfully: {Username}", username);
                return user;
            }

            _logger.LogWarning("Authentication failed for username: {Username}", username);
            return null;
        }

        // Legacy method (keeping for backward compatibility)
        public async Task RegisterUserAsync(User user)
        {
            user.PasswordHash = HashPassword(user.PasswordHash);
            using var conn = _context.CreateConnection();
            var sql = "INSERT INTO Users (Username, PasswordHash, Role) VALUES (@Username, @PasswordHash, @Role)";
            await conn.ExecuteAsync(sql, user);
        }
    }
}
