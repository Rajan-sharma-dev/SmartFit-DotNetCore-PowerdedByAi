using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiddleWareWebApi.Services;
using MiddleWareWebApi.Models;

namespace MiddleWareWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // All actions require authentication
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(UserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        /// <summary>
        /// Get current user's profile - Principal is automatically available
        /// </summary>
        [HttpGet("profile")]
        public async Task<IActionResult> GetMyProfile()
        {
            try
            {
                // No need to pass user context - Principal is automatically available in service
                var user = await _userService.GetMyProfileAsync();
                if (user == null)
                    return NotFound();

                return Ok(user);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile");
                return StatusCode(500, new { message = "An error occurred while getting profile" });
            }
        }

        /// <summary>
        /// Get all users (Admin only) - Principal automatically checked in service
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                // Service automatically checks if current user is Admin using Principal
                var users = await _userService.GetAllUsersAsync();
                return Ok(users);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return StatusCode(500, new { message = "An error occurred while getting users" });
            }
        }

        /// <summary>
        /// Update user profile - Principal automatically validates ownership
        /// </summary>
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] User user)
        {
            try
            {
                // Service automatically validates user can update this profile using Principal
                var success = await _userService.UpdateUserAsync(user);
                if (!success)
                    return BadRequest(new { message = "Failed to update profile" });

                return Ok(new { message = "Profile updated successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile");
                return StatusCode(500, new { message = "An error occurred while updating profile" });
            }
        }

        /// <summary>
        /// Delete user (Admin only) - Principal automatically checked
        /// </summary>
        [HttpDelete("{userId:int}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            try
            {
                // Service automatically checks Admin role using Principal
                var success = await _userService.DeleteUserAsync(userId);
                if (!success)
                    return NotFound(new { message = "User not found" });

                return Ok(new { message = "User deleted successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                return StatusCode(500, new { message = "An error occurred while deleting user" });
            }
        }

        /// <summary>
        /// Search users with automatic authorization
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return BadRequest(new { message = "Search term is required" });

                var users = await _userService.SearchUsersAsync(searchTerm);
                return Ok(users);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users");
                return StatusCode(500, new { message = "An error occurred while searching users" });
            }
        }

        /// <summary>
        /// Get users by role with automatic authorization checks
        /// </summary>
        [HttpGet("role/{role}")]
        public async Task<IActionResult> GetUsersByRole(string role)
        {
            try
            {
                var users = await _userService.GetUsersByRoleAsync(role);
                return Ok(users);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by role");
                return StatusCode(500, new { message = "An error occurred while getting users by role" });
            }
        }

        /// <summary>
        /// Get current user's activity log
        /// </summary>
        [HttpGet("activity")]
        public async Task<IActionResult> GetMyActivity()
        {
            try
            {
                var activity = await _userService.GetMyActivityLogAsync();
                return Ok(activity);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user activity");
                return StatusCode(500, new { message = "An error occurred while getting activity" });
            }
        }
    }
}