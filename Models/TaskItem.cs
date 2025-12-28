using System.ComponentModel.DataAnnotations;

namespace MiddleWareWebApi.Models
{
    public class TaskItem
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)] // Increased for AI-generated descriptions
        public string? Description { get; set; }

        public bool IsCompleted { get; set; } = false;

        // Enhanced properties for AI Command Interpreter
        [MaxLength(50)]
        public string Priority { get; set; } = "Medium"; // Low, Medium, High, Critical

        [MaxLength(50)]
        public string TaskType { get; set; } = "Task"; // Bug, Story, Feature, Defect, Enhancement, Task

        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, OnHold, Cancelled

        public int? AssignedToUserId { get; set; } // Who is assigned this task

        [MaxLength(100)]
        public string? AssignedToName { get; set; } // Cache assignee name for quick display

        // Project management fields
        [MaxLength(100)]
        public string? ProjectName { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        // Time management
        public DateTime? DueDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        // Progress tracking
        [Range(0, 100)]
        public int ProgressPercentage { get; set; } = 0;

        public int EstimatedHours { get; set; } = 0;
        public int ActualHours { get; set; } = 0;

        // Agile/Scrum support
        public int? StoryPoints { get; set; }

        [MaxLength(50)]
        public string? SprintName { get; set; }

        // Metadata
        [MaxLength(200)]
        public string? Tags { get; set; } // Comma-separated tags

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public int UserId { get; set; } // Creator/Owner

        // Navigation properties
        public User? User { get; set; }
    }
}
