using System.Security.Claims;
using MiddleWareWebApi.Services;

namespace MiddleWareWebApi.MiddleWare
{
    public class JwtAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtAuthenticationMiddleware> _logger;

        public JwtAuthenticationMiddleware(
            RequestDelegate next,
            ILogger<JwtAuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            try
            {
                // 🔍 Check if this is a public service call using centralized config
                var isPublicServiceCall = PublicServicesConfig.IsPublicServiceCall(context);

                if (isPublicServiceCall)
                {
                    // 🔓 For public services, don't require authentication but still enhance if token exists
                    _logger.LogDebug("Public service call detected: {Path}", context.Request.Path);
                    
                    // Try to enhance with JWT if present (but don't require it)
                    await TryEnhanceWithJwtAsync(context, serviceProvider, required: false);
                }
                else
                {
                    // 🔒 For protected services, require authentication and enhance context
                    var jwtTokenService = serviceProvider.GetRequiredService<Services.IJwtTokenService>();
                    var token = ExtractTokenFromRequest(context);
                    
                    if (!string.IsNullOrEmpty(token))
                    {
                        var principal = jwtTokenService.GetPrincipalFromToken(token);
                        if (principal != null)
                        {
                            // Enhance existing context.User from ASP.NET Core authentication
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

                            _logger.LogDebug("User {UserId} authenticated successfully for protected service", userId);
                        }
                        else
                        {
                            _logger.LogWarning("Invalid JWT token provided for protected service");
                            context.Items["IsAuthenticated"] = false;
                        }
                    }
                    else
                    {
                        // No token for protected service - let ASP.NET Core authentication handle this
                        context.Items["IsAuthenticated"] = false;
                        _logger.LogDebug("No JWT token found for protected service call");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JWT authentication middleware");
                context.Items["IsAuthenticated"] = false;
            }

            await _next(context);
        }

        /// <summary>
        /// Try to enhance context with JWT information (optional for public services)
        /// </summary>
        private async Task TryEnhanceWithJwtAsync(HttpContext context, IServiceProvider serviceProvider, bool required)
        {
            try
            {
                var jwtTokenService = serviceProvider.GetRequiredService<Services.IJwtTokenService>();
                var token = ExtractTokenFromRequest(context);
                
                if (!string.IsNullOrEmpty(token))
                {
                    var principal = jwtTokenService.GetPrincipalFromToken(token);
                    if (principal != null)
                    {
                        // Even for public services, if user has valid token, enhance the context
                        context.User = principal;
                        
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

                        _logger.LogDebug("Enhanced public service call with user context: {UserId}", userId);
                    }
                    else if (required)
                    {
                        _logger.LogWarning("Invalid JWT token provided");
                        context.Items["IsAuthenticated"] = false;
                    }
                }
                else if (!required)
                {
                    // For public services, no token is OK
                    context.Items["IsAuthenticated"] = false;
                    _logger.LogDebug("Public service call without authentication token");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enhancing context with JWT");
                if (required)
                {
                    context.Items["IsAuthenticated"] = false;
                }
            }
        }

        /// <summary>
        /// Extract JWT token from request (cookie, header, or query)
        /// </summary>
        private static string? ExtractTokenFromRequest(HttpContext context)
        {
            // 🔐 Priority 1: Check HTTP-only cookie (automatic)
            var tokenFromCookie = context.Request.Cookies["accessToken"];
            if (!string.IsNullOrEmpty(tokenFromCookie))
            {
                return tokenFromCookie;
            }

            // 🔐 Priority 2: Check Authorization header (for API clients)
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            // 🔐 Priority 3: Check query parameter (for special cases like WebSocket)
            var tokenFromQuery = context.Request.Query["token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(tokenFromQuery))
            {
                return tokenFromQuery;
            }

            return null;
        }
    }
}
