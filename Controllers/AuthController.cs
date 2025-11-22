using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiddleWareWebApi.Models.Identity;
using MiddleWareWebApi.Services.Interfaces;
using System.Security.Claims;

namespace MiddleWareWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IIdentityService _identityService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IIdentityService identityService, ILogger<AuthController> logger)
        {
            _identityService = identityService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var ipAddress = GetIpAddress();
                var response = await _identityService.LoginAsync(request, ipAddress);

                if (response == null)
                    return Unauthorized(new { message = "Invalid email or password" });

                // 🔐 Set JWT token in HTTP-only cookie (automatic for all future requests)
                SetJwtTokenCookie(response.Token, response.Expires);
                SetRefreshTokenCookie(response.RefreshToken);

                return Ok(new
                {
                    message = "Login successful",
                    // Don't send token in response body - it's in cookie
                    expires = response.Expires,
                    user = response.User
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, new { message = "An error occurred during login" });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var ipAddress = GetIpAddress();
                var response = await _identityService.RegisterAsync(request, ipAddress);

                if (response == null)
                    return BadRequest(new { message = "User with this email or username already exists" });

                // 🔐 Set JWT token in HTTP-only cookie (automatic for all future requests)
                SetJwtTokenCookie(response.Token, response.Expires);
                SetRefreshTokenCookie(response.RefreshToken);

                return Ok(new
                {
                    message = "Registration successful",
                    // Don't send token in response body - it's in cookie
                    expires = response.Expires,
                    user = response.User
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                return StatusCode(500, new { message = "An error occurred during registration" });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken()
        {
            try
            {
                // Get refresh token from cookie
                var refreshToken = Request.Cookies["refreshToken"];
                
                if (string.IsNullOrEmpty(refreshToken))
                    return BadRequest(new { message = "Refresh token is required" });

                var ipAddress = GetIpAddress();
                var response = await _identityService.RefreshTokenAsync(refreshToken, ipAddress);

                if (response == null)
                    return Unauthorized(new { message = "Invalid or expired refresh token" });

                // 🔐 Update JWT token in HTTP-only cookie
                SetJwtTokenCookie(response.Token, response.Expires);
                SetRefreshTokenCookie(response.RefreshToken);

                return Ok(new
                {
                    message = "Token refreshed successfully",
                    expires = response.Expires,
                    user = response.User
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return StatusCode(500, new { message = "An error occurred during token refresh" });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];
                
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    await _identityService.LogoutAsync(refreshToken);
                }

                // 🔐 Clear all authentication cookies
                ClearAuthenticationCookies();

                return Ok(new { message = "Logout successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { message = "An error occurred during logout" });
            }
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var user = await _identityService.GetUserByIdAsync(userId.Value);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, new { message = "An error occurred while getting user information" });
            }
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var success = await _identityService.ChangePasswordAsync(userId.Value, request);

                if (!success)
                    return BadRequest(new { message = "Current password is incorrect" });

                return Ok(new { message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password change");
                return StatusCode(500, new { message = "An error occurred while changing password" });
            }
        }

        // 🔐 Set JWT token in HTTP-only cookie (automatic authentication)
        private void SetJwtTokenCookie(string token, DateTime expires)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,  // Prevents JavaScript access (XSS protection)
                Secure = true,    // HTTPS only (set to false for development HTTP)
                SameSite = SameSiteMode.Strict, // CSRF protection
                Expires = expires,
                Path = "/",
                IsEssential = true
            };

            Response.Cookies.Append("accessToken", token, cookieOptions);
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Set to false for development HTTP
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7),
                Path = "/",
                IsEssential = true
            };

            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }

        // 🔐 Clear all authentication cookies
        private void ClearAuthenticationCookies()
        {
            Response.Cookies.Delete("accessToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Set to false for development HTTP
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });

            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Set to false for development HTTP
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });
        }

        private string GetIpAddress()
        {
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
                return Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();

            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                return userId;
            
            return null;
        }
    }
}
