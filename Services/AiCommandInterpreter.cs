using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using MiddleWareWebApi.Models.Configuration;
using MiddleWareWebApi.Services.Interfaces;
using MiddleWareWebApi.Models.Identity;
using MiddleWareWebApi.Models;
using System.Net.Http.Headers;
            using System.Collections.Concurrent;

namespace MiddleWareWebApi.Services
{
    /// <summary>
    /// AI Command Interpreter that understands user intent and executes real application actions
    /// </summary>
    public class AiCommandInterpreter : IAiCommandInterpreter
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAiSettings _settings;
        private readonly ILogger<AiCommandInterpreter> _logger;
        private readonly TaskService _taskService;
        private readonly UserService _userService;
        private readonly IOpenAiProjectService _projectAiService;
        private readonly SemaphoreSlim _rateLimitSemaphore;
        private readonly ConcurrentQueue<DateTime> _requestTimestamps;
        private static readonly object _lockObject = new object();

        public AiCommandInterpreter(
            HttpClient httpClient,
            IOptions<OpenAiSettings> settings,
            ILogger<AiCommandInterpreter> logger,
            TaskService taskService,
            UserService userService,
            IOpenAiProjectService projectAiService)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
            _taskService = taskService;
            _userService = userService;
            _projectAiService = projectAiService;
            _rateLimitSemaphore = new SemaphoreSlim(_settings.RequestsPerMinute, _settings.RequestsPerMinute);
            _requestTimestamps = new ConcurrentQueue<DateTime>();

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AI-Command-Interpreter/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        }

        public async Task<CommandExecutionResult> ExecuteCommandAsync(string userPrompt, PrincipalDto? principal = null)
        {
            try
            {
                _logger.LogInformation("Executing AI command: {Prompt}", userPrompt);

                // Step 1: Analyze the command to understand user intent
                var analysis = await AnalyzeCommandAsync(userPrompt);
                
                if (analysis.Type == CommandType.Unknown)
                {
                    try
                    {
                        var clarification = await GenerateClarificationAsync(userPrompt);
                        return new CommandExecutionResult
                        {
                            Success = false,
                            Message = "I couldn't understand what you want me to do. Could you please rephrase your request?",
                            AiResponse = clarification
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate AI clarification, using fallback message");
                        return new CommandExecutionResult
                        {
                            Success = false,
                            Message = "I couldn't understand what you want me to do. Could you please rephrase your request?",
                            AiResponse = "Please try one of these commands: 'list my tasks', 'create a new task', 'show my user info', or 'analyze my progress'."
                        };
                    }
                }

                // Step 2: Check authentication requirements
                if (analysis.RequiresAuthentication && principal?.IsAuthenticated != true)
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = "Authentication required for this action",
                        ExecutedCommand = analysis.Type
                    };
                }

                // Step 3: Execute the appropriate command based on type
                CommandExecutionResult result = analysis.Type switch
                {
                    CommandType.CreateTask => await ExecuteCreateTaskAsync(analysis, principal),
                    CommandType.UpdateTask => await ExecuteUpdateTaskAsync(analysis, principal),
                    CommandType.DeleteTask => await ExecuteDeleteTaskAsync(analysis, principal),
                    CommandType.ListTasks => await ExecuteListTasksAsync(analysis, principal),
                    CommandType.AddComment => await ExecuteAddCommentAsync(analysis, principal),
                    CommandType.ChangeTaskStatus => await ExecuteChangeStatusAsync(analysis, principal),
                    CommandType.GetUserInfo => await ExecuteGetUserInfoAsync(analysis, principal),
                    CommandType.AnalyzeProgress => await ExecuteAnalyzeProgressAsync(analysis, principal),
                    CommandType.Greeting => await ExecuteGreetingAsync(analysis, principal),
                    _ => new CommandExecutionResult
                    {
                        Success = false,
                        Message = $"Command type '{analysis.Type}' is not yet implemented",
                        ExecutedCommand = analysis.Type
                    }
                };

                result.ExecutedCommand = analysis.Type;
                
                _logger.LogInformation("Command executed successfully: {Type} - {Success}", 
                    analysis.Type, result.Success);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing AI command: {Prompt}", userPrompt);
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = "An error occurred while processing your request",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        private async Task<CommandExecutionResult> ExecuteGreetingAsync(CommandAnalysisResult analysis, PrincipalDto? principal)
        {
            var responses = new[]
            {
              "Hello! I'm your AI project management assistant. I can help you create tasks, list your work, analyze progress, and more. Try saying 'list my tasks' or 'create a new task'.",
              "Hi there! Ready to help you manage your tasks and projects. What would you like to do today?",
              "Hey! I'm here to help with your project management. You can ask me to create tasks, show progress, or manage your projects.",
              "Greetings! I can assist with tasks, projects, user info, progress analysis, and more. Just ask!"
            };

            var random = new Random();
            var response = responses[random.Next(responses.Length)];

            return new CommandExecutionResult
            {
                Success = true,
                Message = "Greeting processed",
                AiResponse = response
            };
        }
        public async Task<CommandAnalysisResult> AnalyzeCommandAsync(string userPrompt)
        {
            try
            {
                // Step 1: First determine the general command type
                var commandType = await DetectCommandTypeAsync(userPrompt);
                
                // Step 2: Use specialized prompt based on detected type
                return commandType switch
                {
                    CommandType.CreateTask => await AnalyzeTaskCreationAsync(userPrompt),
                    CommandType.UpdateTask => await AnalyzeTaskUpdateAsync(userPrompt),
                    CommandType.DeleteTask => await AnalyzeTaskDeletionAsync(userPrompt),
                    CommandType.ListTasks => await AnalyzeTaskListingAsync(userPrompt),
                    CommandType.AddComment => await AnalyzeCommentAdditionAsync(userPrompt),
                    CommandType.ChangeTaskStatus => await AnalyzeStatusChangeAsync(userPrompt),
                    CommandType.GetUserInfo => await AnalyzeUserInfoRequestAsync(userPrompt),
                    CommandType.AnalyzeProgress => await AnalyzeProgressRequestAsync(userPrompt),
                    CommandType.Greeting => await AnalyzeGreetingAsync(userPrompt),
                    _ => new CommandAnalysisResult 
                    { 
                        Type = CommandType.Unknown,
                        Intent = "Could not determine user intent",
                        RequiresAuthentication = false,
                        Confidence = "Low"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing command: {Prompt}", userPrompt);
                return new CommandAnalysisResult { Type = CommandType.Unknown };
            }
        }

        private async Task<CommandType> DetectCommandTypeAsync(string userPrompt)
        {
            var systemMessage = @"You are a command type classifier. Analyze the user input and return ONLY the most appropriate command type.
                                Available command types:
                                - CreateTask: Creating/adding new tasks, stories, bugs
                                - UpdateTask: Modifying existing tasks  
                                - DeleteTask: Removing/deleting tasks
                                - ListTasks: Showing/displaying tasks
                                - AddComment: Adding comments to tasks
                                - ChangeTaskStatus: Changing task completion status
                                - GetUserInfo: Getting user profile information
                                - AnalyzeProgress: Analyzing progress/productivity
                                - Greeting: Greetings, hello, hi, casual conversation
                                - Unknown: When intent is unclear";

            var prompt = $@"Classify this user input into one of the command types:
                         User Input: ""{userPrompt}""
                         
                         Return ONLY the exact command type name (e.g., 'CreateTask', 'Greeting', etc.)";

            try
            {
                var response = await GetAiCompletionAsync(prompt, systemMessage);
                return ParseCommandType(response.Trim());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error detecting command type for: {Prompt}", userPrompt);
                return CommandType.Unknown;
            }
        }

        #region Specialized Command Analyzers

        private async Task<CommandAnalysisResult> AnalyzeTaskCreationAsync(string userPrompt)
        {
            var systemMessage = @"You are a task creation specialist for SmartFit project management. Extract comprehensive task details from user input.
                                Focus on extracting: title, description, priority, task type, assignee, project, category, due date, estimation.
                                
                                Task Types: Bug, Story, Feature, Defect, Enhancement, Task
                                Priorities: Low, Medium, High, Critical
                                Categories: Authentication, UI/UX, Backend, Frontend, Database, Performance, Security, Testing, Documentation
                                Projects: SmartFit-WebApp, SmartFit-Mobile, SmartFit-Backend, SmartFit-API, SmartFit-Analytics, SmartFit-Admin
                                
                                Common SmartFit contexts:
                                - Workout tracking, fitness goals, exercise routines
                                - User profiles, authentication, social features  
                                - Nutrition tracking, meal planning, calorie counting
                                - Progress analytics, reports, dashboards
                                - Mobile app, wearable integration, notifications
                                - Admin tools, trainer management, client tracking";

            var prompt = $@"Extract task creation details from this SmartFit fitness app request:
                         User Input: ""{userPrompt}""
                         
                         Return JSON format:
                         {{
                          ""type"": ""CreateTask"",
                          ""intent"": ""User wants to create a new task"",
                          ""parameters"": {{
                            ""title"": ""extracted task title (clear and concise)"",
                            ""description"": ""extracted description if provided (optional)"",
                            ""priority"": ""Low/Medium/High/Critical (default: Medium)"",
                            ""taskType"": ""Bug/Story/Feature/Defect/Enhancement/Task (default: Task)"",
                            ""assignee"": ""extracted assignee name if mentioned"",
                            ""projectName"": ""SmartFit project if identifiable"",
                            ""category"": ""relevant category if determinable"",
                            ""estimatedHours"": ""estimated hours as number if mentioned"",
                            ""dueDate"": ""due date if specified (YYYY-MM-DD format)"",
                            ""tags"": ""comma-separated relevant tags""
                          }},
                          ""requiresAuthentication"": true,
                          ""confidence"": ""High/Medium/Low"",
                          ""actionDescription"": ""Create a new task with extracted details""
                         }}
                         
                         Examples:
                         - ""create critical bug fix login authentication issue assigned to john"" 
                           → title: ""fix login authentication issue"", taskType: ""Bug"", priority: ""Critical"", assignee: ""john"", category: ""Authentication""
                         
                         - ""add high priority story for workout tracking dashboard""
                           → title: ""workout tracking dashboard"", taskType: ""Story"", priority: ""High"", projectName: ""SmartFit-WebApp"", category: ""UI/UX""
                         
                         - ""create feature nutrition meal planning with 40 hours estimation due next friday""
                           → title: ""nutrition meal planning"", taskType: ""Feature"", estimatedHours: ""40"", projectName: ""SmartFit-WebApp""
                         
                         - ""add enhancement optimize mobile app performance for android""
                           → title: ""optimize mobile app performance for android"", taskType: ""Enhancement"", projectName: ""SmartFit-Mobile"", category: ""Performance""
                         
                         - ""create task update exercise database with new workout routines""
                           → title: ""update exercise database with new workout routines"", taskType: ""Task"", category: ""Database"", tags: ""exercises,workouts,database""";

            return await ExecuteSpecializedAnalysis(prompt, systemMessage, CommandType.CreateTask);
        }

        private async Task<CommandAnalysisResult> AnalyzeGreetingAsync(string userPrompt)
        {
            return new CommandAnalysisResult
            {
                Type = CommandType.Greeting,
                Intent = "User is greeting or having casual conversation",
                Parameters = new Dictionary<string, object>(),
                RequiresAuthentication = false,
                Confidence = "High",
                ActionDescription = "Respond with a friendly greeting and available options"
            };
        }

        private async Task<CommandAnalysisResult> AnalyzeTaskListingAsync(string userPrompt)
        {
            var systemMessage = @"You are a task listing specialist. Extract filtering criteria from user input.
                                Focus on: status filters, assignee filters, priority filters, date ranges.";

            var prompt = $@"Extract task listing criteria from this input:
                         User Input: ""{userPrompt}""
                         
                         Return JSON format:
                         {{
                          ""type"": ""ListTasks"",
                          ""intent"": ""User wants to view tasks"",
                          ""parameters"": {{
                            ""status"": ""completed/pending/all"",
                            ""assignee"": ""specific user if mentioned"",
                            ""priority"": ""High/Medium/Low if specified"",
                            ""limit"": ""number of tasks to show""
                          }},
                          ""requiresAuthentication"": true,
                          ""confidence"": ""High/Medium/Low"",
                          ""actionDescription"": ""List tasks based on specified criteria""
                         }}";

            return await ExecuteSpecializedAnalysis(prompt, systemMessage, CommandType.ListTasks);
        }

        private async Task<CommandAnalysisResult> AnalyzeTaskUpdateAsync(string userPrompt)
        {
            var systemMessage = @"You are a task update specialist. Extract task ID and update fields.
                                Focus on: task identification, field updates (title, description, priority, assignee).";

            var prompt = $@"Extract task update details from this input:
                         User Input: ""{userPrompt}""
                         
                         Return JSON format:
                         {{
                          ""type"": ""UpdateTask"",
                          ""intent"": ""User wants to update an existing task"",
                          ""parameters"": {{
                            ""taskId"": ""extracted task ID or number"",
                            ""title"": ""new title if provided"",
                            ""description"": ""new description if provided"",
                            ""priority"": ""new priority if specified"",
                            ""assignee"": ""new assignee if mentioned""
                          }},
                          ""requiresAuthentication"": true,
                          ""confidence"": ""High/Medium/Low"",
                          ""actionDescription"": ""Update specified task with new details""
                         }}";

            return await ExecuteSpecializedAnalysis(prompt, systemMessage, CommandType.UpdateTask);
        }

        private async Task<CommandAnalysisResult> AnalyzeTaskDeletionAsync(string userPrompt)
        {
            var systemMessage = @"You are a task deletion specialist. Extract task identification for deletion.
                                Focus on: task ID, task title, or other identifying information.";

            var prompt = $@"Extract task deletion details from this input:
                         User Input: ""{userPrompt}""
                         
                         Return JSON format:
                         {{
                          ""type"": ""DeleteTask"",
                          ""intent"": ""User wants to delete a task"",
                          ""parameters"": {{
                            ""taskId"": ""extracted task ID or number"",
                            ""title"": ""task title if ID not provided""
                          }},
                          ""requiresAuthentication"": true,
                          ""confidence"": ""High/Medium/Low"",
                          ""actionDescription"": ""Delete the specified task""
                         }}";

            return await ExecuteSpecializedAnalysis(prompt, systemMessage, CommandType.DeleteTask);
        }

        private async Task<CommandAnalysisResult> AnalyzeCommentAdditionAsync(string userPrompt)
        {
            var systemMessage = @"You are a comment addition specialist. Extract task ID and comment text.";

            var prompt = $@"Extract comment details from this input:
                         User Input: ""{userPrompt}""
                         
                         Return JSON format:
                         {{
                          ""type"": ""AddComment"",
                          ""intent"": ""User wants to add a comment to a task"",
                          ""parameters"": {{
                            ""taskId"": ""extracted task ID"",
                            ""comment"": ""comment text to add""
                          }},
                          ""requiresAuthentication"": true,
                          ""confidence"": ""High/Medium/Low"",
                          ""actionDescription"": ""Add comment to specified task""
                         }}";

            return await ExecuteSpecializedAnalysis(prompt, systemMessage, CommandType.AddComment);
        }

        private async Task<CommandAnalysisResult> AnalyzeStatusChangeAsync(string userPrompt)
        {
            var systemMessage = @"You are a status change specialist. Extract task ID and new status.";

            var prompt = $@"Extract status change details from this input:
                         User Input: ""{userPrompt}""
                         
                         Return JSON format:
                         {{
                          ""type"": ""ChangeTaskStatus"",
                          ""intent"": ""User wants to change task status"",
                          ""parameters"": {{
                            ""taskId"": ""extracted task ID"",
                            ""status"": ""completed/pending/in-progress""
                          }},
                          ""requiresAuthentication"": true,
                          ""confidence"": ""High/Medium/Low"",
                          ""actionDescription"": ""Change status of specified task""
                         }}";

            return await ExecuteSpecializedAnalysis(prompt, systemMessage, CommandType.ChangeTaskStatus);
        }

        private async Task<CommandAnalysisResult> AnalyzeUserInfoRequestAsync(string userPrompt)
        {
            return new CommandAnalysisResult
            {
                Type = CommandType.GetUserInfo,
                Intent = "User wants to view their profile information",
                Parameters = new Dictionary<string, object>(),
                RequiresAuthentication = true,
                Confidence = "High",
                ActionDescription = "Retrieve and display user profile information"
            };
        }

        private async Task<CommandAnalysisResult> AnalyzeProgressRequestAsync(string userPrompt)
        {
            var systemMessage = @"You are a progress analysis specialist. Extract time period and scope.";

            var prompt = $@"Extract progress analysis criteria from this input:
                         User Input: ""{userPrompt}""
                         
                         Return JSON format:
                         {{
                          ""type"": ""AnalyzeProgress"",
                          ""intent"": ""User wants to analyze their progress"",
                          ""parameters"": {{
                            ""timeRange"": ""week/month/quarter/year"",
                            ""scope"": ""tasks/projects/overall""
                          }},
                          ""requiresAuthentication"": true,
                          ""confidence"": ""High/Medium/Low"",
                          ""actionDescription"": ""Analyze user progress for specified period""
                         }}";

            return await ExecuteSpecializedAnalysis(prompt, systemMessage, CommandType.AnalyzeProgress);
        }

        private async Task<CommandAnalysisResult> ExecuteSpecializedAnalysis(string prompt, string systemMessage, CommandType expectedType)
        {
            try
            {
                var response = await GetAiCompletionAsync(prompt, systemMessage);
                
                // Extract JSON from response
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonResponse = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var analysisData = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
                    
                    return new CommandAnalysisResult
                    {
                        Type = expectedType,
                        Intent = analysisData.GetProperty("intent").GetString() ?? "",
                        Parameters = ParseParameters(analysisData.GetProperty("parameters")),
                        RequiresAuthentication = analysisData.GetProperty("requiresAuthentication").GetBoolean(),
                        Confidence = analysisData.GetProperty("confidence").GetString() ?? "Medium",
                        ActionDescription = analysisData.GetProperty("actionDescription").GetString() ?? ""
                    };
                }

                return new CommandAnalysisResult { Type = expectedType, Confidence = "Low" };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in specialized analysis for {Type}", expectedType);
                return new CommandAnalysisResult { Type = expectedType, Confidence = "Low" };
            }
        }

        #endregion

        #region Command Execution Methods

        private async Task<CommandExecutionResult> ExecuteCreateTaskAsync(CommandAnalysisResult analysis, PrincipalDto? principal)
        {
            try
            {
                // Extract enhanced parameters from specialized analysis
                var title = GetParameterValue<string>(analysis.Parameters, "title") ?? "New Task";
                var description = GetParameterValue<string>(analysis.Parameters, "description") ?? "";
                var priority = GetParameterValue<string>(analysis.Parameters, "priority") ?? "Medium";
                var taskType = GetParameterValue<string>(analysis.Parameters, "taskType") ?? "Task";
                var assignee = GetParameterValue<string>(analysis.Parameters, "assignee");
                var projectName = GetParameterValue<string>(analysis.Parameters, "projectName");
                var category = GetParameterValue<string>(analysis.Parameters, "category");
                var estimatedHoursStr = GetParameterValue<string>(analysis.Parameters, "estimatedHours");
                var dueDateStr = GetParameterValue<string>(analysis.Parameters, "dueDate");
                var tags = GetParameterValue<string>(analysis.Parameters, "tags");

                // Parse estimated hours
                int.TryParse(estimatedHoursStr, out int estimatedHours);

                // Parse due date
                DateTime? dueDate = null;
                if (!string.IsNullOrEmpty(dueDateStr))
                {
                    if (DateTime.TryParse(dueDateStr, out DateTime parsedDate))
                    {
                        dueDate = parsedDate;
                    }
                }

                // Use AI to enhance task description if minimal
                if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(title))
                {
                    try
                    {
                        var context = $"Task Type: {taskType}, Priority: {priority}";
                        if (!string.IsNullOrWhiteSpace(assignee))
                            context += $", Assigned to: {assignee}";
                        if (!string.IsNullOrWhiteSpace(projectName))
                            context += $", Project: {projectName}";
                        if (!string.IsNullOrWhiteSpace(category))
                            context += $", Category: {category}";
                        
                        description = await _projectAiService.GenerateTaskDescriptionAsync(title, context);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate AI task description, using basic description");
                        description = $"{taskType}: {title}";
                    }
                }

                var newTask = new TaskItem
                {
                    Title = title,
                    Description = description,
                    IsCompleted = false,
                    Priority = priority,
                    TaskType = taskType,
                    Status = "Pending",
                    AssignedToName = assignee,
                    ProjectName = projectName,
                    Category = category,
                    DueDate = dueDate,
                    EstimatedHours = estimatedHours,
                    Tags = tags,
                    ProgressPercentage = 0,
                    ActualHours = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                    // UserId will be set by TaskService.CreateTaskAsync
                };

                var createdTask = await _taskService.CreateTaskAsync(newTask);

                // Build detailed response message
                var responseDetails = new List<string>();
                responseDetails.Add($"📝 **Title**: {title}");
                responseDetails.Add($"🔖 **Type**: {taskType}");
                responseDetails.Add($"⚡ **Priority**: {priority}");
                
                if (!string.IsNullOrWhiteSpace(assignee))
                    responseDetails.Add($"👤 **Assigned to**: {assignee}");
                
                if (!string.IsNullOrWhiteSpace(projectName))
                    responseDetails.Add($"📁 **Project**: {projectName}");
                
                if (!string.IsNullOrWhiteSpace(category))
                    responseDetails.Add($"🏷️ **Category**: {category}");
                
                if (dueDate.HasValue)
                    responseDetails.Add($"📅 **Due Date**: {dueDate.Value:yyyy-MM-dd}");
                
                if (estimatedHours > 0)
                    responseDetails.Add($"⏱️ **Estimated**: {estimatedHours} hours");
                
                if (!string.IsNullOrWhiteSpace(tags))
                    responseDetails.Add($"🏷️ **Tags**: {tags}");
                    
                if (!string.IsNullOrWhiteSpace(description))
                    responseDetails.Add($"📄 **Description**: {(description.Length > 100 ? description.Substring(0, 100) + "..." : description)}");

                var detailedResponse = string.Join("\n", responseDetails);

                return new CommandExecutionResult
                {
                    Success = true,
                    Message = $"Task '{title}' created successfully with {taskType.ToLower()} type and {priority.ToLower()} priority",
                    Data = new 
                    {
                        Task = createdTask,
                        ExtractedParameters = new
                        {
                            Title = title,
                            TaskType = taskType,
                            Priority = priority,
                            Assignee = assignee,
                            ProjectName = projectName,
                            Category = category,
                            EstimatedHours = estimatedHours,
                            DueDate = dueDate,
                            Tags = tags,
                            Description = description
                        }
                    },
                    AiResponse = $"✅ **Task Created Successfully!**\n\n{detailedResponse}\n\n" +
                                $"🤖 I extracted these details from your request and {(string.IsNullOrWhiteSpace(GetParameterValue<string>(analysis.Parameters, "description")) ? "generated a detailed description to help with implementation." : "used your provided description.")}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating task: {Message}", ex.Message);
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = "Failed to create task",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        private async Task<CommandExecutionResult> ExecuteListTasksAsync(CommandAnalysisResult analysis, PrincipalDto? principal)
        {
            try
            {
                // Extract filtering parameters from specialized analysis
                var statusFilter = GetParameterValue<string>(analysis.Parameters, "status")?.ToLowerInvariant() ?? "all";
                var assigneeFilter = GetParameterValue<string>(analysis.Parameters, "assignee");
                var priorityFilter = GetParameterValue<string>(analysis.Parameters, "priority");
                var limitStr = GetParameterValue<string>(analysis.Parameters, "limit");
                int.TryParse(limitStr, out int limit);
                if (limit == 0) limit = 10; // Default limit

                // Apply filters based on extracted parameters using the new TaskService method
                var tasks = await _taskService.GetTasksWithFiltersAsync(
                    status: statusFilter != "all" ? statusFilter : null,
                    priority: priorityFilter,
                    assignedToName: assigneeFilter
                );
                var taskList = tasks.ToList();

                // Apply limit
                var limitedTasks = taskList.Take(limit).ToList();
                var hasMoreTasks = taskList.Count > limit;

                var message = taskList.Count switch
                {
                    0 => GetFilteredMessage(statusFilter, assigneeFilter, priorityFilter, "No tasks found"),
                    1 => GetFilteredMessage(statusFilter, assigneeFilter, priorityFilter, "Found 1 task"),
                    _ => GetFilteredMessage(statusFilter, assigneeFilter, priorityFilter, $"Found {taskList.Count} tasks")
                };

                var taskSummaries = limitedTasks.Select((t, index) => 
                    $"{index + 1}. **{t.Title}** - {GetStatusEmoji(t.Status)} {t.Status} - 🔥 {t.Priority}" + 
                    (string.IsNullOrEmpty(t.AssignedToName) ? "" : $" - 👤 {t.AssignedToName}")).ToList();

                // Build enhanced AI response
                var filterInfo = new List<string>();
                if (statusFilter != "all") filterInfo.Add($"Status: {statusFilter}");
                if (!string.IsNullOrWhiteSpace(assigneeFilter)) filterInfo.Add($"Assignee: {assigneeFilter}");
                if (!string.IsNullOrWhiteSpace(priorityFilter)) filterInfo.Add($"Priority: {priorityFilter}");
                
                var filterText = filterInfo.Count > 0 ? $" (Filtered by: {string.Join(", ", filterInfo)})" : "";

                var aiResponse = taskList.Count > 0 
                    ? $"📋 **Your Tasks{filterText}**\n\n{string.Join("\n", taskSummaries)}" +
                      (hasMoreTasks ? $"\n\n... and {taskList.Count - limit} more tasks. Use 'show more tasks' to see additional items." : "")
                    : $"📋 No tasks found{filterText.ToLowerInvariant()}. Would you like me to help you create some tasks?";

                return new CommandExecutionResult
                {
                    Success = true,
                    Message = message,
                    Data = new 
                    {
                        Tasks = limitedTasks,
                        TotalCount = taskList.Count,
                        AppliedFilters = new
                        {
                            Status = statusFilter,
                            Assignee = assigneeFilter,
                            Priority = priorityFilter,
                            Limit = limit
                        },
                        HasMoreTasks = hasMoreTasks
                    },
                    AiResponse = aiResponse
                };
            }
            catch (Exception ex)
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = "Failed to retrieve tasks",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        private string GetFilteredMessage(string status, string assignee, string priority, string baseMessage)
        {
            var filters = new List<string>();
            if (status != "all") filters.Add($"{status} status");
            if (!string.IsNullOrWhiteSpace(assignee)) filters.Add($"assigned to {assignee}");
            if (!string.IsNullOrWhiteSpace(priority)) filters.Add($"{priority} priority");
            
            var filterText = filters.Count > 0 ? $" with {string.Join(", ", filters)}" : "";
            return $"{baseMessage}{filterText}";
        }

        private string GetStatusEmoji(string status)
        {
            return status?.ToLowerInvariant() switch
            {
                "pending" => "⏳",
                "inprogress" => "🔄",
                "completed" => "✅",
                "onhold" => "⏸️",
                "cancelled" => "❌",
                _ => "📋"
            };
        }

        private async Task<CommandExecutionResult> ExecuteUpdateTaskAsync(CommandAnalysisResult analysis, PrincipalDto? principal)
        {
            try
            {
                var taskId = GetParameterValue<int>(analysis.Parameters, "taskId");
                var title = GetParameterValue<string>(analysis.Parameters, "title");
                var description = GetParameterValue<string>(analysis.Parameters, "description");
                var priority = GetParameterValue<string>(analysis.Parameters, "priority");
                var assignee = GetParameterValue<string>(analysis.Parameters, "assignee");

                if (taskId == 0)
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = "Task ID is required to update a task. Please specify which task you want to update (e.g., 'update task 5 title to...')",
                        AiResponse = "❌ I need a task ID to update a task. Please specify which task you want to update.\n\n" +
                                   "Examples:\n" +
                                   "• \"Update task 5 title to 'New Title'\"\n" +
                                   "• \"Change task 3 priority to High\"\n" +
                                   "• \"Update task 1 description\""
                    };
                }

                var existingTask = await _taskService.GetTaskByIdAsync(taskId);
                if (existingTask == null)
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = $"Task with ID {taskId} not found",
                        AiResponse = $"❌ Sorry, I couldn't find task #{taskId}. Please check the task ID and try again.\n\n" +
                                   "💡 Try 'list my tasks' to see all available tasks with their IDs."
                    };
                }

                // Track what was updated for response
                var updatedFields = new List<string>();
                var originalTitle = existingTask.Title;

                if (!string.IsNullOrWhiteSpace(title))
                {
                    existingTask.Title = title;
                    updatedFields.Add($"Title: '{originalTitle}' → '{title}'");
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    existingTask.Description = description;
                    updatedFields.Add($"Description updated");
                }

                // Update priority and assignee using the new properties
                if (!string.IsNullOrWhiteSpace(priority))
                {
                    existingTask.Priority = priority;
                    updatedFields.Add($"Priority: {existingTask.Priority} → {priority}");
                }

                if (!string.IsNullOrWhiteSpace(assignee))
                {
                    existingTask.AssignedToName = assignee;
                    updatedFields.Add($"Assigned to: {assignee}");
                }

                if (updatedFields.Count == 0)
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = "No valid update fields provided",
                        AiResponse = "❌ I didn't find any fields to update. Please specify what you'd like to change.\n\n" +
                                   "Examples:\n" +
                                   "• \"Update task 5 title to 'New Title'\"\n" +
                                   "• \"Change task 3 description to 'New description'\"\n" +
                                   "• \"Update task 1 priority to High\""
                    };
                }

                existingTask.UpdatedAt = DateTime.UtcNow;
                var success = await _taskService.UpdateTaskAsync(existingTask);

                if (success)
                {
                    var updateSummary = string.Join("\n• ", updatedFields);
                    
                    return new CommandExecutionResult
                    {
                        Success = true,
                        Message = $"Task #{taskId} '{existingTask.Title}' updated successfully",
                        Data = new 
                        {
                            Task = existingTask,
                            UpdatedFields = updatedFields,
                            UpdatedParameters = new
                            {
                                TaskId = taskId,
                                Title = title,
                                Description = description,
                                Priority = priority,
                                Assignee = assignee
                            }
                        },
                        AiResponse = $"✅ **Task #{taskId} Updated Successfully!**\n\n" +
                                    $"📝 **Task**: {existingTask.Title}\n\n" +
                                    $"**Changes Made:**\n• {updateSummary}\n\n" +
                                    $"🕒 Last updated: {DateTime.Now:yyyy-MM-dd HH:mm}"
                    };
                }
                else
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = "Failed to update task",
                        AiResponse = $"❌ Sorry, I couldn't update task #{taskId}. There might be a system issue. Please try again later."
                    };
                }
            }
            catch (Exception ex)
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = "Failed to update task",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        private async Task<CommandExecutionResult> ExecuteDeleteTaskAsync(CommandAnalysisResult analysis, PrincipalDto? principal)
        {
            try
            {
                var taskId = GetParameterValue<int>(analysis.Parameters, "taskId");

                if (taskId == 0)
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = "Task ID is required to delete a task"
                    };
                }

                var success = await _taskService.DeleteTaskAsync(taskId);

                return new CommandExecutionResult
                {
                    Success = success,
                    Message = success ? $"Task {taskId} deleted successfully" : "Failed to delete task",
                    AiResponse = success ? $"🗑️ I've deleted task {taskId} as requested." : null
                };
            }
            catch (Exception ex)
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = "Failed to delete task",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        private async Task<CommandExecutionResult> ExecuteAddCommentAsync(CommandAnalysisResult analysis, PrincipalDto? principal)
        {
            var taskId = GetParameterValue<int>(analysis.Parameters, "taskId");
            var comment = GetParameterValue<string>(analysis.Parameters, "comment") ?? "";

            if (taskId == 0)
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = "Task ID is required to add a comment",
                    AiResponse = "❌ I need a task ID to add a comment. Please specify which task you want to comment on.\n\n" +
                               "Examples:\n" +
                               "• \"Add comment 'Work in progress' to task 5\"\n" +
                               "• \"Comment on task 3: 'Need more details'\"\n" +
                               "• \"Add note to task 1: 'Completed testing'\""
                };
            }

            if (string.IsNullOrWhiteSpace(comment))
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = "Comment text is required",
                    AiResponse = $"❌ I need comment text to add to task #{taskId}. What would you like to say?\n\n" +
                               "Examples:\n" +
                               $"• \"Add comment 'Work in progress' to task {taskId}\"\n" +
                               $"• \"Comment on task {taskId}: 'Need clarification'\""
                };
            }

            try
            {
                var task = await _taskService.GetTaskByIdAsync(taskId);
                if (task == null)
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = $"Task {taskId} not found",
                        AiResponse = $"❌ Sorry, I couldn't find task #{taskId}. Please check the task ID and try again.\n\n" +
                                   "💡 Try 'list my tasks' to see all available tasks with their IDs."
                    };
                }

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                var userName = principal?.Email?.Split('@')[0] ?? "User"; // Get username part of email
                var formattedComment = $"\n\n**Comment ({timestamp} by {userName}):** {comment}";
                
                task.Description += formattedComment;
                task.UpdatedAt = DateTime.UtcNow;

                var success = await _taskService.UpdateTaskAsync(task);

                if (success)
                {
                    return new CommandExecutionResult
                    {
                        Success = true,
                        Message = "Comment added successfully",
                        Data = new 
                        {
                            Task = task,
                            AddedComment = new
                            {
                                Text = comment,
                                Author = userName,
                                Timestamp = timestamp,
                                TaskId = taskId,
                                TaskTitle = task.Title
                            }
                        },
                        AiResponse = $"💬 **Comment Added Successfully!**\n\n" +
                                    $"📝 **Task #{taskId}**: {task.Title}\n" +
                                    $"👤 **Your comment**: \"{comment}\"\n" +
                                    $"🕒 **Added**: {timestamp}\n\n" +
                                    $"The comment has been added to the task description."
                    };
                }
                else
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = "Failed to add comment",
                        AiResponse = $"❌ Sorry, I couldn't add the comment to task #{taskId}. There might be a system issue. Please try again later."
                    };
                }
            }
            catch (Exception ex)
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = "Failed to add comment",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        private async Task<CommandExecutionResult> ExecuteChangeStatusAsync(CommandAnalysisResult analysis, PrincipalDto? principal)
        {
            try
            {
                var taskId = GetParameterValue<int>(analysis.Parameters, "taskId");
                var status = GetParameterValue<string>(analysis.Parameters, "status")?.ToLowerInvariant();

                if (taskId == 0)
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = "Task ID is required to change status"
                    };
                }

                var success = await _taskService.ToggleTaskCompletionAsync(taskId);
                var updatedTask = await _taskService.GetTaskByIdAsync(taskId);
                var statusText = updatedTask?.IsCompleted == true ? "completed" : "pending";

                return new CommandExecutionResult
                {
                    Success = success,
                    Message = success ? $"Task {taskId} status changed to {statusText}" : "Failed to change task status",
                    Data = updatedTask,
                    AiResponse = success ? $"✅ I've marked task '{updatedTask?.Title}' as {statusText}" : null
                };
            }
            catch (Exception ex)
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = "Failed to change task status",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        private async Task<CommandExecutionResult> ExecuteGetUserInfoAsync(CommandAnalysisResult analysis, PrincipalDto? principal)
        {
            try
            {
                if (principal?.UserId == null)
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = "Authentication required to get user information"
                    };
                }

                var user = await _userService.GetUserByEmailAsync(principal.Email ?? "");
                if (user == null)
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = "User profile not found"
                    };
                }

                var userInfo = new
                {
                    Name = user.FullName,
                    Email = user.Email,
                    Username = user.Username,
                    Role = user.Role,
                    MemberSince = user.CreatedAt,
                    IsActive = user.IsActive
                };

                return new CommandExecutionResult
                {
                    Success = true,
                    Message = "User information retrieved successfully",
                    Data = userInfo,
                    AiResponse = $"👤 Here's your profile information:\n" +
                                $"Name: {user.FullName}\n" +
                                $"Email: {user.Email}\n" +
                                $"Role: {user.Role}\n" +
                                $"Member since: {user.CreatedAt:MMMM yyyy}"
                };
            }
            catch (Exception ex)
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = "Failed to retrieve user information",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        private async Task<CommandExecutionResult> ExecuteAnalyzeProgressAsync(CommandAnalysisResult analysis, PrincipalDto? principal)
        {
            try
            {
                if (principal?.UserId == null)
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = "Authentication required for progress analysis"
                    };
                }

                var tasks = await _taskService.GetMyTasksAsync(principal);
                var taskList = tasks.ToList();

                var completedCount = taskList.Count(t => t.IsCompleted);
                var totalCount = taskList.Count;
                var completionRate = totalCount > 0 ? (double)completedCount / totalCount * 100 : 0;

                var progressData = $"Total tasks: {totalCount}, Completed: {completedCount}, Completion rate: {completionRate:F1}%";

                var aiAnalysis = await _projectAiService.AnalyzeUserProductivityAsync(principal, progressData);

                var summary = new
                {
                    TotalTasks = totalCount,
                    CompletedTasks = completedCount,
                    PendingTasks = totalCount - completedCount,
                    CompletionRate = Math.Round(completionRate, 1),
                    Analysis = aiAnalysis
                };

                return new CommandExecutionResult
                {
                    Success = true,
                    Message = "Progress analysis completed",
                    Data = summary,
                    AiResponse = $"📊 Your Progress Summary:\n" +
                                $"Total Tasks: {totalCount}\n" +
                                $"Completed: {completedCount}\n" +
                                $"Completion Rate: {completionRate:F1}%\n\n" +
                                $"AI Analysis:\n{aiAnalysis}"
                };
            }
            catch (Exception ex)
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = "Failed to analyze progress",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        #endregion

        #region Helper Methods

        private async Task<string> GetAiCompletionAsync(string prompt, string systemMessage)
        {
            const int maxRetries = 1;
            const int baseDelayMs = 1000;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // Apply rate limiting
                    await ApplyRateLimitAsync();

                    var requestBody = new
                    {
                        model = _settings.DefaultModel,
                        messages = new[]
                        {
                            new { role = "system", content = systemMessage },
                            new { role = "user", content = prompt }
                        },
                        max_tokens = 800,
                        temperature = 0.3
                    };

                    var json = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    
                    var response = await _httpClient.PostAsync("/v1/chat/completions", content);
                    
                    // Handle specific HTTP status codes
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        
                        switch (response.StatusCode)
                        {
                            case System.Net.HttpStatusCode.TooManyRequests:
                                _logger.LogWarning("Rate limit exceeded on attempt {Attempt}. Status: {StatusCode}, Content: {Content}", 
                                    attempt + 1, response.StatusCode, errorContent);
                                
                                if (attempt < maxRetries - 1)
                                {
                                    var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                                    _logger.LogInformation("Retrying in {Delay}ms...", delay.TotalMilliseconds);
                                    await Task.Delay(delay);
                                    continue;
                                }
                                throw new InvalidOperationException("AI service rate limit exceeded after all retry attempts. Please try again later.");
                            
                            case System.Net.HttpStatusCode.Unauthorized:
                                _logger.LogError("Authentication failed. Status: {StatusCode}", response.StatusCode);
                                throw new UnauthorizedAccessException("AI service authentication failed.");
                            
                            case System.Net.HttpStatusCode.BadRequest:
                                _logger.LogError("Bad request to AI service. Status: {StatusCode}, Content: {Content}", 
                                    response.StatusCode, errorContent);
                                throw new ArgumentException("Invalid request to AI service.");
                            
                            default:
                                _logger.LogError("AI service request failed. Status: {StatusCode}, Content: {Content}", 
                                    response.StatusCode, errorContent);
                                response.EnsureSuccessStatusCode();
                                break;
                        }
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    return result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Network error when calling AI service on attempt {Attempt}", attempt + 1);
                    if (attempt < maxRetries - 1)
                    {
                        var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                        await Task.Delay(delay);
                        continue;
                    }
                    throw new InvalidOperationException("Network error occurred while contacting AI service after all retry attempts.", ex);
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogError(ex, "AI service request timed out on attempt {Attempt}", attempt + 1);
                    if (attempt < maxRetries - 1)
                    {
                        var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                        await Task.Delay(delay);
                        continue;
                    }
                    throw new TimeoutException("AI service request timed out after all retry attempts.", ex);
                }
                catch (Exception ex) when (!(ex is InvalidOperationException || ex is UnauthorizedAccessException || ex is ArgumentException))
                {
                    _logger.LogError(ex, "Error getting AI completion on attempt {Attempt}", attempt + 1);
                    if (attempt < maxRetries - 1)
                    {
                        var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                        await Task.Delay(delay);
                        continue;
                    }
                    throw;
                }
            }
            
            throw new InvalidOperationException("All retry attempts failed");
        }

        private async Task<string> GenerateClarificationAsync(string userPrompt)
        {
            var prompt = $@"The user said: ""{userPrompt}""

                I couldn't understand their intent. Generate a helpful clarification response that:
                1. Acknowledges I didn't understand
                2. Lists 3-4 common things they might have meant
                3. Asks them to be more specific

                Available actions: create task, update task, list tasks, delete task, 
                add comment, change status, get my info, analyze progress.";

            return await GetAiCompletionAsync(prompt, "You are a helpful project management AI assistant.");
        }

        private CommandType ParseCommandType(string commandTypeStr)
        {
            return commandTypeStr?.ToLowerInvariant() switch
            {
                "createtask" => CommandType.CreateTask,
                "updatetask" => CommandType.UpdateTask,
                "deletetask" => CommandType.DeleteTask,
                "listtasks" => CommandType.ListTasks,
                "addcomment" => CommandType.AddComment,
                "changetaskstatus" => CommandType.ChangeTaskStatus,
                "assigntask" => CommandType.AssignTask,
                "setduedate" => CommandType.SetDueDate,
                "createproject" => CommandType.CreateProject,
                "updateproject" => CommandType.UpdateProject,
                "generatereport" => CommandType.GenerateReport,
                "getuserinfo" => CommandType.GetUserInfo,
                "analyzeprogress" => CommandType.AnalyzeProgress,
                "plansprint" => CommandType.PlanSprint,
                "greeting" => CommandType.Greeting,
                _ => CommandType.Unknown
            };
        }

        private Dictionary<string, object> ParseParameters(JsonElement parametersElement)
        {
            var parameters = new Dictionary<string, object>();
            
            if (parametersElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in parametersElement.EnumerateObject())
                {
                    parameters[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString() ?? "",
                        JsonValueKind.Number => property.Value.GetInt32(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => property.Value.ToString()
                    };
                }
            }
            
            return parameters;
        }

        private T GetParameterValue<T>(Dictionary<string, object> parameters, string key)
        {
            if (parameters.TryGetValue(key, out var value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default(T);
                }
            }
            return default(T);
        }

        public async Task<bool> ValidateCommandAsync(string userPrompt, PrincipalDto? principal = null)
        {
            var analysis = await AnalyzeCommandAsync(userPrompt);
            return analysis.Type != CommandType.Unknown && 
                   (!analysis.RequiresAuthentication || principal?.IsAuthenticated == true);
        }

        public async Task<List<string>> GetCommandSuggestionsAsync(string partialPrompt)
        {
            var suggestions = new List<string>
            {
                "Create a new task called 'Fix login bug'",
                "List all my tasks",
                "Mark task 5 as completed", 
                "Update task 3 title to 'New feature implementation'",
                "Delete task 7",
                "Add comment 'Working on this now' to task 2",
                "Show my user information",
                "Analyze my progress this week"
            };

            if (!string.IsNullOrWhiteSpace(partialPrompt))
            {
                return suggestions.Where(s => 
                    s.Contains(partialPrompt, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return suggestions;
        }

        private async Task ApplyRateLimitAsync()
        {
            // Wait for semaphore availability
            await _rateLimitSemaphore.WaitAsync();
            
            try
            {
                var now = DateTime.UtcNow;
                _requestTimestamps.Enqueue(now);
                
                // Clean up old timestamps (older than 1 minute)
                lock (_lockObject)
                {
                    while (_requestTimestamps.TryPeek(out var oldestTimestamp) && 
                           now.Subtract(oldestTimestamp).TotalMinutes >= 1)
                    {
                        _requestTimestamps.TryDequeue(out _);
                    }
                }
                
                // If we have too many requests in the last minute, wait
                if (_requestTimestamps.Count >= _settings.RequestsPerMinute)
                {
                    var delay = TimeSpan.FromSeconds(60.0 / _settings.RequestsPerMinute);
                    _logger.LogInformation("Rate limiting: waiting {Delay}ms to avoid 429 error", delay.TotalMilliseconds);
                    await Task.Delay(delay);
                }
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }

        #endregion
    }
}