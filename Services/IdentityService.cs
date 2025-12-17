using Dapper;
using MiddleWareWebApi.data;
using MiddleWareWebApi.Models;
using MiddleWareWebApi.Models.Identity;
using MiddleWareWebApi.Services.Interfaces;
using BCrypt.Net;
using Microsoft.Extensions.Options;
using MiddleWareWebApi.Models.Configuration;

namespace MiddleWareWebApi.Services
{
    public class IdentityService : IIdentityService
    {
        private readonly DapperContext _context;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<IdentityService> _logger;

        public IdentityService(
            DapperContext context, 
            IJwtTokenService jwtTokenService,
            IOptions<JwtSettings> jwtSettings,
            ILogger<IdentityService> logger)
        {
            _context = context;
            _jwtTokenService = jwtTokenService;
            _jwtSettings = jwtSettings.Value;
            _logger = logger;
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request, string ipAddress)
        {
            try
            {
                using var connection = _context.CreateConnection();
                
                var user = await connection.QueryFirstOrDefaultAsync<User>(
                    "SELECT * FROM Users WHERE Email = @Email AND IsActive = 1", 
                    new { Email = request.Email });

                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Failed login attempt for email: {Email}", request.Email);
                    return null;
                }

                var accessToken = _jwtTokenService.GenerateAccessToken(user);
                var refreshToken = _jwtTokenService.GenerateRefreshToken();

                // Save refresh token to database
                await SaveRefreshTokenAsync(user.UserId, refreshToken, ipAddress);

                _logger.LogInformation("User {UserId} successfully logged in", user.UserId);

                return new AuthResponse
                {
                    Token = accessToken,
                    RefreshToken = refreshToken,
                    Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                    User = MapToUserInfo(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
                throw;
            }
        }

        public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, string ipAddress)
        {
            try
            {
                using var connection = _context.CreateConnection();
                //check if user exit below
                var existingUser = await connection.QueryFirstOrDefaultAsync<User>(
                    "SELECT * FROM Users WHERE Email = @Email OR Username = @Username", 
                    new { Email = request.Email, Username = request.Username });
                if (existingUser != null) {
                    _logger.LogWarning("Registration attempt with existing email or username: {Email}, {Username}", request.Email, request.Username);
                    return null;
                }



                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

                var user = new User
                {
                    Username = request.Username,
                    Email = request.Email,
                    PasswordHash = hashedPassword,
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    Role = "User",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Password = request.Password,
                    ConfirmPassword = request.ConfirmPassword
                };

                var sql = @"
                    INSERT INTO Users (Username, Email,  FullName, PhoneNumber,CreatedAt, PasswordHash) 
                    OUTPUT INSERTED.UserId
                    VALUES (@Username, @Email, @FullName, @PhoneNumber, @CreatedAt, @PasswordHash)";
                try
                {
                    var userId = await connection.QuerySingleAsync<int>(sql, user);
                    user.UserId = userId;
                }
                catch (Exception ex)
                {

                }

                var accessToken = _jwtTokenService.GenerateAccessToken(user);
                var refreshToken = _jwtTokenService.GenerateRefreshToken();

                await SaveRefreshTokenAsync(user.UserId, refreshToken, ipAddress);

                _logger.LogInformation("User {UserId} successfully registered", user.UserId);

                return new AuthResponse
                {
                    Token = accessToken,
                    RefreshToken = refreshToken,
                    Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                    User = MapToUserInfo(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for email: {Email}", request.Email);
                throw;
            }
        }

        public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken, string ipAddress)
        {
            try
            {
                using var connection = _context.CreateConnection();

                var token = await connection.QueryFirstOrDefaultAsync<RefreshToken>(
                    "SELECT * FROM RefreshTokens WHERE Token = @Token AND IsRevoked = 0", 
                    new { Token = refreshToken });

                if (token == null || !token.IsActive)
                {
                    _logger.LogWarning("Invalid or expired refresh token used");
                    return null;
                }

                var user = await connection.QueryFirstOrDefaultAsync<User>(
                    "SELECT * FROM Users WHERE UserId = @UserId AND IsActive = 1", 
                    new { UserId = token.UserId });

                if (user == null)
                {
                    _logger.LogWarning("User not found for refresh token");
                    return null;
                }

                // Revoke old token
                await RevokeTokenAsync(refreshToken, ipAddress);

                // Generate new tokens
                var newAccessToken = _jwtTokenService.GenerateAccessToken(user);
                var newRefreshToken = _jwtTokenService.GenerateRefreshToken();

                await SaveRefreshTokenAsync(user.UserId, newRefreshToken, ipAddress);

                _logger.LogInformation("Tokens refreshed for user {UserId}", user.UserId);

                return new AuthResponse
                {
                    Token = newAccessToken,
                    RefreshToken = newRefreshToken,
                    Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                    User = MapToUserInfo(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                throw;
            }
        }

        public async Task<bool> RevokeTokenAsync(string refreshToken, string ipAddress)
        {
            try
            {
                using var connection = _context.CreateConnection();

                var result = await connection.ExecuteAsync(@"
                    UPDATE RefreshTokens 
                    SET IsRevoked = 1, RevokedAt = @RevokedAt, RevokedBy = @RevokedBy 
                    WHERE Token = @Token",
                    new 
                    { 
                        Token = refreshToken, 
                        RevokedAt = DateTime.UtcNow, 
                        RevokedBy = ipAddress 
                    });

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                return false;
            }
        }

        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request)
        {
            try
            {
                using var connection = _context.CreateConnection();

                var user = await connection.QueryFirstOrDefaultAsync<User>(
                    "SELECT * FROM Users WHERE UserId = @UserId", 
                    new { UserId = userId });

                if (user == null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                {
                    _logger.LogWarning("Invalid current password for user {UserId}", userId);
                    return false;
                }

                var newHashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

                var result = await connection.ExecuteAsync(
                    "UPDATE Users SET PasswordHash = @PasswordHash WHERE UserId = @UserId",
                    new { PasswordHash = newHashedPassword, UserId = userId });

                _logger.LogInformation("Password changed successfully for user {UserId}", userId);
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", userId);
                return false;
            }
        }

        public async Task<UserInfo?> GetUserByIdAsync(int userId)
        {
            try
            {
                using var connection = _context.CreateConnection();

                var user = await connection.QueryFirstOrDefaultAsync<User>(
                    "SELECT * FROM Users WHERE UserId = @UserId AND IsActive = 1", 
                    new { UserId = userId });

                return user != null ? MapToUserInfo(user) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID: {UserId}", userId);
                return null;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            return await Task.FromResult(_jwtTokenService.ValidateToken(token));
        }

        public async Task<int?> GetUserIdFromTokenAsync(string token)
        {
            try
            {
                var principal = _jwtTokenService.GetPrincipalFromToken(token);
                if (principal == null) return null;

                var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                    return null;

                return await Task.FromResult(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting user ID from token");
                return null;
            }
        }

        public async Task<bool> LogoutAsync(string refreshToken)
        {
            return await RevokeTokenAsync(refreshToken, "logout");
        }

        private async Task SaveRefreshTokenAsync(int userId, string refreshToken, string ipAddress)
        {
            using var connection = _context.CreateConnection();

            var token = new RefreshToken
            {
                Token = refreshToken,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
                CreatedByIp = ipAddress
            };

            await connection.ExecuteAsync(@"
                INSERT INTO RefreshTokens (Token, UserId, CreatedAt, ExpiresAt, CreatedByIp, IsRevoked) 
                VALUES (@Token, @UserId, @CreatedAt, @ExpiresAt, @CreatedByIp, 0)", token);
        }

        private static UserInfo MapToUserInfo(User user)
        {
            return new UserInfo
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName ?? string.Empty,
                Role = user.Role ?? "User",
                IsActive = user.IsActive
            };
        }

        public async Task<UserInfo?> GetUserByTokenAsync(string token)
        {
            try
            {
                // First, validate the token and extract user ID
                var userId = await GetUserIdFromTokenAsync(token);
                if (userId == null)
                {
                    _logger.LogWarning("Unable to extract user ID from token");
                    return null;
                }

                // Then get the full user information
                var user = await GetUserByIdAsync(userId.Value);
                if (user == null)
                {
                    _logger.LogWarning("User not found for ID: {UserId}", userId);
                    return null;
                }

                _logger.LogDebug("Successfully retrieved user {UserId} from token", userId);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by token");
                return null;
            }
        }

        /// <summary>
        /// Checks if the current user's token is expired based on PrincipalDto
        /// </summary>
        /// <param name="principal">Auto-injected by middleware containing user info</param>
        /// <returns>True if token is expired, False if token is still valid</returns>
        public async Task<bool> IsTokenExpiredByUserId(PrincipalDto principal)
        {
            try
            {
                if (principal == null || !principal.IsAuthenticated)
                {
                    _logger.LogWarning("PrincipalDto is null or user is not authenticated");
                    return true; // Not authenticated means expired
                }

                if (!principal.UserId.HasValue)
                {
                    _logger.LogWarning("UserId not found in PrincipalDto");
                    return true; // No user ID means expired
                }

                // Check if user still exists and is active in database
                using var connection = _context.CreateConnection();
                var user = await connection.QueryFirstOrDefaultAsync<User>(
                    "SELECT UserId, IsActive FROM Users WHERE UserId = @UserId",
                    new { UserId = principal.UserId.Value });

                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found in database", principal.UserId);
                    return true; // User doesn't exist means token is invalid
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("User {UserId} is inactive", principal.UserId);
                    return true; // Inactive user means token should be considered expired
                }

                // Check if there are any active refresh tokens for this user
                var activeRefreshTokenCount = await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM RefreshTokens WHERE UserId = @UserId AND IsRevoked = 0 AND ExpiresAt > @Now",
                    new { UserId = principal.UserId.Value, Now = DateTime.UtcNow });

                if (activeRefreshTokenCount == 0)
                {
                    _logger.LogWarning("No active refresh tokens found for user {UserId}", principal.UserId);
                    return true; // No active refresh tokens means user should re-authenticate
                }

                _logger.LogDebug("Token is valid for user {UserId}", principal.UserId);
                return false; // Token is valid
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking token expiration for user");
                return true; // Error means expired for safety
            }
        }

        /// <summary>
        /// Gets detailed token status for the current user
        /// </summary>
        /// <param name="principal">Auto-injected by middleware</param>
        /// <returns>Detailed token status information</returns>
        public async Task<object> GetUserTokenStatus(PrincipalDto principal)
        {
            try
            {
                if (principal == null)
                {
                    return new
                    {
                        isValid = false,
                        isExpired = true,
                        error = "No principal data available",
                        userId = (int?)null
                    };
                }

                if (!principal.IsAuthenticated || !principal.UserId.HasValue)
                {
                    return new
                    {
                        isValid = false,
                        isExpired = true,
                        error = "User not authenticated",
                        userId = principal.UserId,
                        principalData = new
                        {
                            isAuthenticated = principal.IsAuthenticated,
                            ipAddress = principal.IpAddress
                        }
                    };
                }

                using var connection = _context.CreateConnection();

                // Check user status
                var user = await connection.QueryFirstOrDefaultAsync<User>(
                    "SELECT UserId, Username, Email, IsActive, CreatedAt FROM Users WHERE UserId = @UserId",
                    new { UserId = principal.UserId.Value });

                if (user == null)
                {
                    return new
                    {
                        isValid = false,
                        isExpired = true,
                        error = "User not found in database",
                        userId = principal.UserId
                    };
                }

                // Get active refresh tokens
                var refreshTokens = await connection.QueryAsync<RefreshToken>(
                    "SELECT Token, CreatedAt, ExpiresAt, CreatedByIp, IsRevoked FROM RefreshTokens WHERE UserId = @UserId ORDER BY CreatedAt DESC",
                    new { UserId = principal.UserId.Value });

                var activeTokens = refreshTokens.Where(rt => !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow).ToList();
                var isExpired = !user.IsActive || activeTokens.Count == 0;

                return new
                {
                    isValid = !isExpired,
                    isExpired = isExpired,
                    userId = principal.UserId,
                    userInfo = new
                    {
                        username = user.Username,
                        email = user.Email,
                        isActive = user.IsActive,
                        memberSince = user.CreatedAt
                    },
                    principalInfo = new
                    {
                        role = principal.Role,
                        email = principal.Email,
                        ipAddress = principal.IpAddress,
                        loginTime = principal.LoginTime,
                        isAuthenticated = principal.IsAuthenticated
                    },
                    tokenInfo = new
                    {
                        totalRefreshTokens = refreshTokens.Count(),
                        activeRefreshTokens = activeTokens.Count,
                        latestTokenCreated = refreshTokens.FirstOrDefault()?.CreatedAt,
                        nextTokenExpiry = activeTokens.OrderBy(rt => rt.ExpiresAt).FirstOrDefault()?.ExpiresAt
                    },
                    checkedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user token status");
                return new
                {
                    isValid = false,
                    isExpired = true,
                    error = ex.Message,
                    userId = principal?.UserId
                };
            }
        }

        /// <summary>
        /// Simple method that just returns the current user ID from PrincipalDto
        /// </summary>
        /// <param name="principal">Auto-injected by middleware</param>
        /// <returns>Current user's ID</returns>
        public int? GetCurrentUserIdFromPrincipal(PrincipalDto principal)
        {
            try
            {
                if (principal?.IsAuthenticated == true && principal.UserId.HasValue)
                {
                    _logger.LogDebug("Retrieved user ID {UserId} from PrincipalDto", principal.UserId);
                    return principal.UserId;
                }

                _logger.LogWarning("Unable to get user ID from PrincipalDto - not authenticated or missing UserId");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user ID from PrincipalDto");
                return null;
            }
        }
    }
}