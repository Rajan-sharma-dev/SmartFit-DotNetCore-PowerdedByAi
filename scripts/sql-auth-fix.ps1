# Quick SQL Authentication Fix
# This script helps you determine the correct credentials and fixes the authentication issue

Write-Host "?? SQL Authentication Quick Fix" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan

Write-Host ""
Write-Host "? Current Issue:" -ForegroundColor Red
Write-Host "   Login failed for user 'RajanSharma'" -ForegroundColor Red
Write-Host ""
Write-Host "?? Root Cause Analysis:" -ForegroundColor Yellow
Write-Host "   Your Azure SQL Server was created with username 'sqladmin'" -ForegroundColor Yellow
Write-Host "   But your connection string is using 'RajanSharma'" -ForegroundColor Yellow
Write-Host ""

# Get the resource group info
$resourceGroup = "smarttask-ai-rg-production"
$sqlServerName = "smarttask-ai-api-sql-production"
$serverFqdn = "$sqlServerName.database.windows.net"

Write-Host "?? Solution Options:" -ForegroundColor Cyan
Write-Host ""

Write-Host "?? Option 1: Use Correct Admin Credentials (EASIEST)" -ForegroundColor Green
Write-Host "   Update your connection string to use the admin account that was created:" -ForegroundColor Gray
Write-Host ""
Write-Host "   Current (WRONG):" -ForegroundColor Red
Write-Host "   User ID=RajanSharma;Password={your_password}" -ForegroundColor Red
Write-Host ""
Write-Host "   Correct (USE THIS):" -ForegroundColor Green
Write-Host "   User ID=sqladmin;Password={admin_password}" -ForegroundColor Green
Write-Host ""

Write-Host "?? Option 2: Create New User 'RajanSharma'" -ForegroundColor Yellow
Write-Host "   This requires connecting with admin credentials first" -ForegroundColor Gray
Write-Host ""

Write-Host "?? Recommended Action:" -ForegroundColor Cyan
Write-Host ""

# Check if they want to see admin credentials
Write-Host "Do you want to:" -ForegroundColor White
Write-Host "1. Get the current admin password from Azure Key Vault/GitHub Secrets" -ForegroundColor Cyan
Write-Host "2. Reset the admin password" -ForegroundColor Cyan  
Write-Host "3. Create a new user 'RajanSharma'" -ForegroundColor Cyan
Write-Host ""

$choice = Read-Host "Enter your choice (1, 2, or 3)"

switch ($choice) {
    "1" {
        Write-Host ""
        Write-Host "?? Getting Admin Password:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "The admin password should be in one of these locations:" -ForegroundColor Gray
        Write-Host "   • GitHub Repository Secrets (SQL_ADMIN_PASSWORD)" -ForegroundColor Cyan
        Write-Host "   • Azure Key Vault" -ForegroundColor Cyan
        Write-Host "   • Your deployment notes/documentation" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Once you have the password, update your connection string:" -ForegroundColor Yellow
        Write-Host "Server=tcp:$serverFqdn,1433;Initial Catalog=smarttask-ai-rg-productionDb;User Id=sqladmin;Password=ADMIN_PASSWORD_HERE;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" -ForegroundColor Green
    }
    
    "2" {
        Write-Host ""
        Write-Host "?? Resetting Admin Password:" -ForegroundColor Yellow
        Write-Host ""
        $newPassword = Read-Host "Enter new admin password" -AsSecureString
        $newPasswordText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($newPassword))
        
        try {
            Write-Host "Updating SQL Server admin password..." -ForegroundColor Cyan
            az sql server update --name $sqlServerName --resource-group $resourceGroup --admin-password $newPasswordText
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "? Admin password updated successfully!" -ForegroundColor Green
                Write-Host ""
                Write-Host "Updated connection string:" -ForegroundColor Yellow
                Write-Host "Server=tcp:$serverFqdn,1433;Initial Catalog=smarttask-ai-rg-productionDb;User Id=sqladmin;Password=$newPasswordText;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" -ForegroundColor Green
            } else {
                Write-Host "? Failed to update password" -ForegroundColor Red
            }
        } catch {
            Write-Host "? Error: $($_.Exception.Message)" -ForegroundColor Red
        } finally {
            $newPasswordText = $null
        }
    }
    
    "3" {
        Write-Host ""
        Write-Host "?? Creating New User 'RajanSharma':" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "To create a new user, I need the current admin credentials:" -ForegroundColor Gray
        
        $adminPassword = Read-Host "Enter admin password for 'sqladmin'" -AsSecureString
        $newUserPassword = Read-Host "Enter new password for 'RajanSharma'" -AsSecureString
        
        Write-Host ""
        Write-Host "Run this script to create the user:" -ForegroundColor Cyan
        Write-Host ".\scripts\create-sql-user.ps1 -ResourceGroupName '$resourceGroup' -AdminUsername 'sqladmin' -AdminPassword (password) -NewUsername 'RajanSharma' -NewUserPassword (password)" -ForegroundColor Yellow
    }
    
    default {
        Write-Host ""
        Write-Host "Invalid choice. Please run the script again." -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "?? After fixing the credentials, test your connection:" -ForegroundColor Cyan
Write-Host "   Visit: https://your-app.azurewebsites.net/test-db" -ForegroundColor Yellow
Write-Host ""
Write-Host "? The firewall issue is already fixed - you just need the right credentials!" -ForegroundColor Green