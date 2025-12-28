using Microsoft.AspNetCore.Mvc;
using MiddleWareWebApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace MiddleWareWebApi.Controllers
{
    [ApiController]
    [Route("api/ai-commands")]
    public class AiCommandController : ControllerBase
    {
        private readonly IAiCommandInterpreter _aiCommandInterpreter;
        private readonly ILogger<AiCommandController> _logger;

        public AiCommandController(IAiCommandInterpreter aiCommandInterpreter, ILogger<AiCommandController> logger)
        {
            _aiCommandInterpreter = aiCommandInterpreter;
            _logger = logger;
        }

        /// <summary>
        /// Execute natural language commands - Public endpoint for testing
        /// </summary>
        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteCommand([FromBody] CommandRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Command))
                {
                    return BadRequest(new { error = "Command is required" });
                }

                // For non-authenticated commands only
                var result = await _aiCommandInterpreter.ExecuteCommandAsync(request.Command);

                return Ok(new
                {
                    success = result.Success,
                    message = result.Message,
                    aiResponse = result.AiResponse,
                    data = result.Data,
                    executedCommand = result.ExecutedCommand.ToString(),
                    errors = result.Errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing AI command: {Command}", request.Command);
                return StatusCode(500, new { error = "Failed to execute command", details = ex.Message });
            }
        }

        /// <summary>
        /// Analyze command intent without executing - Public endpoint
        /// </summary>
        [HttpPost("analyze")]
        public async Task<IActionResult> AnalyzeCommand([FromBody] CommandRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Command))
                {
                    return BadRequest(new { error = "Command is required" });
                }

                var analysis = await _aiCommandInterpreter.AnalyzeCommandAsync(request.Command);

                return Ok(new
                {
                    commandType = analysis.Type.ToString(),
                    intent = analysis.Intent,
                    parameters = analysis.Parameters,
                    requiresAuthentication = analysis.RequiresAuthentication,
                    confidence = analysis.Confidence,
                    actionDescription = analysis.ActionDescription,
                    canExecutePublicly = !analysis.RequiresAuthentication,
                    needsAuthentication = analysis.RequiresAuthentication
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing AI command: {Command}", request.Command);
                return StatusCode(500, new { error = "Failed to analyze command", details = ex.Message });
            }
        }

        /// <summary>
        /// Get command suggestions - Public endpoint
        /// </summary>
        [HttpGet("suggestions")]
        public async Task<IActionResult> GetCommandSuggestions([FromQuery] string? partial = null)
        {
            try
            {
                var suggestions = await _aiCommandInterpreter.GetCommandSuggestionsAsync(partial ?? "");

                return Ok(new
                {
                    suggestions = suggestions,
                    count = suggestions.Count,
                    examples = new[]
                    {
                        "Create a task for implementing user authentication",
                        "List all my tasks",
                        "Mark task 5 as completed",
                        "Update task 3 description to include API integration",
                        "Show my progress this week",
                        "Add comment 'Almost done' to task 8"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting command suggestions");
                return StatusCode(500, new { error = "Failed to get suggestions", details = ex.Message });
            }
        }

        /// <summary>
        /// Get AI capabilities and command examples
        /// </summary>
        [HttpGet("capabilities")]
        public IActionResult GetCapabilities()
        {
            return Ok(new
            {
                description = "AI Command Interpreter that understands natural language and executes application actions",
                supportedCommands = new[]
                {
                    "CreateTask - Create new tasks",
                    "UpdateTask - Modify existing tasks", 
                    "DeleteTask - Remove tasks",
                    "ListTasks - Show your tasks (requires auth)",
                    "AddComment - Add comments to tasks (requires auth)",
                    "ChangeTaskStatus - Mark tasks as completed/pending (requires auth)",
                    "GetUserInfo - Get your profile information (requires auth)",
                    "AnalyzeProgress - Analyze your task completion progress (requires auth)"
                },
                examples = new
                {
                    publicCommands = new[]
                    {
                        "Create a task called 'Fix login bug'",
                        "Analyze this command: 'show my tasks'"
                    },
                    authenticatedCommands = new[]
                    {
                        "List all my tasks",
                        "Mark task 5 as completed",
                        "Update task 3 with description 'Add API validation'",
                        "Delete task 7",
                        "Add comment 'Testing completed' to task 2", 
                        "Show my user information",
                        "Analyze my progress this week"
                    }
                },
                usage = new
                {
                    publicEndpoint = "/api/ai-commands/execute",
                    authenticatedEndpoint = "/api/services/AiCommandInterpreter/ExecuteCommandAsync",
                    analysisEndpoint = "/api/ai-commands/analyze",
                    note = "For authenticated commands, use the Dynamic Service Middleware endpoint with Authorization header"
                }
            });
        }
    }

    public class CommandRequest
    {
        public string Command { get; set; } = string.Empty;
    }
}