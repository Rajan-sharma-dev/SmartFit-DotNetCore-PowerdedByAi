# Script to add a new user to Azure SQL Database
# This allows you to keep using 'RajanSharma' as the username

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$SqlServerName = "smarttask-ai-api-sql-production",
    
    [Parameter(Mandatory=$false)]
    [string]$DatabaseName = "SmartTaskDB",
    
    [Parameter(Mandatory=$true)]
    [string]$AdminUsername = "sqladmin",
    
    [Parameter(Mandatory=$true)]
    [SecureString]$AdminPassword,
    
    [Parameter(Mandatory=$true)]
    [string]$NewUsername = "RajanSharma",
    
    [Parameter(Mandatory=$true)]
    [SecureString]$NewUserPassword
)

# Colors for output
$Green = "`e[32m"
$Yellow = "`e[33m"
$Red = "`e[31m"
$Blue = "`e[34m"
$Reset = "`e[0m"

Write-Host "${Blue}?? Azure SQL Database User Creation Script${Reset}" -ForegroundColor Blue
Write-Host "==========================================" -ForegroundColor Blue

# Convert secure strings to plain text for SQL commands
$adminPasswordText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($AdminPassword))
$newUserPasswordText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($NewUserPassword))

$serverFqdn = "$SqlServerName.database.windows.net"

Write-Host "${Yellow}?? Connecting to SQL Server: $serverFqdn${Reset}"
Write-Host "${Yellow}?? Database: $DatabaseName${Reset}"
Write-Host "${Yellow}?? Creating user: $NewUsername${Reset}"

# Create SQL commands
$createLoginSql = @"
-- Create login at server level
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = '$NewUsername')
BEGIN
    CREATE LOGIN [$NewUsername] WITH PASSWORD = '$newUserPasswordText';
    PRINT 'Login created successfully';
END
ELSE
BEGIN
    PRINT 'Login already exists';
END
"@

$createUserSql = @"
-- Create user in database and assign roles
USE [$DatabaseName];

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = '$NewUsername')
BEGIN
    CREATE USER [$NewUsername] FOR LOGIN [$NewUsername];
    PRINT 'User created successfully';
END
ELSE
BEGIN
    PRINT 'User already exists';
END

-- Add user to db_owner role (full access)
IF NOT EXISTS (SELECT * FROM sys.database_role_members rm 
               JOIN sys.database_principals rp ON rm.role_principal_id = rp.principal_id 
               JOIN sys.database_principals mp ON rm.member_principal_id = mp.principal_id 
               WHERE rp.name = 'db_owner' AND mp.name = '$NewUsername')
BEGIN
    ALTER ROLE db_owner ADD MEMBER [$NewUsername];
    PRINT 'User added to db_owner role';
END
ELSE
BEGIN
    PRINT 'User already in db_owner role';
END
"@

try {
    # Check if sqlcmd is available
    $sqlcmdPath = Get-Command sqlcmd -ErrorAction SilentlyContinue
    if (-not $sqlcmdPath) {
        Write-Host "${Red}? sqlcmd not found. Installing SQL Server Command Line Tools...${Reset}"
        Write-Host "${Yellow}Please install SQL Server Command Line Tools and try again.${Reset}"
        Write-Host "${Yellow}Download from: https://docs.microsoft.com/en-us/sql/tools/sqlcmd-utility${Reset}"
        exit 1
    }

    Write-Host "${Yellow}?? Creating server login...${Reset}"
    
    # Create login at server level
    $loginResult = $createLoginSql | sqlcmd -S $serverFqdn -d "master" -U $AdminUsername -P $adminPasswordText -b -V 1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "${Green}? Server login created/verified${Reset}"
    } else {
        Write-Host "${Red}? Failed to create server login:${Reset}"
        Write-Host $loginResult
        throw "Login creation failed"
    }

    Write-Host "${Yellow}?? Creating database user and assigning permissions...${Reset}"
    
    # Create user in database
    $userResult = $createUserSql | sqlcmd -S $serverFqdn -d $DatabaseName -U $AdminUsername -P $adminPasswordText -b -V 1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "${Green}? Database user created and permissions assigned${Reset}"
    } else {
        Write-Host "${Red}? Failed to create database user:${Reset}"
        Write-Host $userResult
        throw "User creation failed"
    }

    Write-Host ""
    Write-Host "${Green}?? User creation completed successfully!${Reset}"
    Write-Host "${Yellow}?? You can now use this connection string:${Reset}"
    Write-Host "Server=tcp:$serverFqdn,1433;Initial Catalog=$DatabaseName;User Id=$NewUsername;Password=YourPassword;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    
    Write-Host ""
    Write-Host "${Yellow}?? Testing connection with new user...${Reset}"
    
    # Test connection with new user
    $testSql = "SELECT 1 as TestResult, SYSTEM_USER as CurrentUser, DB_NAME() as DatabaseName"
    $testResult = $testSql | sqlcmd -S $serverFqdn -d $DatabaseName -U $NewUsername -P $newUserPasswordText -h -1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "${Green}? Connection test successful with new user!${Reset}"
        Write-Host "${Blue}Test Result:${Reset}"
        Write-Host $testResult
    } else {
        Write-Host "${Yellow}?? Connection test failed, but user was created. Wait a few minutes and try again.${Reset}"
    }

} catch {
    Write-Host "${Red}? Error: $($_.Exception.Message)${Reset}"
    exit 1
} finally {
    # Clear sensitive variables
    $adminPasswordText = $null
    $newUserPasswordText = $null
}

Write-Host ""
Write-Host "${Green}? Script completed!${Reset}"