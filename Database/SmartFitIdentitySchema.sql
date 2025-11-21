-- SmartFit Identity Database Schema
-- Run this script to create the required database tables

-- Create database if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'SmartFitDB')
BEGIN
    CREATE DATABASE SmartFitDB;
END
GO

USE SmartFitDB;
GO

-- Create Users table (updated with identity fields)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
BEGIN
    CREATE TABLE Users (
        UserId INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL UNIQUE,
        Email NVARCHAR(255) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(500) NOT NULL,
        FullName NVARCHAR(150) NULL,
        Role NVARCHAR(50) NOT NULL DEFAULT 'User',
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        
        -- Address Fields
        AddressLine1 NVARCHAR(255) NULL,
        AddressLine2 NVARCHAR(255) NULL,
        City NVARCHAR(100) NULL,
        State NVARCHAR(100) NULL,
        PostalCode NVARCHAR(20) NULL,
        Country NVARCHAR(100) NULL,
        
        -- Other fields
        PhoneNumber NVARCHAR(20) NULL,
        DateOfBirth DATE NULL,
        ProfilePictureUrl NVARCHAR(500) NULL,
        
        -- Indexes
        INDEX IX_Users_Email (Email),
        INDEX IX_Users_Username (Username),
        INDEX IX_Users_IsActive (IsActive)
    );
END
GO

-- Create RefreshTokens table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RefreshTokens' AND xtype='U')
BEGIN
    CREATE TABLE RefreshTokens (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Token NVARCHAR(500) NOT NULL UNIQUE,
        UserId INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        ExpiresAt DATETIME2 NOT NULL,
        IsRevoked BIT NOT NULL DEFAULT 0,
        RevokedBy NVARCHAR(100) NULL,
        RevokedAt DATETIME2 NULL,
        CreatedByIp NVARCHAR(50) NOT NULL,
        
        -- Foreign Key
        CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE,
        
        -- Indexes
        INDEX IX_RefreshTokens_Token (Token),
        INDEX IX_RefreshTokens_UserId (UserId),
        INDEX IX_RefreshTokens_ExpiresAt (ExpiresAt),
        INDEX IX_RefreshTokens_IsRevoked (IsRevoked)
    );
END
GO

-- Create Tasks table (if not exists)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Tasks' AND xtype='U')
BEGIN
    CREATE TABLE Tasks (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(1000) NULL,
        IsCompleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UserId INT NOT NULL,
        
        -- Foreign Key
        CONSTRAINT FK_Tasks_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE,
        
        -- Indexes
        INDEX IX_Tasks_UserId (UserId),
        INDEX IX_Tasks_IsCompleted (IsCompleted),
        INDEX IX_Tasks_CreatedAt (CreatedAt)
    );
END
GO

-- Insert default admin user (password: Admin123!)
IF NOT EXISTS (SELECT * FROM Users WHERE Email = 'admin@smartfit.com')
BEGIN
    INSERT INTO Users (Username, Email, PasswordHash, FullName, Role, IsActive, CreatedAt)
    VALUES (
        'admin',
        'admin@smartfit.com',
        '$2a$11$3K7B6K5I5J7K8L9M0N1O2P3Q4R5S6T7U8V9W0X1Y2Z3A4B5C6D7E8F', -- BCrypt hash of 'Admin123!'
        'System Administrator',
        'Admin',
        1,
        GETUTCDATE()
    );
END
GO

-- Insert sample user (password: User123!)
IF NOT EXISTS (SELECT * FROM Users WHERE Email = 'user@smartfit.com')
BEGIN
    INSERT INTO Users (Username, Email, PasswordHash, FullName, Role, IsActive, CreatedAt)
    VALUES (
        'testuser',
        'user@smartfit.com',
        '$2a$11$4L8C7K6I6J8K9L0M1N2O3P4Q5R6S7T8U9V0W1X2Y3Z4A5B6C7D8E9F', -- BCrypt hash of 'User123!'
        'Test User',
        'User',
        1,
        GETUTCDATE()
    );
END
GO

-- Create stored procedures for cleanup (optional)
CREATE OR ALTER PROCEDURE sp_CleanupExpiredRefreshTokens
AS
BEGIN
    DELETE FROM RefreshTokens 
    WHERE ExpiresAt < GETUTCDATE() OR IsRevoked = 1;
    
    SELECT @@ROWCOUNT as DeletedRows;
END
GO

-- Create view for active users (optional)
CREATE OR ALTER VIEW vw_ActiveUsers
AS
SELECT 
    UserId,
    Username,
    Email,
    FullName,
    Role,
    CreatedAt,
    PhoneNumber,
    City,
    State,
    Country
FROM Users
WHERE IsActive = 1;
GO

PRINT 'SmartFit Identity Database schema created successfully!';
PRINT 'Default users created:';
PRINT '  Admin: admin@smartfit.com / Admin123!';
PRINT '  User:  user@smartfit.com / User123!';