CREATE TABLE TaskItems (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(200) NOT NULL,
    Description NVARCHAR(2000) NULL,
    IsCompleted BIT NOT NULL DEFAULT 0,
    Priority NVARCHAR(50) NOT NULL DEFAULT 'Medium',
    TaskType NVARCHAR(50),
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    UserId INT NOT NULL,
    AssignedToUserId INT NULL,
    AssignedToName NVARCHAR(100) NULL,
    ProjectName NVARCHAR(100) NULL,
    Category NVARCHAR(50) NULL,
    SprintName NVARCHAR(50) NULL,
    DueDate DATETIME2 NULL,
    StartDate DATETIME2 NULL,
    CompletedDate DATETIME2 NULL,
    ProgressPercentage INT NOT NULL DEFAULT 0,
    EstimatedHours INT NOT NULL DEFAULT 0,
    ActualHours INT NOT NULL DEFAULT 0,
    StoryPoints INT NULL,
    Tags NVARCHAR(200) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()

    CONSTRAINT FK_TaskItems_User FOREIGN KEY (UserId) REFERENCES Users(UserId)
)

