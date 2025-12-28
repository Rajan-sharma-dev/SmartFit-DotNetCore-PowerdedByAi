using MiddleWareWebApi.Models.Identity;

namespace MiddleWareWebApi.Services.Interfaces
{
    /// <summary>
    /// AI Command Interpreter that understands natural language and executes application actions
    /// </summary>
    public interface IAiCommandInterpreter
    {
        // Main command interpretation and execution
        Task<CommandExecutionResult> ExecuteCommandAsync(string userPrompt, PrincipalDto? principal = null);
        Task<CommandAnalysisResult> AnalyzeCommandAsync(string userPrompt);
        
        // Command validation and context
        Task<bool> ValidateCommandAsync(string userPrompt, PrincipalDto? principal = null);
        Task<List<string>> GetCommandSuggestionsAsync(string partialPrompt);
    }

    // Command Analysis Result
    public class CommandAnalysisResult
    {
        public CommandType Type { get; set; }
        public string Intent { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public bool RequiresAuthentication { get; set; }
        public string Confidence { get; set; } = string.Empty; // High, Medium, Low
        public string ActionDescription { get; set; } = string.Empty;
    }

    // Command Execution Result
    public class CommandExecutionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public string? AiResponse { get; set; }
        public List<string> Errors { get; set; } = new();
        public CommandType ExecutedCommand { get; set; }
    }

    // Command Types
    public enum CommandType
    {
        Unknown,
        CreateTask,
        UpdateTask,
        DeleteTask,
        ListTasks,
        AddComment,
        ChangeTaskStatus,
        AssignTask,
        SetDueDate,
        CreateProject,
        UpdateProject,
        GenerateReport,
        GetUserInfo,
        AnalyzeProgress,
        PlanSprint,
        Greeting
    }
}