using System.Security.Claims;
using MiddleWareWebApi.Models.Identity;
using MiddleWareWebApi.MiddleWare;

namespace MiddleWareWebApi.Services
{
    public interface ICurrentUserService
    {
        ClaimsPrincipal Principal { get; }
        UserInfo? User { get; }
        int? UserId { get; }
        string? Username { get; }
        string? Email { get; }
        string? Role { get; }
        string? FullName { get; }
        bool IsAuthenticated { get; }
        bool IsInRole(string role);
        string? GetClaimValue(string claimType);
        UserContext ToUserContext();
    }

    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public ClaimsPrincipal Principal => 
            _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();

        public UserInfo? User
        {
            get
            {
                if (!IsAuthenticated) return null;

                return new UserInfo
                {
                    UserId = UserId ?? 0,
                    Username = Username ?? string.Empty,
                    Email = Email ?? string.Empty,
                    FullName = FullName ?? string.Empty,
                    Role = Role ?? "User",
                    IsActive = true
                };
            }
        }

        public int? UserId
        {
            get
            {
                var userIdClaim = Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return int.TryParse(userIdClaim, out var userId) ? userId : null;
            }
        }

        public string? Username => Principal.FindFirst(ClaimTypes.Name)?.Value;

        public string? Email => Principal.FindFirst(ClaimTypes.Email)?.Value;

        public string? Role => Principal.FindFirst(ClaimTypes.Role)?.Value;

        public string? FullName => Principal.FindFirst("FullName")?.Value;

        public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;

        public bool IsInRole(string role) => Principal.IsInRole(role);

        public string? GetClaimValue(string claimType) => 
            Principal.FindFirst(claimType)?.Value;

        public UserContext ToUserContext()
        {
            if (!IsAuthenticated)
            {
                return new UserContext
                {
                    IsAuthenticated = false
                };
            }

            return new UserContext
            {
                UserId = UserId?.ToString(),
                Username = Username ?? string.Empty,
                Email = Email ?? string.Empty,
                Role = Role ?? "User",
                FullName = FullName ?? string.Empty,
                IsAuthenticated = true,
                Claims = Principal.Claims.ToDictionary(c => c.Type, c => c.Value)
            };
        }
    }
}