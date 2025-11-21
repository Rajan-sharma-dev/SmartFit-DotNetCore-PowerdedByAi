using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiddleWareWebApi.Services;
using MiddleWareWebApi.Models;

namespace MiddleWareWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // All actions require authentication
    public class TaskController : ControllerBase
    {
        private readonly TaskService _taskService;
        private readonly ILogger<TaskController> _logger;

        public TaskController(TaskService taskService, ILogger<TaskController> logger)
        {
            _taskService = taskService;
            _logger = logger;
        }

        /// <summary>
        /// Get current user's tasks - Principal automatically available
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyTasks()
        {
            try
            {
                // Service automatically gets tasks for current user using Principal
                var tasks = await _taskService.GetMyTasksAsync();
                return Ok(tasks);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user tasks");
                return StatusCode(500, new { message = "An error occurred while getting tasks" });
            }
        }

        /// <summary>
        /// Get all tasks (Admin only) - Principal automatically checked
        /// </summary>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllTasks()
        {
            try
            {
                // Service automatically checks Admin role using Principal
                var tasks = await _taskService.GetAllTasksAsync();
                return Ok(tasks);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all tasks");
                return StatusCode(500, new { message = "An error occurred while getting all tasks" });
            }
        }

        /// <summary>
        /// Create a new task - Principal automatically sets ownership
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] TaskItem task)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Service automatically assigns task to current user using Principal
                var createdTask = await _taskService.CreateTaskAsync(task);
                return CreatedAtAction(nameof(GetTaskById), new { id = createdTask.Id }, createdTask);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating task");
                return StatusCode(500, new { message = "An error occurred while creating task" });
            }
        }

        /// <summary>
        /// Get specific task by ID - Principal automatically checks ownership
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetTaskById(int id)
        {
            try
            {
                // Service automatically checks if current user can access this task
                var task = await _taskService.GetTaskByIdAsync(id);
                if (task == null)
                    return NotFound(new { message = "Task not found" });

                return Ok(task);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting task");
                return StatusCode(500, new { message = "An error occurred while getting task" });
            }
        }

        /// <summary>
        /// Update task - Principal automatically validates ownership
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] TaskItem task)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (id != task.Id)
                    return BadRequest(new { message = "ID mismatch" });

                // Service automatically validates user can update this task using Principal
                var success = await _taskService.UpdateTaskAsync(task);
                if (!success)
                    return NotFound(new { message = "Task not found" });

                return Ok(new { message = "Task updated successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating task");
                return StatusCode(500, new { message = "An error occurred while updating task" });
            }
        }

        /// <summary>
        /// Delete task - Principal automatically validates ownership
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            try
            {
                // Service automatically validates user can delete this task using Principal
                var success = await _taskService.DeleteTaskAsync(id);
                if (!success)
                    return NotFound(new { message = "Task not found" });

                return Ok(new { message = "Task deleted successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task");
                return StatusCode(500, new { message = "An error occurred while deleting task" });
            }
        }

        /// <summary>
        /// Toggle task completion status
        /// </summary>
        [HttpPatch("{id:int}/toggle")]
        public async Task<IActionResult> ToggleTaskCompletion(int id)
        {
            try
            {
                // Service automatically validates ownership using Principal
                var success = await _taskService.ToggleTaskCompletionAsync(id);
                if (!success)
                    return NotFound(new { message = "Task not found" });

                return Ok(new { message = "Task status updated successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling task completion");
                return StatusCode(500, new { message = "An error occurred while updating task status" });
            }
        }

        /// <summary>
        /// Search current user's tasks
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchMyTasks([FromQuery] string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return BadRequest(new { message = "Search term is required" });

                // Service automatically searches only current user's tasks using Principal
                var tasks = await _taskService.SearchMyTasksAsync(searchTerm);
                return Ok(tasks);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tasks");
                return StatusCode(500, new { message = "An error occurred while searching tasks" });
            }
        }

        /// <summary>
        /// Get current user's task statistics
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetMyTaskStats()
        {
            try
            {
                // Service automatically gets stats for current user using Principal
                var stats = await _taskService.GetMyTaskStatsAsync();
                return Ok(stats);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting task statistics");
                return StatusCode(500, new { message = "An error occurred while getting task statistics" });
            }
        }
    }
}
