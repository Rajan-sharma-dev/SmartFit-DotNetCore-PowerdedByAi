using System.Security.Claims;
using MiddleWareWebApi.MiddleWare;

namespace MiddleWareWebApi.Extensions
{
    public static class HttpContextExtensions
    {
        public static UserContext? GetCurrentUser(this HttpContext context)
        {
            var isAuthenticated = context.Items["IsAuthenticated"] as bool? ?? false;
            
            if (!isAuthenticated || context.User?.Identity?.IsAuthenticated != true)
                return null;

            return new UserContext
            {
                UserId = context.Items["UserId"]?.ToString(),
                Username = context.Items["Username"]?.ToString() ?? string.Empty,
                Email = context.Items["UserEmail"]?.ToString() ?? string.Empty,
                Role = context.Items["UserRole"]?.ToString() ?? "User",
                FullName = context.Items["UserFullName"]?.ToString() ?? string.Empty,
                IsAuthenticated = true,
                Claims = context.User.Claims.ToDictionary(c => c.Type, c => c.Value)
            };
        }

        public static string? GetCurrentUserId(this HttpContext context)
        {
            return context.Items["UserId"]?.ToString();
        }

        public static string? GetCurrentUserRole(this HttpContext context)
        {
            return context.Items["UserRole"]?.ToString();
        }

        public static bool IsUserAuthenticated(this HttpContext context)
        {
            return context.Items["IsAuthenticated"] as bool? ?? false;
        }

        public static bool IsUserInRole(this HttpContext context, string role)
        {
            var userRole = context.GetCurrentUserRole();
            return userRole?.Equals(role, StringComparison.OrdinalIgnoreCase) == true;
        }
    }

    public static class ClaimsPrincipalExtensions
    {
        public static UserContext? ToUserContext(this ClaimsPrincipal principal)
        {
            if (principal?.Identity?.IsAuthenticated != true)
                return null;

            return new UserContext
            {
                UserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                Username = principal.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
                Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
                Role = principal.FindFirst(ClaimTypes.Role)?.Value ?? "User",
                FullName = principal.FindFirst("FullName")?.Value ?? string.Empty,
                IsAuthenticated = true,
                Claims = principal.Claims.ToDictionary(c => c.Type, c => c.Value)
            };
        }
    }
}