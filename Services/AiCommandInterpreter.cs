using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using MiddleWareWebApi.Models.Configuration;
using MiddleWareWebApi.Services.Interfaces;
using MiddleWareWebApi.Models.Identity;
using MiddleWareWebApi.Models;
using MiddleWareWebApi.Models.Prompts;
using System.Net.Http.Headers;
using System.Collections.Concurrent;

namespace MiddleWareWebApi.Services
{
    /// <summary>
    /// AI Command Interpreter that understands user intent and executes real application actions.
    /// Uses external prompt templates for maintainability and scalability.
    /// </summary>
    public class AiCommandInterpreter : IAiCommandInterpreter
    {
        private readonly IPromptOrchestrationService _promptOrchestration;
        private readonly ILogger<AiCommandInterpreter> _logger;
        private readonly TaskService _taskService;
        private readonly UserService _userService;
        private readonly IOpenAiProjectService _projectAiService;

        public AiCommandInterpreter(
            IPromptOrchestrationService promptOrchestration,
            ILogger<AiCommandInterpreter> logger,
            TaskService taskService,
            UserService userService,
            IOpenAiProjectService projectAiService)
        {
            _promptOrchestration = promptOrchestration;
            _logger = logger;
            _taskService = taskService;
            _userService = userService;
            _projectAiService = projectAiService;
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
            try
            {
                var placeholders = new Dictionary<string, string>
                {
                    ["USER_INPUT"] = userPrompt
                };

                var response = await _promptOrchestration.ExecutePromptAsync("DetectCommandType", placeholders);
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
            try
            {
                var placeholders = new Dictionary<string, string>
                {
                    ["USER_INPUT"] = userPrompt
                };

                var response = await _promptOrchestration.ExecutePromptAsync("CreateTask", placeholders);
                return ParseAnalysisResponse(response, CommandType.CreateTask);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing task creation for: {Prompt}", userPrompt);
                return new CommandAnalysisResult { Type = CommandType.CreateTask, Confidence = "Low" };
            }
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
            try
            {
                var placeholders = new Dictionary<string, string>
                {
                    ["USER_INPUT"] = userPrompt
                };

                var response = await _promptOrchestration.ExecutePromptAsync("ListTasks", placeholders);
                return ParseAnalysisResponse(response, CommandType.ListTasks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing task listing for: {Prompt}", userPrompt);
                return new CommandAnalysisResult { Type = CommandType.ListTasks, Confidence = "Low" };
            }
        }

        private async Task<CommandAnalysisResult> AnalyzeTaskUpdateAsync(string userPrompt)
        {
            // For now, use a simple fallback since we don't have UpdateTask prompts yet
            return new CommandAnalysisResult 
            { 
                Type = CommandType.UpdateTask, 
                Intent = "User wants to update an existing task",
                Parameters = ExtractUpdateParameters(userPrompt),
                RequiresAuthentication = true,
                Confidence = "Medium"
            };
        }

        private async Task<CommandAnalysisResult> AnalyzeTaskDeletionAsync(string userPrompt)
        {
            // For now, use a simple fallback since we don't have DeleteTask prompts yet
            return new CommandAnalysisResult 
            { 
                Type = CommandType.DeleteTask, 
                Intent = "User wants to delete a task",
                Parameters = ExtractDeleteParameters(userPrompt),
                RequiresAuthentication = true,
                Confidence = "Medium"
            };
        }

        private async Task<CommandAnalysisResult> AnalyzeCommentAdditionAsync(string userPrompt)
        {
            // For now, use a simple fallback since we don't have AddComment prompts yet
            return new CommandAnalysisResult 
            { 
                Type = CommandType.AddComment, 
                Intent = "User wants to add a comment to a task",
                Parameters = ExtractCommentParameters(userPrompt),
                RequiresAuthentication = true,
                Confidence = "Medium"
            };
        }

        private async Task<CommandAnalysisResult> AnalyzeStatusChangeAsync(string userPrompt)
        {
            // For now, use a simple fallback since we don't have ChangeTaskStatus prompts yet
            return new CommandAnalysisResult 
            { 
                Type = CommandType.ChangeTaskStatus, 
                Intent = "User wants to change task status",
                Parameters = ExtractStatusChangeParameters(userPrompt),
                RequiresAuthentication = true,
                Confidence = "Medium"
            };
        }

        private async Task<CommandAnalysisResult> AnalyzeProgressRequestAsync(string userPrompt)
        {
            // For now, use a simple fallback since we don't have AnalyzeProgress prompts yet
            return new CommandAnalysisResult 
            { 
                Type = CommandType.AnalyzeProgress, 
                Intent = "User wants to analyze their progress",
                Parameters = new Dictionary<string, object>
                {
                    ["timeRange"] = "month",
                    ["scope"] = "overall"
                },
                RequiresAuthentication = true,
                Confidence = "High"
            };
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

        /// <summary>
        /// Parses AI response JSON into CommandAnalysisResult
        /// </summary>
        private CommandAnalysisResult ParseAnalysisResponse(string response, CommandType expectedType)
        {
            try
            {
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
                _logger.LogWarning(ex, "Error parsing analysis response for {Type}", expectedType);
                return new CommandAnalysisResult { Type = expectedType, Confidence = "Low" };
            }
        }

        /// <summary>
        /// Simple fallback parameter extraction for commands without prompt templates yet
        /// </summary>
        private Dictionary<string, object> ExtractUpdateParameters(string userPrompt)
        {
            var parameters = new Dictionary<string, object>();
            
            // Simple regex-based extraction as fallback
            var taskIdMatch = System.Text.RegularExpressions.Regex.Match(userPrompt, @"task\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (taskIdMatch.Success && int.TryParse(taskIdMatch.Groups[1].Value, out int taskId))
            {
                parameters["taskId"] = taskId;
            }

            if (userPrompt.Contains("title", StringComparison.OrdinalIgnoreCase))
            {
                var titleMatch = System.Text.RegularExpressions.Regex.Match(userPrompt, @"title\s+(?:to\s+)?['""]([^'""]+)['""]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (titleMatch.Success)
                {
                    parameters["title"] = titleMatch.Groups[1].Value;
                }
            }

            if (userPrompt.Contains("priority", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var priority in new[] { "high", "low", "medium", "critical" })
                {
                    if (userPrompt.Contains(priority, StringComparison.OrdinalIgnoreCase))
                    {
                        parameters["priority"] = char.ToUpper(priority[0]) + priority.Substring(1);
                        break;
                    }
                }
            }

            return parameters;
        }

        private Dictionary<string, object> ExtractDeleteParameters(string userPrompt)
        {
            var parameters = new Dictionary<string, object>();
            
            var taskIdMatch = System.Text.RegularExpressions.Regex.Match(userPrompt, @"task\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (taskIdMatch.Success && int.TryParse(taskIdMatch.Groups[1].Value, out int taskId))
            {
                parameters["taskId"] = taskId;
            }

            return parameters;
        }

        private Dictionary<string, object> ExtractCommentParameters(string userPrompt)
        {
            var parameters = new Dictionary<string, object>();
            
            var taskIdMatch = System.Text.RegularExpressions.Regex.Match(userPrompt, @"task\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (taskIdMatch.Success && int.TryParse(taskIdMatch.Groups[1].Value, out int taskId))
            {
                parameters["taskId"] = taskId;
            }

            var commentMatch = System.Text.RegularExpressions.Regex.Match(userPrompt, @"['""]([^'""]+)['""]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (commentMatch.Success)
            {
                parameters["comment"] = commentMatch.Groups[1].Value;
            }

            return parameters;
        }

        private Dictionary<string, object> ExtractStatusChangeParameters(string userPrompt)
        {
            var parameters = new Dictionary<string, object>();
            
            var taskIdMatch = System.Text.RegularExpressions.Regex.Match(userPrompt, @"task\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (taskIdMatch.Success && int.TryParse(taskIdMatch.Groups[1].Value, out int taskId))
            {
                parameters["taskId"] = taskId;
            }

            foreach (var status in new[] { "completed", "complete", "done", "pending", "in-progress" })
            {
                if (userPrompt.Contains(status, StringComparison.OrdinalIgnoreCase))
                {
                    parameters["status"] = status.Contains("complet") || status.Contains("done") ? "completed" : status;
                    break;
                }
            }

            return parameters;
        }

        private async Task<string> GenerateClarificationAsync(string userPrompt)
        {
            try
            {
                var placeholders = new Dictionary<string, string>
                {
                    ["USER_INPUT"] = userPrompt
                };

                return await _promptOrchestration.ExecutePromptAsync("Clarification", placeholders);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate AI clarification, using fallback");
                return "I couldn't understand your request. Please try one of these commands: 'list my tasks', 'create a new task', 'show my user info', or 'analyze my progress'.";
            }
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

        #endregion
    }
}