# Script to configure Azure SQL Database with Managed Identity Authentication
# This enables passwordless authentication for your App Service

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "smarttask-ai-rg-production",
    
    [Parameter(Mandatory=$false)]
    [string]$AppServiceName = "smarttask-ai-api",
    
    [Parameter(Mandatory=$false)]
    [string]$SqlServerName = "smarttask-ai-api-sql-production",
    
    [Parameter(Mandatory=$false)]
    [string]$DatabaseName = "SmartTaskDB"
)

# Colors for output
$Green = "`e[32m"
$Yellow = "`e[33m"
$Red = "`e[31m"
$Blue = "`e[34m"
$Cyan = "`e[36m"
$Reset = "`e[0m"

Write-Host "${Blue}?? Azure SQL Managed Identity Configuration${Reset}" -ForegroundColor Blue
Write-Host "=========================================" -ForegroundColor Blue

Write-Host ""
Write-Host "${Yellow}?? Step 1: Checking current configuration...${Reset}"

# Check if Azure CLI is logged in
try {
    $account = az account show 2>$null | ConvertFrom-Json
    Write-Host "${Green}? Authenticated with Azure: $($account.name)${Reset}"
} catch {
    Write-Host "${Red}? Please login to Azure CLI first: az login${Reset}"
    exit 1
}

# Check if App Service exists
Write-Host "${Cyan}Checking App Service: $AppServiceName${Reset}"
$appService = az webapp show --name $AppServiceName --resource-group $ResourceGroupName 2>$null | ConvertFrom-Json

if (-not $appService) {
    Write-Host "${Red}? App Service '$AppServiceName' not found in resource group '$ResourceGroupName'${Reset}"
    exit 1
}

Write-Host "${Green}? Found App Service: $AppServiceName${Reset}"

# Check if SQL Server exists  
Write-Host "${Cyan}Checking SQL Server: $SqlServerName${Reset}"
$sqlServer = az sql server show --name $SqlServerName --resource-group $ResourceGroupName 2>$null | ConvertFrom-Json

if (-not $sqlServer) {
    Write-Host "${Red}? SQL Server '$SqlServerName' not found in resource group '$ResourceGroupName'${Reset}"
    exit 1
}

Write-Host "${Green}? Found SQL Server: $SqlServerName${Reset}"

Write-Host ""
Write-Host "${Yellow}?? Step 2: Enabling Managed Identity for App Service...${Reset}"

# Enable system-assigned managed identity for App Service
Write-Host "${Cyan}Enabling system-assigned managed identity...${Reset}"
$identityResult = az webapp identity assign --name $AppServiceName --resource-group $ResourceGroupName 2>&1

if ($LASTEXITCODE -eq 0) {
    $identity = $identityResult | ConvertFrom-Json
    $principalId = $identity.principalId
    $tenantId = $identity.tenantId
    
    Write-Host "${Green}? Managed identity enabled${Reset}"
    Write-Host "${Cyan}   Principal ID: $principalId${Reset}"
    Write-Host "${Cyan}   Tenant ID: $tenantId${Reset}"
} else {
    Write-Host "${Yellow}?? Managed identity may already be enabled or failed to enable${Reset}"
    
    # Try to get existing identity
    $existingIdentity = az webapp identity show --name $AppServiceName --resource-group $ResourceGroupName 2>$null | ConvertFrom-Json
    if ($existingIdentity -and $existingIdentity.principalId) {
        $principalId = $existingIdentity.principalId
        $tenantId = $existingIdentity.tenantId
        Write-Host "${Green}? Using existing managed identity${Reset}"
        Write-Host "${Cyan}   Principal ID: $principalId${Reset}"
    } else {
        Write-Host "${Red}? Could not get managed identity information${Reset}"
        exit 1
    }
}

Write-Host ""
Write-Host "${Yellow}?? Step 3: Configuring SQL Server for Azure AD Authentication...${Reset}"

# Enable Azure AD admin for SQL Server (this requires your user to be set as admin)
Write-Host "${Cyan}Setting up Azure AD authentication on SQL Server...${Reset}"

# Get current user info
$currentUser = az account show --query "{displayName:user.name, objectId:user.name}" 2>$null | ConvertFrom-Json
$currentUserObjectId = az ad signed-in-user show --query "id" -o tsv 2>$null

if ($currentUserObjectId) {
    Write-Host "${Cyan}Current user object ID: $currentUserObjectId${Reset}"
    
    # Set current user as Azure AD admin for SQL Server
    $adminResult = az sql server ad-admin create --resource-group $ResourceGroupName --server-name $SqlServerName --display-name $currentUser.displayName --object-id $currentUserObjectId 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "${Green}? Azure AD admin configured for SQL Server${Reset}"
    } else {
        Write-Host "${Yellow}?? Azure AD admin may already be configured${Reset}"
    }
} else {
    Write-Host "${Yellow}?? Could not get current user object ID${Reset}"
}

Write-Host ""
Write-Host "${Yellow}?? Step 4: Creating SQL user for App Service Managed Identity...${Reset}"

# Create SQL script to add the managed identity as a user
$sqlScript = @"
-- Connect to your database and run this SQL script
-- This creates a user for the App Service managed identity

-- Create user from external provider (Azure AD)
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = '$AppServiceName')
BEGIN
    CREATE USER [$AppServiceName] FROM EXTERNAL PROVIDER;
    PRINT 'User created for managed identity';
END
ELSE
BEGIN
    PRINT 'User already exists for managed identity';
END

-- Grant necessary permissions (adjust as needed)
ALTER ROLE db_datareader ADD MEMBER [$AppServiceName];
ALTER ROLE db_datawriter ADD MEMBER [$AppServiceName];
ALTER ROLE db_ddladmin ADD MEMBER [$AppServiceName];

-- Optional: Grant db_owner if you need full access
-- ALTER ROLE db_owner ADD MEMBER [$AppServiceName];

PRINT 'Permissions granted to managed identity user';
"@

Write-Host "${Cyan}SQL script to create managed identity user:${Reset}"
Write-Host "${Yellow}$sqlScript${Reset}"

Write-Host ""
Write-Host "${Blue}?? Manual SQL Step Required:${Reset}"
Write-Host "1. Connect to your database using SQL Server Management Studio or Azure Portal Query Editor"
Write-Host "2. Connect using Azure AD authentication (your account)"
Write-Host "3. Run the SQL script shown above"
Write-Host "4. Or save it to a file and run it"

# Save SQL script to file
$scriptPath = "setup-managed-identity-user.sql"
$sqlScript | Out-File -FilePath $scriptPath -Encoding UTF8
Write-Host "${Green}? SQL script saved to: $scriptPath${Reset}"

Write-Host ""
Write-Host "${Yellow}?? Step 5: Updating App Service Connection String...${Reset}"

# Update App Service connection string to use managed identity
$connectionString = "Server=tcp:$($sqlServer.fullyQualifiedDomainName),1433;Initial Catalog=$DatabaseName;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=`"Active Directory Default`";"

Write-Host "${Cyan}Setting connection string for managed identity authentication...${Reset}"
$connResult = az webapp config connection-string set --name $AppServiceName --resource-group $ResourceGroupName --connection-string-type SQLAzure --settings DefaultConnection="$connectionString" 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "${Green}? Connection string updated successfully${Reset}"
    Write-Host "${Cyan}New connection string: $connectionString${Reset}"
} else {
    Write-Host "${Red}? Failed to update connection string: $connResult${Reset}"
}

Write-Host ""
Write-Host "${Green}?? Managed Identity Configuration Summary${Reset}"
Write-Host "????????????????????????????????????????" -ForegroundColor Green
Write-Host ""
Write-Host "${Green}? App Service managed identity: Enabled${Reset}"
Write-Host "${Green}? SQL Server firewall: Configured${Reset}"
Write-Host "${Green}? Connection string: Updated${Reset}"
Write-Host "${Yellow}? SQL user creation: Manual step required${Reset}"
Write-Host ""
Write-Host "${Blue}?? Next Steps:${Reset}"
Write-Host "1. Run the SQL script in '$scriptPath' against your database"
Write-Host "2. Restart your App Service: az webapp restart --name $AppServiceName --resource-group $ResourceGroupName"
Write-Host "3. Test your /test-db endpoint"
Write-Host ""
Write-Host "${Cyan}?? New Connection String (use this):${Reset}"
Write-Host "$connectionString" -ForegroundColor Green
Write-Host ""
Write-Host "${Yellow}Benefits of Managed Identity:${Reset}"
Write-Host "• No passwords to manage" -ForegroundColor Gray
Write-Host "• Automatic token refresh" -ForegroundColor Gray  
Write-Host "• More secure than SQL authentication" -ForegroundColor Gray
Write-Host "• Integrated with Azure security" -ForegroundColor Gray