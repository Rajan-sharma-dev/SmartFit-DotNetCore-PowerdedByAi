using System.Security.Claims;
using MiddleWareWebApi.Services;

namespace MiddleWareWebApi.MiddleWare
{
    public class JwtAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly ILogger<JwtAuthenticationMiddleware> _logger;

        public JwtAuthenticationMiddleware(
            RequestDelegate next, 
            IJwtTokenService jwtTokenService,
            ILogger<JwtAuthenticationMiddleware> logger)
        {
            _next = next;
            _jwtTokenService = jwtTokenService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                var token = ExtractTokenFromRequest(context);
                
                if (!string.IsNullOrEmpty(token))
                {
                    var principal = _jwtTokenService.GetPrincipalFromToken(token);
                    if (principal != null)
                    {
                        context.User = principal;
                        
                        // Add user details to context items for easy access in services
                        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        var username = principal.FindFirst(ClaimTypes.Name)?.Value;
                        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
                        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
                        var fullName = principal.FindFirst("FullName")?.Value;

                        context.Items["UserId"] = userId;
                        context.Items["Username"] = username;
                        context.Items["UserEmail"] = email;
                        context.Items["UserRole"] = role;
                        context.Items["UserFullName"] = fullName;
                        context.Items["IsAuthenticated"] = true;

                        _logger.LogDebug("User {UserId} authenticated successfully", userId);
                    }
                    else
                    {
                        _logger.LogWarning("Invalid JWT token provided");
                        context.Items["IsAuthenticated"] = false;
                    }
                }
                else
                {
                    context.Items["IsAuthenticated"] = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JWT authentication middleware");
                context.Items["IsAuthenticated"] = false;
            }

            await _next(context);
        }

        private static string? ExtractTokenFromRequest(HttpContext context)
        {
            // Check Authorization header first
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            // Check query parameter as fallback
            var tokenFromQuery = context.Request.Query["token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(tokenFromQuery))
            {
                return tokenFromQuery;
            }

            return null;
        }
    }
}
