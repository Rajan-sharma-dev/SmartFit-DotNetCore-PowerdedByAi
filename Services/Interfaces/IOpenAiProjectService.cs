using MiddleWareWebApi.Models;
using MiddleWareWebApi.Models.Identity;

namespace MiddleWareWebApi.Services.Interfaces
{
    /// <summary>
    /// Advanced OpenAI service interface for project management specific AI functionality
    /// </summary>
    public interface IOpenAiProjectService
    {
        // Project Management AI Methods
        Task<string> AnalyzeProjectRequirementsAsync(string requirements);
        Task<string> GenerateProjectPlanAsync(string projectDescription, string methodology = "Agile");
        Task<string> SuggestTaskBreakdownAsync(string userStory);
        Task<string> EstimateTaskEffortAsync(string taskDescription);
        Task<string> GenerateSprintPlanAsync(List<TaskItem> backlogItems, int sprintDurationWeeks = 2);
        
        // Code Analysis Methods
        Task<string> AnalyzeCodeQualityAsync(string codeSnippet, string language = "C#");
        Task<string> SuggestCodeImprovementsAsync(string codeSnippet);
        Task<string> GenerateCodeDocumentationAsync(string codeSnippet);
        Task<string> GenerateUnitTestsAsync(string methodCode);
        
        // Team Productivity Methods
        Task<string> AnalyzeTeamVelocityAsync(List<TaskItem> completedTasks, int sprintDuration);
        Task<string> SuggestProcessImprovementsAsync(string teamChallenges);
        Task<string> GenerateMeetingAgendaAsync(string meetingType, List<string> topics);
        Task<string> GenerateStatusReportAsync(List<TaskItem> tasks, string reportPeriod);
        
        // Risk Management Methods
        Task<string> IdentifyProjectRisksAsync(string projectDescription);
        Task<string> SuggestRiskMitigationAsync(string riskDescription);
        
        // Communication Methods
        Task<string> GenerateStakeholderUpdateAsync(string projectStatus, List<string> achievements, List<string> challenges);
        Task<string> DraftTechnicalDocumentationAsync(string feature, string technicalSpecs);
        
        // Utility Methods
        Task<bool> TestProjectContextAsync();
        Task<string> GetProjectAdviceAsync(string question);
        Task<string> PersonalizeResponseAsync(string baseResponse, string userRole, string projectContext);
        
        // Additional AI Command Interpreter Support Methods
        Task<string> GenerateTaskDescriptionAsync(string title, string context);
        Task<string> AnalyzeUserProductivityAsync(PrincipalDto principal, string progressData);
    }
}