# Comprehensive fix script for Azure App Service and SQL Database
# Fixes both 500.30 error and SQL connectivity issues

Write-Host "?? Comprehensive Azure Fix Script" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# Check Azure CLI
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "? Azure CLI not found. Please install Azure CLI first." -ForegroundColor Red
    exit 1
}

# Check login
try {
    $account = az account show 2>$null | ConvertFrom-Json
    if (-not $account) {
        Write-Host "Logging into Azure CLI..." -ForegroundColor Yellow
        az login
    }
    Write-Host "? Authenticated with Azure: $($account.name)" -ForegroundColor Green
} catch {
    Write-Host "? Failed to authenticate with Azure" -ForegroundColor Red
    exit 1
}

# Variables
$resourceGroup = "smarttask-ai-rg-production"
$appName = "smarttask-ai-api"
$sqlServerName = "smarttask-ai-api-sql-production"

Write-Host ""
Write-Host "?? Step 1: Fixing SQL Database Network Access..." -ForegroundColor Yellow

# Enable public network access for SQL Server
Write-Host "?? Enabling public network access..." -ForegroundColor Cyan
$enableResult = az sql server update --name $sqlServerName --resource-group $resourceGroup --enable-public-network true 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Public network access enabled" -ForegroundColor Green
} else {
    Write-Host "?? Public access command result: $enableResult" -ForegroundColor Yellow
}

# Add firewall rules
Write-Host "?? Configuring firewall rules..." -ForegroundColor Cyan

# Azure services rule (allows App Service to connect)
$azureRule = az sql server firewall-rule create --name "AllowAzureServices" --server $sqlServerName --resource-group $resourceGroup --start-ip-address "0.0.0.0" --end-ip-address "0.0.0.0" 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Azure services firewall rule added" -ForegroundColor Green
} else {
    Write-Host "?? Azure services rule may already exist" -ForegroundColor Yellow
}

# Get current public IP for development access
try {
    $publicIP = (Invoke-RestMethod -Uri "https://api.ipify.org?format=text" -TimeoutSec 10).Trim()
    Write-Host "?? Your public IP: $publicIP" -ForegroundColor Cyan
    
    $devRule = az sql server firewall-rule create --name "DeveloperAccess-$(Get-Date -Format 'yyyyMMdd')" --server $sqlServerName --resource-group $resourceGroup --start-ip-address $publicIP --end-ip-address $publicIP 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? Developer access firewall rule added" -ForegroundColor Green
    } else {
        Write-Host "?? Developer access rule may already exist" -ForegroundColor Yellow
    }
} catch {
    Write-Host "?? Could not determine your public IP" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "?? Step 2: Configuring App Service Settings..." -ForegroundColor Yellow

# Get the secrets from the user
Write-Host "Enter your production secrets:" -ForegroundColor Cyan
Write-Host "(These should match your GitHub Secrets)" -ForegroundColor Gray

$dbConnectionString = Read-Host -Prompt "DATABASE_CONNECTION_STRING" -AsSecureString
$jwtSecretKey = Read-Host -Prompt "JWT_SECRET_KEY" -AsSecureString  
$openAiApiKey = Read-Host -Prompt "OPENAI_API_KEY" -AsSecureString

# Convert secure strings to plain text for Azure CLI
$dbConnectionStringText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($dbConnectionString))
$jwtSecretKeyText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($jwtSecretKey))
$openAiApiKeyText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($openAiApiKey))

if ([string]::IsNullOrEmpty($dbConnectionStringText) -or [string]::IsNullOrEmpty($jwtSecretKeyText) -or [string]::IsNullOrEmpty($openAiApiKeyText)) {
    Write-Host "? All three secrets are required" -ForegroundColor Red
    exit 1
}

Write-Host "?? Applying App Service configuration..." -ForegroundColor Cyan

try {
    # Configure App Service settings
    $settingsResult = az webapp config appsettings set `
        --name $appName `
        --resource-group $resourceGroup `
        --settings `
        "ASPNETCORE_ENVIRONMENT=Production" `
        "ConnectionStrings__DefaultConnection=$dbConnectionStringText" `
        "JwtSettings__SecretKey=$jwtSecretKeyText" `
        "JwtSettings__Issuer=SmartTask-AI-Production" `
        "JwtSettings__Audience=SmartTask-AI-Users" `
        "JwtSettings__AccessTokenExpirationMinutes=60" `
        "JwtSettings__RefreshTokenExpirationDays=7" `
        "OpenAi__ApiKey=$openAiApiKeyText" `
        "OpenAi__BaseUrl=https://api.openai.com/v1" `
        "OpenAi__DefaultModel=gpt-4o-mini" `
        "OpenAi__MaxTokens=1500" `
        "OpenAi__Temperature=0.7" `
        "OpenAi__TimeoutSeconds=30" `
        "Logging__LogLevel__Default=Information" 2>&1
        
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? App Service settings configured successfully" -ForegroundColor Green
    } else {
        Write-Host "? Failed to configure settings: $settingsResult" -ForegroundColor Red
        throw "Configuration failed"
    }
    
    # Clear sensitive variables
    $dbConnectionStringText = $null
    $jwtSecretKeyText = $null
    $openAiApiKeyText = $null
    
    Write-Host ""
    Write-Host "?? Restarting App Service..." -ForegroundColor Yellow
    az webapp restart --name $appName --resource-group $resourceGroup
    
    Write-Host "? App Service restarted" -ForegroundColor Green
    Write-Host "? Waiting for app to start (60 seconds)..." -ForegroundColor Yellow
    
    # Wait for app to start
    Start-Sleep -Seconds 60
    
    Write-Host ""
    Write-Host "?? Step 3: Testing Application..." -ForegroundColor Yellow
    
    # Test endpoints
    $baseUrl = "https://smarttask-ai-api-h7evagcdc3e2hrek.centralindia-01.azurewebsites.net"
    
    # Test health endpoint
    try {
        Write-Host "?? Testing health endpoint..." -ForegroundColor Cyan
        $healthResponse = Invoke-WebRequest -Uri "$baseUrl/health" -Method GET -TimeoutSec 30 -UseBasicParsing
        Write-Host "? Health endpoint: OK ($($healthResponse.StatusCode))" -ForegroundColor Green
    } catch {
        Write-Host "?? Health endpoint failed: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    # Test database endpoint
    try {
        Write-Host "??? Testing database connectivity..." -ForegroundColor Cyan
        $dbResponse = Invoke-WebRequest -Uri "$baseUrl/test-db" -Method GET -TimeoutSec 30 -UseBasicParsing
        
        if ($dbResponse.StatusCode -eq 200) {
            Write-Host "? Database connectivity: SUCCESS!" -ForegroundColor Green
            
            # Parse and display database response
            $dbContent = $dbResponse.Content | ConvertFrom-Json
            Write-Host "   Server: $($dbContent.server)" -ForegroundColor Cyan
            Write-Host "   Database: $($dbContent.database)" -ForegroundColor Cyan
            Write-Host "   Response Time: $($dbContent.responseTime)" -ForegroundColor Cyan
        } else {
            Write-Host "?? Database test returned status: $($dbResponse.StatusCode)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "? Database connectivity failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "   This may indicate the SQL firewall changes need more time to propagate." -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "?? Configuration Complete!" -ForegroundColor Green
    Write-Host "????????????????????????????????????" -ForegroundColor Green
    Write-Host "?? App URL: $baseUrl" -ForegroundColor Cyan
    Write-Host "?? Health Check: $baseUrl/health" -ForegroundColor Cyan
    Write-Host "??? Database Test: $baseUrl/test-db" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "If database connectivity still fails, wait 2-3 minutes for" -ForegroundColor Yellow
    Write-Host "Azure SQL firewall changes to fully propagate." -ForegroundColor Yellow
    
} catch {
    Write-Host "? Error during configuration: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "? Script completed successfully!" -ForegroundColor Green