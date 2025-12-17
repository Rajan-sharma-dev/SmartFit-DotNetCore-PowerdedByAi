using MiddleWareWebApi.Models;
using MiddleWareWebApi.Services;
using MiddleWareWebApi.Services.Interfaces;
using System.Security.Claims;

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
                        var identityService = serviceProvider.GetRequiredService<IIdentityService>();
                        var principal = await identityService.GetUserByTokenAsync(token);
                        
                        if (principal != null)
                        {
                            // Enhance context with JWT information
                            context.Items["Principal"] = principal;

                            _logger.LogDebug("User {UserId} authenticated successfully", principal.UserId);
                        }
                        else
                        {
                            // Invalid token
                            context.Items["IsAuthenticated"] = false;
                            _logger.LogDebug("Invalid JWT token provided for public service");
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
                    var identityService = serviceProvider.GetRequiredService<IIdentityService>();
                    var principal = await identityService.GetUserByTokenAsync(token);

                    if (principal != null)
                    {
                        // Enhance context with JWT information
                        context.Items["Principal"] = principal;

                        _logger.LogDebug("User {UserId} authenticated successfully", principal.UserId);
                    }
                    else
                    {
                        // Invalid token
                        context.Items["IsAuthenticated"] = false;
                        _logger.LogDebug("Invalid JWT token provided for public service");
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
