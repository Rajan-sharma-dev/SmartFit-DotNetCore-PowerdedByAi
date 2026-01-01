# Complete script to gather all GitHub secrets for Azure deployment
# Run this script to get all the secrets you need for your GitHub Actions

Write-Host "SmartTask AI Assistant - GitHub Secrets Generator" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""

# Colors for better readability
$Green = "Green"
$Red = "Red" 
$Yellow = "Yellow"
$Cyan = "Cyan"
$White = "White"

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

# Check Azure CLI
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Azure CLI not found. Please install it first." -ForegroundColor $Red
    Write-Host "Visit: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor $White
    exit 1
}

# Check Azure CLI login
try {
    $account = az account show 2>$null | ConvertFrom-Json
    if (-not $account) {
        Write-Host "Not logged in to Azure CLI. Logging in..." -ForegroundColor $Red
        az login
        $account = az account show | ConvertFrom-Json
    }
    Write-Host "SUCCESS: Azure CLI authenticated for subscription: $($account.name)" -ForegroundColor $Green
} catch {
    Write-Host "ERROR: Failed to authenticate with Azure CLI" -ForegroundColor $Red
    exit 1
}

Write-Host ""
Write-Host "GITHUB SECRETS TO CONFIGURE:" -ForegroundColor $Cyan
Write-Host "================================" -ForegroundColor $Cyan

# 1. Get Publish Profile
Write-Host ""
Write-Host "1. AZURE_WEBAPP_PUBLISH_PROFILE_PRODUCTION" -ForegroundColor $Yellow
Write-Host "-------------------------------------------" -ForegroundColor $Yellow

try {
    $publishProfile = az webapp deployment list-publishing-profiles --name "smarttask-ai-api" --resource-group "smarttask-ai-rg-production" --xml
    if ($publishProfile) {
        Write-Host "SUCCESS: Publish profile retrieved successfully!" -ForegroundColor $Green
        Write-Host ""
        Write-Host "SECRET VALUE (copy this entire XML):" -ForegroundColor $Cyan
        Write-Host "===============================================================================" -ForegroundColor $Yellow
        Write-Output $publishProfile
        Write-Host "===============================================================================" -ForegroundColor $Yellow
        Write-Host ""
        
        # Save to file
        $publishProfile | Out-File -FilePath "publish-profile.xml" -Encoding UTF8
        Write-Host "SAVED: publish-profile.xml" -ForegroundColor $Green
    } else {
        Write-Host "ERROR: Failed to retrieve publish profile" -ForegroundColor $Red
    }
} catch {
    Write-Host "ERROR: Error getting publish profile: $($_.Exception.Message)" -ForegroundColor $Red
}

# 2. Database Connection String
Write-Host ""
Write-Host "2. DATABASE_CONNECTION_STRING" -ForegroundColor $Yellow
Write-Host "-----------------------------" -ForegroundColor $Yellow

try {
    # Try to get SQL servers in the resource group
    $sqlServers = az sql server list --resource-group "smarttask-ai-rg-production" | ConvertFrom-Json
    
    if ($sqlServers -and $sqlServers.Count -gt 0) {
        $serverName = $sqlServers[0].name
        $serverFqdn = $sqlServers[0].fullyQualifiedDomainName
        
        Write-Host "SUCCESS: Found SQL Server: $serverName" -ForegroundColor $Green
        Write-Host "Connection String Template:" -ForegroundColor $Cyan
        Write-Host "Server=tcp:$serverFqdn,1433;Initial Catalog=SmartTaskDB;User ID=sqladmin;Password=YOUR_SQL_PASSWORD;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;" -ForegroundColor $White
        Write-Host ""
        Write-Host "IMPORTANT: Replace 'YOUR_SQL_PASSWORD' with your actual SQL Server admin password" -ForegroundColor $Red
    } else {
        Write-Host "WARNING: No SQL Server found. You may need to create one or check the resource group." -ForegroundColor $Yellow
        Write-Host "Connection String Template (update server name):" -ForegroundColor $Cyan
        Write-Host "Server=tcp:your-sql-server.database.windows.net,1433;Initial Catalog=SmartTaskDB;User ID=sqladmin;Password=YOUR_SQL_PASSWORD;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;" -ForegroundColor $White
    }
} catch {
    Write-Host "ERROR: Error checking SQL Server: $($_.Exception.Message)" -ForegroundColor $Red
    Write-Host "Connection String Template (update server name and password):" -ForegroundColor $Cyan
    Write-Host "Server=tcp:your-sql-server.database.windows.net,1433;Initial Catalog=SmartTaskDB;User ID=sqladmin;Password=YOUR_SQL_PASSWORD;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;" -ForegroundColor $White
}

# 3. JWT Secret Key
Write-Host ""
Write-Host "3. JWT_SECRET_KEY" -ForegroundColor $Yellow
Write-Host "-----------------" -ForegroundColor $Yellow

# Generate a secure JWT secret
$jwtSecret = [System.Web.Security.Membership]::GeneratePassword(64, 16)
Write-Host "SUCCESS: Generated secure JWT secret key:" -ForegroundColor $Green
Write-Host "$jwtSecret" -ForegroundColor $White
Write-Host ""
Write-Host "This is a randomly generated 64-character key. Keep it secure!" -ForegroundColor $Cyan

# 4. OpenAI API Key
Write-Host ""
Write-Host "4. OPENAI_API_KEY" -ForegroundColor $Yellow
Write-Host "-----------------" -ForegroundColor $Yellow
Write-Host "You need to get this manually from OpenAI:" -ForegroundColor $Yellow
Write-Host "1. Go to: https://platform.openai.com/api-keys" -ForegroundColor $White
Write-Host "2. Sign in to your OpenAI account" -ForegroundColor $White
Write-Host "3. Click 'Create new secret key'" -ForegroundColor $White
Write-Host "4. Copy the key (starts with 'sk-proj-' or 'sk-')" -ForegroundColor $White
Write-Host "5. Set usage limits if needed" -ForegroundColor $White

# Summary
Write-Host ""
Write-Host "SUMMARY - Add these secrets to GitHub:" -ForegroundColor $Cyan
Write-Host "=========================================" -ForegroundColor $Cyan
Write-Host "Go to: https://github.com/Rajan-sharma-dev/SmartTask-AI-Assistant-Backend/settings/secrets/actions" -ForegroundColor $White
Write-Host ""
Write-Host "Required Secrets:" -ForegroundColor $Yellow
Write-Host "SUCCESS: AZURE_WEBAPP_PUBLISH_PROFILE_PRODUCTION (generated above)" -ForegroundColor $Green
Write-Host "TODO: DATABASE_CONNECTION_STRING (update with your SQL password)" -ForegroundColor $Yellow
Write-Host "SUCCESS: JWT_SECRET_KEY (generated above)" -ForegroundColor $Green
Write-Host "TODO: OPENAI_API_KEY (get from OpenAI platform)" -ForegroundColor $Yellow
Write-Host ""

# Test deployment URL
Write-Host "Your App Service URL:" -ForegroundColor $Cyan
Write-Host "https://smarttask-ai-api-h7evagcdc3e2hrek.centralindia-01.azurewebsites.net" -ForegroundColor $White
Write-Host ""

Write-Host "Script completed! Configure the secrets in GitHub and push to main branch to deploy." -ForegroundColor $Green