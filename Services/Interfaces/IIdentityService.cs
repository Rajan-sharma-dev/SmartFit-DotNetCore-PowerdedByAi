using MiddleWareWebApi.Models.Identity;

namespace MiddleWareWebApi.Services.Interfaces
{
    public interface IIdentityService
    {
        Task<AuthResponse?> LoginAsync(LoginRequest request, string ipAddress);
        Task<AuthResponse?> RegisterAsync(RegisterRequest request, string ipAddress);
        Task<AuthResponse?> RefreshTokenAsync(string refreshToken, string ipAddress);
        Task<bool> RevokeTokenAsync(string refreshToken, string ipAddress);
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request);
        Task<UserInfo?> GetUserByIdAsync(int userId);
        Task<bool> ValidateTokenAsync(string token);
        Task<int?> GetUserIdFromTokenAsync(string token);
        Task<bool> LogoutAsync(string refreshToken);
        Task<UserInfo?> GetUserByTokenAsync(string token);
        Task<object> GetUserTokenStatus(PrincipalDto principal);
        public int? GetCurrentUserIdFromPrincipal(PrincipalDto principal);
    }
}