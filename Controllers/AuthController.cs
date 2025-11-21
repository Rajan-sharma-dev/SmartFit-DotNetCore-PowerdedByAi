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

                SetRefreshTokenCookie(response.RefreshToken);

                return Ok(new
                {
                    message = "Login successful",
                    token = response.Token,
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

                SetRefreshTokenCookie(response.RefreshToken);

                return Ok(new
                {
                    message = "Registration successful",
                    token = response.Token,
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
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? request = null)
        {
            try
            {
                var refreshToken = request?.RefreshToken ?? Request.Cookies["refreshToken"];
                
                if (string.IsNullOrEmpty(refreshToken))
                    return BadRequest(new { message = "Refresh token is required" });

                var ipAddress = GetIpAddress();
                var response = await _identityService.RefreshTokenAsync(refreshToken, ipAddress);

                if (response == null)
                    return Unauthorized(new { message = "Invalid or expired refresh token" });

                SetRefreshTokenCookie(response.RefreshToken);

                return Ok(new
                {
                    message = "Token refreshed successfully",
                    token = response.Token,
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

        [HttpPost("revoke-token")]
        [Authorize]
        public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequest? request = null)
        {
            try
            {
                var refreshToken = request?.RefreshToken ?? Request.Cookies["refreshToken"];
                
                if (string.IsNullOrEmpty(refreshToken))
                    return BadRequest(new { message = "Refresh token is required" });

                var ipAddress = GetIpAddress();
                var success = await _identityService.RevokeTokenAsync(refreshToken, ipAddress);

                if (!success)
                    return BadRequest(new { message = "Invalid refresh token" });

                return Ok(new { message = "Token revoked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token revocation");
                return StatusCode(500, new { message = "An error occurred during token revocation" });
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

                // Clear refresh token cookie
                Response.Cookies.Delete("refreshToken");

                return Ok(new { message = "Logout successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { message = "An error occurred during logout" });
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

        [HttpPost("validate-token")]
        public async Task<IActionResult> ValidateToken([FromBody] string token)
        {
            try
            {
                var isValid = await _identityService.ValidateTokenAsync(token);
                return Ok(new { isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return StatusCode(500, new { message = "An error occurred while validating token" });
            }
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(7),
                SameSite = SameSiteMode.Strict,
                Secure = true // Set to true in production with HTTPS
            };

            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
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
