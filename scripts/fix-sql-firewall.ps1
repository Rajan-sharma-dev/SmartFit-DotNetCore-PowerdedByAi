# Quick fix for Azure SQL Database public network access issue
# This script enables public network access and configures firewall rules

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$SqlServerName = "smarttask-ai-api-sql-production",
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId = $null
)

# Colors for output
$Red = "`e[31m"
$Green = "`e[32m"
$Yellow = "`e[33m"
$Blue = "`e[34m"
$Reset = "`e[0m"

Write-Host "${Blue}?? Azure SQL Database Firewall Fix Script${Reset}" -ForegroundColor Blue
Write-Host "================================" -ForegroundColor Blue

# Set subscription if provided
if ($SubscriptionId) {
    Write-Host "${Yellow}?? Setting Azure subscription...${Reset}"
    az account set --subscription $SubscriptionId
    if ($LASTEXITCODE -ne 0) {
        Write-Host "${Red}? Failed to set subscription. Please check your subscription ID.${Reset}"
        exit 1
    }
}

# Get current subscription info
$currentSub = az account show --query "{name:name, id:id}" -o json | ConvertFrom-Json
Write-Host "${Green}? Using subscription: $($currentSub.name) ($($currentSub.id))${Reset}"

# Check if SQL Server exists
Write-Host "${Yellow}?? Checking SQL Server existence...${Reset}"
$sqlServerExists = az sql server show --name $SqlServerName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null

if (-not $sqlServerExists) {
    Write-Host "${Red}? SQL Server '$SqlServerName' not found in resource group '$ResourceGroupName'${Reset}"
    Write-Host "${Yellow}Available SQL servers in the resource group:${Reset}"
    az sql server list --resource-group $ResourceGroupName --query "[].{Name:name, Location:location}" -o table
    exit 1
}

Write-Host "${Green}? Found SQL Server: $SqlServerName${Reset}"

# Enable public network access
Write-Host "${Yellow}?? Enabling public network access...${Reset}"
$enableResult = az sql server update --name $SqlServerName --resource-group $ResourceGroupName --enable-public-network true 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "${Green}? Public network access enabled successfully${Reset}"
} else {
    Write-Host "${Red}? Failed to enable public network access:${Reset}"
    Write-Host $enableResult
    exit 1
}

# Add firewall rule for Azure services
Write-Host "${Yellow}?? Adding firewall rule for Azure services...${Reset}"
$azureRule = az sql server firewall-rule create --name "AllowAzureServices" --server $SqlServerName --resource-group $ResourceGroupName --start-ip-address "0.0.0.0" --end-ip-address "0.0.0.0" 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "${Green}? Azure services firewall rule added${Reset}"
} else {
    Write-Host "${Yellow}??  Azure services rule may already exist or failed to add:${Reset}"
    Write-Host $azureRule
}

# Get current public IP for development access
Write-Host "${Yellow}?? Getting your current public IP...${Reset}"
try {
    $publicIP = (Invoke-RestMethod -Uri "https://api.ipify.org?format=text" -TimeoutSec 10).Trim()
    Write-Host "${Green}? Your public IP: $publicIP${Reset}"
    
    # Add firewall rule for current IP
    Write-Host "${Yellow}?? Adding firewall rule for your IP...${Reset}"
    $devRule = az sql server firewall-rule create --name "DeveloperAccess-$(Get-Date -Format 'yyyyMMdd')" --server $SqlServerName --resource-group $ResourceGroupName --start-ip-address $publicIP --end-ip-address $publicIP 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "${Green}? Developer access firewall rule added${Reset}"
    } else {
        Write-Host "${Yellow}??  Developer access rule may already exist or failed to add:${Reset}"
        Write-Host $devRule
    }
} catch {
    Write-Host "${Yellow}??  Could not determine your public IP. You may need to add it manually.${Reset}"
}

# Show current firewall rules
Write-Host "${Yellow}?? Current firewall rules:${Reset}"
az sql server firewall-rule list --server $SqlServerName --resource-group $ResourceGroupName --query "[].{Name:name, StartIP:startIpAddress, EndIP:endIpAddress}" -o table

# Test connection
Write-Host "${Yellow}?? Testing connection to SQL Server...${Reset}"
$serverFqdn = "$SqlServerName.database.windows.net"
$testResult = Test-NetConnection -ComputerName $serverFqdn -Port 1433 -WarningAction SilentlyContinue

if ($testResult.TcpTestSucceeded) {
    Write-Host "${Green}? TCP connection to SQL Server successful (Port 1433 open)${Reset}"
} else {
    Write-Host "${Red}? TCP connection to SQL Server failed${Reset}"
    Write-Host "${Yellow}   This might take a few minutes to propagate. Try testing your application again in 2-3 minutes.${Reset}"
}

Write-Host ""
Write-Host "${Green}?? Firewall configuration completed!${Reset}"
Write-Host "${Yellow}?? Next steps:${Reset}"
Write-Host "   1. Wait 2-3 minutes for changes to propagate"
Write-Host "   2. Test your application's /test-db endpoint"
Write-Host "   3. Check your connection string format"
Write-Host ""
Write-Host "${Blue}Connection String Format:${Reset}"
Write-Host "Server=tcp:$serverFqdn,1433;Initial Catalog=YourDatabaseName;User Id=YourUsername;Password=YourPassword;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"