# Deploy Azure Infrastructure using Bicep
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("dev", "staging", "production")]
    [string]$Environment
)

$ErrorActionPreference = "Stop"

$RESOURCE_GROUP_NAME = "smarttask-ai-rg"
$LOCATION = "East US 2"

Write-Host "?? Deploying infrastructure for environment: $Environment" -ForegroundColor Green
Write-Host "?? Location: $LOCATION" -ForegroundColor Cyan
Write-Host "?? Resource Group: $RESOURCE_GROUP_NAME-$Environment" -ForegroundColor Cyan

# Prompt for sensitive parameters
Write-Host ""
$SQL_ADMIN_PASSWORD = Read-Host -Prompt "Enter SQL Server admin password (min 8 characters, must contain uppercase, lowercase, number, and special char)" -AsSecureString
$SQL_ADMIN_PASSWORD_TEXT = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SQL_ADMIN_PASSWORD))

$OPENAI_API_KEY = Read-Host -Prompt "Enter OpenAI API Key" -AsSecureString  
$OPENAI_API_KEY_TEXT = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($OPENAI_API_KEY))

$JWT_SECRET_KEY = Read-Host -Prompt "Enter JWT Secret Key (min 32 characters)" -AsSecureString
$JWT_SECRET_KEY_TEXT = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($JWT_SECRET_KEY))

# Validate inputs
if ($SQL_ADMIN_PASSWORD_TEXT.Length -lt 8) {
    Write-Error "? SQL Server password must be at least 8 characters long"
    exit 1
}

if ($JWT_SECRET_KEY_TEXT.Length -lt 32) {
    Write-Error "? JWT Secret Key must be at least 32 characters long"
    exit 1
}

if ([string]::IsNullOrEmpty($OPENAI_API_KEY_TEXT)) {
    Write-Error "? OpenAI API Key is required"
    exit 1
}

# Check if Azure CLI is logged in
try {
    az account show | Out-Null
} catch {
    Write-Error "? Not logged in to Azure CLI. Please run 'az login' first."
    exit 1
}

Write-Host "? Prerequisites validated" -ForegroundColor Green

# Create resource group if it doesn't exist
Write-Host "???  Creating resource group..." -ForegroundColor Yellow
az group create --name "$RESOURCE_GROUP_NAME-$Environment" --location "$LOCATION" --output table

Write-Host "?? Deploying Bicep template..." -ForegroundColor Yellow

# Deploy Bicep template
try {
    $deploymentOutput = az deployment group create `
        --resource-group "$RESOURCE_GROUP_NAME-$Environment" `
        --template-file "infrastructure/main.bicep" `
        --parameters environment="$Environment" `
        --parameters location="$LOCATION" `
        --parameters sqlAdminPassword="$SQL_ADMIN_PASSWORD_TEXT" `
        --parameters openAiApiKey="$OPENAI_API_KEY_TEXT" `
        --parameters jwtSecretKey="$JWT_SECRET_KEY_TEXT" `
        --output json | ConvertFrom-Json

    Write-Host "? Infrastructure deployment completed successfully!" -ForegroundColor Green
    
    # Extract outputs
    $webAppUrl = $deploymentOutput.properties.outputs.webAppUrl.value
    $webAppName = $deploymentOutput.properties.outputs.webAppName.value
    $sqlServerFqdn = $deploymentOutput.properties.outputs.sqlServerFqdn.value
    
    Write-Host ""
    Write-Host "?? Deployment Summary:" -ForegroundColor Cyan
    Write-Host "  Web App URL: $webAppUrl" -ForegroundColor White
    Write-Host "  Web App Name: $webAppName" -ForegroundColor White
    Write-Host "  SQL Server: $sqlServerFqdn" -ForegroundColor White
    Write-Host "  App Insights: Configured" -ForegroundColor White
    Write-Host ""
    Write-Host "?? Next Steps:" -ForegroundColor Yellow
    Write-Host "  1. Download publish profile from Azure Portal for $webAppName" -ForegroundColor White
    Write-Host "  2. Add the publish profile as GitHub secret: AZURE_WEBAPP_PUBLISH_PROFILE_$($Environment.ToUpper())" -ForegroundColor White
    Write-Host "  3. Push code to trigger deployment pipeline" -ForegroundColor White
    Write-Host "  4. Test the application at: $webAppUrl/health" -ForegroundColor White
    Write-Host ""
    
} catch {
    Write-Error "? Infrastructure deployment failed: $_"
    exit 1
}

# Clear sensitive variables
$SQL_ADMIN_PASSWORD_TEXT = $null
$OPENAI_API_KEY_TEXT = $null
$JWT_SECRET_KEY_TEXT = $null