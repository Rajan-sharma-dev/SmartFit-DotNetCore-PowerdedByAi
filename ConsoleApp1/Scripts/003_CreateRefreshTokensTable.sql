CREATE TABLE RefreshTokens (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Token NVARCHAR(500) NOT NULL,
    UserId INT NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    CreatedByIp NVARCHAR(50) NULL,
    IsRevoked BIT NOT NULL DEFAULT 0,
    RevokedAt DATETIME2 NULL,
    RevokedBy NVARCHAR(50) NULL,
    
    -- Foreign key constraint
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE,
    
    -- Unique constraint on token
    CONSTRAINT UQ_RefreshTokens_Token UNIQUE (Token)
);

-- Create indexes for better performance
CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens(UserId);
CREATE INDEX IX_RefreshTokens_Token ON RefreshTokens(Token);
CREATE INDEX IX_RefreshTokens_ExpiresAt ON RefreshTokens(ExpiresAt);
CREATE INDEX IX_RefreshTokens_IsRevoked ON RefreshTokens(IsRevoked);