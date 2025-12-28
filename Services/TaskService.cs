using MiddleWareWebApi.data;
using MiddleWareWebApi.Models;
using Dapper;
using MiddleWareWebApi.Models.Identity;

namespace MiddleWareWebApi.Services
{
    public class TaskService
    {
        private readonly DapperContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<TaskService> _logger;

        public TaskService(
            DapperContext context,
            ICurrentUserService currentUserService,
            ILogger<TaskService> logger)
        {
            _context = context;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        // Get all tasks for the current user
        public async Task<IEnumerable<TaskItem>> GetMyTasksAsync(PrincipalDto principal )
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            using var conn = _context.CreateConnection();
            var tasks = await conn.QueryAsync<TaskItem>(
                "SELECT * FROM TaskItems WHERE UserId = @UserId ORDER BY Priority DESC, CreatedAt DESC",
                new { UserId = _currentUserService.UserId });

            _logger.LogInformation("User {UserId} retrieved {TaskCount} tasks", 
                _currentUserService.UserId, tasks.Count());

            return tasks;
        }

        // Get all tasks (Admin only)
        public async Task<IEnumerable<TaskItem>> GetAllTasksAsync()
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            if (!_currentUserService.IsInRole("Admin"))
            {
                throw new UnauthorizedAccessException("Admin role required");
            }

            using var conn = _context.CreateConnection();
            var tasks = await conn.QueryAsync<TaskItem>(
                "SELECT * FROM TaskItems ORDER BY Priority DESC, CreatedAt DESC");

            _logger.LogInformation("Admin {UserId} retrieved all tasks ({TaskCount})", 
                _currentUserService.UserId, tasks.Count());

            return tasks;
        }

        // Create a new task for the current user
        public async Task<TaskItem> CreateTaskAsync(TaskItem task)
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            // Set the task owner to current user
            task.UserId = _currentUserService.UserId!.Value;
            task.CreatedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;

            using var conn = _context.CreateConnection();
            var sql = @"
                INSERT INTO TaskItems (
                    Title, Description, IsCompleted, Priority, TaskType, Status,
                    UserId, AssignedToUserId, AssignedToName, ProjectName, Category,
                    DueDate, StartDate, ProgressPercentage, EstimatedHours, ActualHours,
                    StoryPoints, SprintName, Tags, CreatedAt, UpdatedAt
                )
                OUTPUT INSERTED.Id
                VALUES (
                    @Title, @Description, @IsCompleted, @Priority, @TaskType, @Status,
                    @UserId, @AssignedToUserId, @AssignedToName, @ProjectName, @Category,
                    @DueDate, @StartDate, @ProgressPercentage, @EstimatedHours, @ActualHours,
                    @StoryPoints, @SprintName, @Tags, @CreatedAt, @UpdatedAt
                )";

            var taskId = await conn.QuerySingleAsync<int>(sql, task);
            task.Id = taskId;

            _logger.LogInformation("User {UserId} created task {TaskId}: {TaskTitle} - Type: {TaskType}, Priority: {Priority}", 
                _currentUserService.UserId, taskId, task.Title, task.TaskType, task.Priority);

            return task;
        }

        // Update a task (users can only update their own tasks, admins can update any)
        public async Task<bool> UpdateTaskAsync(TaskItem task)
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            using var conn = _context.CreateConnection();

            // Check if task exists and get owner
            var existingTask = await conn.QueryFirstOrDefaultAsync<TaskItem>(
                "SELECT * FROM TaskItems WHERE Id = @Id", new { Id = task.Id });

            if (existingTask == null)
            {
                return false;
            }

            // Users can only update their own tasks unless they're admin
            if (!_currentUserService.IsInRole("Admin") && 
                existingTask.UserId != _currentUserService.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to update task {TaskId} owned by {OwnerId}", 
                    _currentUserService.UserId, task.Id, existingTask.UserId);
                throw new UnauthorizedAccessException("You can only update your own tasks");
            }

            task.UpdatedAt = DateTime.UtcNow;
            // Preserve original owner and creation date
            task.UserId = existingTask.UserId;
            task.CreatedAt = existingTask.CreatedAt;

            var sql = @"
                UPDATE TaskItems 
                SET Title = @Title, 
                    Description = @Description, 
                    IsCompleted = @IsCompleted,
                    Priority = @Priority,
                    TaskType = @TaskType,
                    Status = @Status,
                    AssignedToUserId = @AssignedToUserId,
                    AssignedToName = @AssignedToName,
                    ProjectName = @ProjectName,
                    Category = @Category,
                    DueDate = @DueDate,
                    StartDate = @StartDate,
                    CompletedDate = @CompletedDate,
                    ProgressPercentage = @ProgressPercentage,
                    EstimatedHours = @EstimatedHours,
                    ActualHours = @ActualHours,
                    StoryPoints = @StoryPoints,
                    SprintName = @SprintName,
                    Tags = @Tags,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            var result = await conn.ExecuteAsync(sql, task);

            _logger.LogInformation("Task {TaskId} updated by user {UserId} - Priority: {Priority}, Status: {Status}", 
                task.Id, _currentUserService.UserId, task.Priority, task.Status);

            return result > 0;
        }

        // Delete a task (users can only delete their own tasks, admins can delete any)
        public async Task<bool> DeleteTaskAsync(int taskId)
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            using var conn = _context.CreateConnection();

            // Check if task exists and get owner
            var existingTask = await conn.QueryFirstOrDefaultAsync<TaskItem>(
                "SELECT * FROM TaskItems WHERE Id = @Id", new { Id = taskId });

            if (existingTask == null)
            {
                return false;
            }

            // Users can only delete their own tasks unless they're admin
            if (!_currentUserService.IsInRole("Admin") && 
                existingTask.UserId != _currentUserService.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to delete task {TaskId} owned by {OwnerId}", 
                    _currentUserService.UserId, taskId, existingTask.UserId);
                throw new UnauthorizedAccessException("You can only delete your own tasks");
            }

            var result = await conn.ExecuteAsync("DELETE FROM TaskItems WHERE Id = @Id", 
                new { Id = taskId });

            _logger.LogInformation("Task {TaskId} deleted by user {UserId}", 
                taskId, _currentUserService.UserId);

            return result > 0;
        }

        // Get a specific task by ID (with ownership check)
        public async Task<TaskItem?> GetTaskByIdAsync(int taskId)
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            using var conn = _context.CreateConnection();
            var task = await conn.QueryFirstOrDefaultAsync<TaskItem>(
                "SELECT * FROM TaskItems WHERE Id = @Id", new { Id = taskId });

            if (task == null)
            {
                return null;
            }

            // Users can only view their own tasks unless they're admin
            if (!_currentUserService.IsInRole("Admin") && 
                task.UserId != _currentUserService.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to access task {TaskId} owned by {OwnerId}", 
                    _currentUserService.UserId, taskId, task.UserId);
                throw new UnauthorizedAccessException("You can only view your own tasks");
            }

            return task;
        }

        // Toggle task completion status
        public async Task<bool> ToggleTaskCompletionAsync(int taskId)
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            using var conn = _context.CreateConnection();

            // Check if task exists and get owner
            var existingTask = await conn.QueryFirstOrDefaultAsync<TaskItem>(
                "SELECT * FROM TaskItems WHERE Id = @Id", new { Id = taskId });

            if (existingTask == null)
            {
                return false;
            }

            // Users can only toggle their own tasks unless they're admin
            if (!_currentUserService.IsInRole("Admin") && 
                existingTask.UserId != _currentUserService.UserId)
            {
                throw new UnauthorizedAccessException("You can only modify your own tasks");
            }

            var newIsCompleted = !existingTask.IsCompleted;
            var newStatus = newIsCompleted ? "Completed" : "Pending";
            var newProgressPercentage = newIsCompleted ? 100 : existingTask.ProgressPercentage;
            var completedDate = newIsCompleted ? DateTime.UtcNow : (DateTime?)null;

            var result = await conn.ExecuteAsync(@"
                UPDATE TaskItems 
                SET IsCompleted = @IsCompleted, 
                    Status = @Status,
                    ProgressPercentage = @ProgressPercentage,
                    CompletedDate = @CompletedDate,
                    UpdatedAt = @UpdatedAt 
                WHERE Id = @Id",
                new { 
                    IsCompleted = newIsCompleted,
                    Status = newStatus,
                    ProgressPercentage = newProgressPercentage,
                    CompletedDate = completedDate,
                    UpdatedAt = DateTime.UtcNow, 
                    Id = taskId 
                });

            _logger.LogInformation("Task {TaskId} completion toggled to {Status} by user {UserId}", 
                taskId, newStatus, _currentUserService.UserId);

            return result > 0;
        }

        // Search tasks for the current user
        public async Task<IEnumerable<TaskItem>> SearchMyTasksAsync(string searchTerm)
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            using var conn = _context.CreateConnection();
            var tasks = await conn.QueryAsync<TaskItem>(@"
                SELECT * FROM TaskItems 
                WHERE UserId = @UserId 
                AND (Title LIKE @SearchTerm 
                     OR Description LIKE @SearchTerm 
                     OR Tags LIKE @SearchTerm
                     OR ProjectName LIKE @SearchTerm
                     OR Category LIKE @SearchTerm)
                ORDER BY Priority DESC, CreatedAt DESC",
                new { 
                    UserId = _currentUserService.UserId, 
                    SearchTerm = $"%{searchTerm}%" 
                });

            _logger.LogInformation("User {UserId} searched tasks with term: {SearchTerm}, found {Count} results", 
                _currentUserService.UserId, searchTerm, tasks.Count());

            return tasks;
        }

        // Get task statistics for current user
        public async Task<object> GetMyTaskStatsAsync()
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            using var conn = _context.CreateConnection();
            var stats = await conn.QueryFirstAsync(@"
                SELECT 
                    COUNT(*) as TotalTasks,
                    SUM(CASE WHEN IsCompleted = 1 THEN 1 ELSE 0 END) as CompletedTasks,
                    SUM(CASE WHEN IsCompleted = 0 THEN 1 ELSE 0 END) as PendingTasks,
                    SUM(CASE WHEN Priority = 'Critical' THEN 1 ELSE 0 END) as CriticalTasks,
                    SUM(CASE WHEN Priority = 'High' THEN 1 ELSE 0 END) as HighPriorityTasks,
                    SUM(CASE WHEN Status = 'InProgress' THEN 1 ELSE 0 END) as InProgressTasks,
                    SUM(CASE WHEN DueDate IS NOT NULL AND DueDate < GETUTCDATE() AND Status != 'Completed' THEN 1 ELSE 0 END) as OverdueTasks,
                    AVG(CAST(ProgressPercentage AS FLOAT)) as AverageProgress
                FROM TaskItems 
                WHERE UserId = @UserId",
                new { UserId = _currentUserService.UserId });

            _logger.LogInformation("User {UserId} retrieved enhanced task statistics", 
                _currentUserService.UserId);

            return stats;
        }

        // Get filtered tasks for AI Command Interpreter
        public async Task<IEnumerable<TaskItem>> GetTasksWithFiltersAsync(
            string? status = null, 
            string? priority = null, 
            string? taskType = null, 
            string? assignedToName = null,
            string? projectName = null,
            bool? isOverdue = null)
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            using var conn = _context.CreateConnection();
            
            var whereClauses = new List<string> { "UserId = @UserId" };
            var parameters = new Dictionary<string, object> { { "UserId", _currentUserService.UserId } };

            if (!string.IsNullOrEmpty(status))
            {
                whereClauses.Add("Status = @Status");
                parameters.Add("Status", status);
            }

            if (!string.IsNullOrEmpty(priority))
            {
                whereClauses.Add("Priority = @Priority");
                parameters.Add("Priority", priority);
            }

            if (!string.IsNullOrEmpty(taskType))
            {
                whereClauses.Add("TaskType = @TaskType");
                parameters.Add("TaskType", taskType);
            }

            if (!string.IsNullOrEmpty(assignedToName))
            {
                whereClauses.Add("AssignedToName LIKE @AssignedToName");
                parameters.Add("AssignedToName", $"%{assignedToName}%");
            }

            if (!string.IsNullOrEmpty(projectName))
            {
                whereClauses.Add("ProjectName LIKE @ProjectName");
                parameters.Add("ProjectName", $"%{projectName}%");
            }

            if (isOverdue == true)
            {
                whereClauses.Add("DueDate IS NOT NULL AND DueDate < GETUTCDATE() AND Status != 'Completed'");
            }

            var whereClause = string.Join(" AND ", whereClauses);
            var sql = $@"
                SELECT * FROM TaskItems 
                WHERE {whereClause}
                ORDER BY 
                    CASE Priority 
                        WHEN 'Critical' THEN 1
                        WHEN 'High' THEN 2
                        WHEN 'Medium' THEN 3
                        WHEN 'Low' THEN 4
                        ELSE 5
                    END,
                    CASE WHEN DueDate IS NOT NULL THEN 0 ELSE 1 END,
                    DueDate ASC,
                    CreatedAt DESC";

            var tasks = await conn.QueryAsync<TaskItem>(sql, parameters);

            _logger.LogInformation("User {UserId} retrieved {Count} filtered tasks - Status: {Status}, Priority: {Priority}, Type: {TaskType}", 
                _currentUserService.UserId, tasks.Count(), status, priority, taskType);

            return tasks;
        }

        // Update task status and related fields (for AI Command Interpreter)
        public async Task<bool> UpdateTaskStatusAsync(int taskId, string newStatus, string? newPriority = null, string? assignedToName = null)
        {
            if (!_currentUserService.IsAuthenticated)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            using var conn = _context.CreateConnection();

            // Check if task exists and get owner
            var existingTask = await conn.QueryFirstOrDefaultAsync<TaskItem>(
                "SELECT * FROM TaskItems WHERE Id = @Id", new { Id = taskId });

            if (existingTask == null)
            {
                return false;
            }

            // Users can only update their own tasks unless they're admin
            if (!_currentUserService.IsInRole("Admin") && 
                existingTask.UserId != _currentUserService.UserId)
            {
                throw new UnauthorizedAccessException("You can only modify your own tasks");
            }

            // Determine values to update
            var isCompleted = newStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase);
            var progressPercentage = isCompleted ? 100 : existingTask.ProgressPercentage;
            var completedDate = isCompleted ? DateTime.UtcNow : (DateTime?)null;
            var priority = newPriority ?? existingTask.Priority;
            var assignee = assignedToName ?? existingTask.AssignedToName;

            var result = await conn.ExecuteAsync(@"
                UPDATE TaskItems 
                SET Status = @Status,
                    Priority = @Priority,
                    AssignedToName = @AssignedToName,
                    IsCompleted = @IsCompleted,
                    ProgressPercentage = @ProgressPercentage,
                    CompletedDate = @CompletedDate,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id",
                new { 
                    Status = newStatus,
                    Priority = priority,
                    AssignedToName = assignee,
                    IsCompleted = isCompleted,
                    ProgressPercentage = progressPercentage,
                    CompletedDate = completedDate,
                    UpdatedAt = DateTime.UtcNow,
                    Id = taskId 
                });

            _logger.LogInformation("Task {TaskId} status updated to {Status} by user {UserId}", 
                taskId, newStatus, _currentUserService.UserId);

            return result > 0;
        }
    }
}
