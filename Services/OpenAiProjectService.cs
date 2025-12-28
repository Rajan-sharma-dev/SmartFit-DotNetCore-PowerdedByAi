using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using MiddleWareWebApi.Models.Configuration;
using MiddleWareWebApi.Services.Interfaces;
using MiddleWareWebApi.Models;
using MiddleWareWebApi.Models.Identity;
using System.Net.Http.Headers;

namespace MiddleWareWebApi.Services
{
    /// <summary>
    /// Advanced OpenAI service specialized for project management and software development
    /// </summary>
    public class OpenAiProjectService : IOpenAiProjectService
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAiSettings _settings;
        private readonly ILogger<OpenAiProjectService> _logger;
        private readonly ICurrentUserService _currentUserService;

        public OpenAiProjectService(
            HttpClient httpClient,
            IOptions<OpenAiSettings> settings,
            ILogger<OpenAiProjectService> logger,
            ICurrentUserService currentUserService)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
            _currentUserService = currentUserService;

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ProjectManagement-Advanced-AI/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        }

        #region Project Management AI Methods

        public async Task<string> AnalyzeProjectRequirementsAsync(string requirements)
        {
            var prompt = $@"Analyze the following project requirements and provide insights on:
1. Clarity and completeness of requirements
2. Potential ambiguities or missing information
3. Suggested priority levels
4. Technical complexity assessment
5. Resource estimation

Requirements:
{requirements}";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        public async Task<string> GenerateProjectPlanAsync(string projectDescription, string methodology = "Agile")
        {
            var prompt = $@"Create a comprehensive project plan using {methodology} methodology for:
{projectDescription}

Please include:
1. Project phases and milestones
2. High-level task breakdown
3. Estimated timeline
4. Risk considerations
5. Resource requirements
6. Success criteria";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        public async Task<string> SuggestTaskBreakdownAsync(string userStory)
        {
            var prompt = $@"Break down the following user story into specific, actionable tasks:
{userStory}

For each task, provide:
1. Task description
2. Acceptance criteria
3. Estimated effort (story points)
4. Dependencies
5. Technical considerations";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        public async Task<string> EstimateTaskEffortAsync(string taskDescription)
        {
            var prompt = $@"Estimate the effort required for the following task using story points (Fibonacci scale: 1, 2, 3, 5, 8, 13, 21):
{taskDescription}

Consider:
1. Complexity
2. Uncertainty
3. Dependencies
4. Testing requirements
5. Documentation needs

Provide reasoning for your estimate.";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        public async Task<string> GenerateSprintPlanAsync(List<TaskItem> backlogItems, int sprintDurationWeeks = 2)
        {
            var backlogSummary = string.Join("\\n", backlogItems.Select(t => 
                $"- {t.Title} (Priority: {t.Priority}, Status: {t.Status})"));

            var prompt = $@"Create a {sprintDurationWeeks}-week sprint plan from the following backlog items:
{backlogSummary}

Please provide:
1. Recommended sprint goal
2. Selected backlog items for the sprint
3. Task priorities and dependencies
4. Risk mitigation strategies
5. Definition of done criteria";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        #endregion

        #region Code Analysis Methods

        public async Task<string> AnalyzeCodeQualityAsync(string codeSnippet, string language = "C#")
        {
            var prompt = $@"Analyze the following {language} code for quality, best practices, and potential improvements:

```{language}
{codeSnippet}
```

Evaluate:
1. Code readability and maintainability
2. Performance considerations
3. Security vulnerabilities
4. Design patterns usage
5. Adherence to coding standards
6. Potential bugs or issues";

            return await GetCodeAnalysisCompletionAsync(prompt);
        }

        public async Task<string> SuggestCodeImprovementsAsync(string codeSnippet)
        {
            var prompt = $@"Suggest specific improvements for the following code:
```
{codeSnippet}
```

Provide:
1. Refactored code examples
2. Explanation of improvements
3. Performance benefits
4. Maintainability enhancements
5. Best practices applied";

            return await GetCodeAnalysisCompletionAsync(prompt);
        }

        public async Task<string> GenerateCodeDocumentationAsync(string codeSnippet)
        {
            var prompt = $@"Generate comprehensive documentation for the following code:
```
{codeSnippet}
```

Include:
1. XML documentation comments
2. Method/class descriptions
3. Parameter explanations
4. Return value descriptions
5. Usage examples
6. Exception handling documentation";

            return await GetCodeAnalysisCompletionAsync(prompt);
        }

        public async Task<string> GenerateUnitTestsAsync(string methodCode)
        {
            var prompt = $@"Generate comprehensive unit tests for the following method:
```
{methodCode}
```

Create tests for:
1. Happy path scenarios
2. Edge cases
3. Error conditions
4. Boundary values
5. Null/empty input handling

Use xUnit framework and include appropriate assertions and test data.";

            return await GetCodeAnalysisCompletionAsync(prompt);
        }

        #endregion

        #region Team Productivity Methods

        public async Task<string> AnalyzeTeamVelocityAsync(List<TaskItem> completedTasks, int sprintDuration)
        {
            var tasksSummary = string.Join("\\n", completedTasks.Select(t => 
                $"- {t.Title} (Completed: {t.CompletedDate?.ToString("yyyy-MM-dd") ?? "N/A"})"));

            var prompt = $@"Analyze team velocity based on the following completed tasks over {sprintDuration} weeks:
{tasksSummary}

Provide insights on:
1. Current velocity trends
2. Capacity planning recommendations
3. Productivity patterns
4. Potential bottlenecks
5. Improvement suggestions";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        public async Task<string> SuggestProcessImprovementsAsync(string teamChallenges)
        {
            var prompt = $@"Suggest process improvements for the following team challenges:
{teamChallenges}

Provide:
1. Root cause analysis
2. Specific improvement recommendations
3. Implementation steps
4. Success metrics
5. Timeline for changes";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        public async Task<string> GenerateMeetingAgendaAsync(string meetingType, List<string> topics)
        {
            var topicsList = string.Join("\\n", topics.Select((t, i) => $"{i + 1}. {t}"));

            var prompt = $@"Generate a structured agenda for a {meetingType} meeting with the following topics:
{topicsList}

Include:
1. Meeting objectives
2. Time allocations
3. Discussion points for each topic
4. Action items template
5. Meeting logistics";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        public async Task<string> GenerateStatusReportAsync(List<TaskItem> tasks, string reportPeriod)
        {
            var tasksSummary = string.Join("\\n", tasks.Select(t => 
                $"- {t.Title} ({t.Status}) - {t.Description}"));

            var prompt = $@"Generate a project status report for {reportPeriod} based on:
{tasksSummary}

Include:
1. Executive summary
2. Key accomplishments
3. Current status overview
4. Upcoming milestones
5. Risks and issues
6. Resource utilization
7. Next steps";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        #endregion

        #region Risk Management Methods

        public async Task<string> IdentifyProjectRisksAsync(string projectDescription)
        {
            var prompt = $@"Identify potential risks for the following project:
{projectDescription}

Categorize risks by:
1. Technical risks
2. Resource risks
3. Schedule risks
4. External risks
5. Business risks

For each risk, provide impact and probability assessments.";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        public async Task<string> SuggestRiskMitigationAsync(string riskDescription)
        {
            var prompt = $@"Suggest mitigation strategies for the following risk:
{riskDescription}

Provide:
1. Prevention strategies
2. Mitigation actions
3. Contingency plans
4. Monitoring approach
5. Escalation procedures";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        #endregion

        #region Communication Methods

        public async Task<string> GenerateStakeholderUpdateAsync(string projectStatus, List<string> achievements, List<string> challenges)
        {
            var achievementsList = string.Join("\\n", achievements.Select((a, i) => $"{i + 1}. {a}"));
            var challengesList = string.Join("\\n", challenges.Select((c, i) => $"{i + 1}. {c}"));

            var prompt = $@"Generate a professional stakeholder update with:

Project Status: {projectStatus}

Key Achievements:
{achievementsList}

Current Challenges:
{challengesList}

Format as a clear, executive-level communication including next steps and required support.";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        public async Task<string> DraftTechnicalDocumentationAsync(string feature, string technicalSpecs)
        {
            var prompt = $@"Draft technical documentation for:
Feature: {feature}
Technical Specifications: {technicalSpecs}

Include:
1. Feature overview
2. Technical architecture
3. Implementation details
4. API documentation
5. Configuration options
6. Testing approach
7. Deployment considerations";

            return await GetCodeAnalysisCompletionAsync(prompt);
        }

        #endregion

        #region Utility Methods

        public async Task<bool> TestProjectContextAsync()
        {
            try
            {
                var response = await GetProjectAdviceAsync("What are the key principles of Agile project management?");
                var isSuccessful = !string.IsNullOrEmpty(response) && response.Contains("Agile");
                
                _logger.LogInformation("Project AI connection test result: {Success}", isSuccessful);
                return isSuccessful;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Project AI connection test failed");
                return false;
            }
        }

        public async Task<string> GetProjectAdviceAsync(string question)
        {
            var prompt = $@"As a senior project manager and technical lead, provide expert advice on:
{question}

Consider current best practices in software development, Agile methodologies, and team management.";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        public async Task<string> PersonalizeResponseAsync(string baseResponse, string userRole, string projectContext)
        {
            if (!_settings.EnablePersonalization)
                return baseResponse;

            var prompt = $@"Personalize the following response for a {userRole} working on {projectContext}:
{baseResponse}

Adjust the language, depth, and focus to be most relevant for this specific role and context.";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        public async Task<string> GenerateTaskDescriptionAsync(string title, string context)
        {
            var prompt = $@"Generate a detailed, actionable task description for:
Title: {title}
Context: {context}

The description should include:
1. Clear objective and purpose
2. Specific acceptance criteria
3. Technical considerations if applicable
4. Dependencies or prerequisites
5. Definition of done

Keep it concise but comprehensive, suitable for a development team working on a fitness app.";

            return await GetCodeAnalysisCompletionAsync(prompt);
        }

        public async Task<string> AnalyzeUserProductivityAsync(PrincipalDto principal, string progressData)
        {
            var userContext = principal?.Email?.Split('@')[0] ?? "User";
            var userRole = principal?.Role ?? "Team Member";

            var prompt = $@"Analyze the productivity data for {userContext} (Role: {userRole}):
{progressData}

Provide insights on:
1. Current productivity trends
2. Performance compared to typical benchmarks
3. Areas of strength
4. Potential improvement areas
5. Actionable recommendations
6. Motivational feedback

Focus on constructive analysis that helps the user improve their project management effectiveness.";

            return await GetProjectManagementCompletionAsync(prompt);
        }

        #endregion

        #region Private Helper Methods

        private async Task<string> GetProjectManagementCompletionAsync(string prompt)
        {
            return await GetCompletionAsync(prompt, _settings.DefaultProjectContext);
        }

        private async Task<string> GetCodeAnalysisCompletionAsync(string prompt)
        {
            var codeAnalysisContext = "You are a senior software architect and code reviewer with expertise in clean code principles, design patterns, and best practices across multiple programming languages.";
            return await GetCompletionAsync(prompt, codeAnalysisContext);
        }

        private async Task<string> GetCompletionAsync(string prompt, string systemMessage)
        {
            try
            {
                var messages = new List<object>
                {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = prompt }
                };

                var requestBody = new
                {
                    model = _settings.DefaultModel,
                    messages = messages,
                    max_tokens = _settings.MaxTokens,
                    temperature = _settings.Temperature,
                    top_p = _settings.TopP
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug("Sending project AI request: {Model}", _settings.DefaultModel);

                var response = await _httpClient.PostAsync("/chat/completions", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("OpenAI Project API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    throw new HttpRequestException($"OpenAI Project API error: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OpenAiChatResponse>(responseContent);

                var assistantResponse = result?.Choices?.FirstOrDefault()?.Message?.Content ?? 
                    "I apologize, but I couldn't generate a project-specific response at this time.";
                
                _logger.LogInformation("Project AI response received successfully. Tokens used: {Tokens}", 
                    result?.Usage?.TotalTokens ?? 0);

                return assistantResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCompletionAsync for project service");
                throw;
            }
        }

        #endregion

        #region Response Models

        private class OpenAiChatResponse
        {
            public List<Choice>? Choices { get; set; }
            public Usage? Usage { get; set; }
        }

        private class Choice
        {
            public Message? Message { get; set; }
        }

        private class Message
        {
            public string Content { get; set; } = string.Empty;
        }

        private class Usage
        {
            public int TotalTokens { get; set; }
        }

        #endregion
    }
}