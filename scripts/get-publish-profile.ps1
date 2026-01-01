# PowerShell script to get publish profile for your App Service
# Run this script to get the publish profile content

Write-Host "?? Getting publish profile for smarttask-ai-api..." -ForegroundColor Yellow
Write-Host "App Service URL: https://smarttask-ai-api-h7evagcdc3e2hrek.centralindia-01.azurewebsites.net" -ForegroundColor Cyan
Write-Host ""

try {
    # Check if logged in to Azure CLI
    Write-Host "?? Checking Azure CLI login status..." -ForegroundColor Yellow
    $account = az account show 2>$null
    if (-not $account) {
        Write-Host "? Not logged in to Azure CLI. Please run: az login" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "? Azure CLI authenticated" -ForegroundColor Green
    Write-Host ""

    # Get the publish profile XML content
    Write-Host "?? Retrieving publish profile..." -ForegroundColor Yellow
    $publishProfile = az webapp deployment list-publishing-profiles `
        --name "smarttask-ai-api" `
        --resource-group "smarttask-ai-rg-production" `
        --xml

    if ($publishProfile) {
        Write-Host "? Publish profile retrieved successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "?? COPY THE CONTENT BELOW FOR GITHUB SECRET:" -ForegroundColor Cyan
        Write-Host "Secret Name: AZURE_WEBAPP_PUBLISH_PROFILE_PRODUCTION" -ForegroundColor Yellow
        Write-Host "="*80 -ForegroundColor Yellow
        Write-Output $publishProfile
        Write-Host "="*80 -ForegroundColor Yellow
        Write-Host ""
        
        # Save to file
        $publishProfile | Out-File -FilePath "smarttask-ai-api-publish-profile.xml" -Encoding UTF8
        Write-Host "?? Also saved to: smarttask-ai-api-publish-profile.xml" -ForegroundColor Green
        Write-Host ""
        
        # GitHub instructions
        Write-Host "?? NEXT STEPS:" -ForegroundColor Cyan
        Write-Host "1. Copy the XML content above (between the == lines)" -ForegroundColor White
        Write-Host "2. Go to: https://github.com/Rajan-sharma-dev/SmartTask-AI-Assistant-Backend/settings/secrets/actions" -ForegroundColor White
        Write-Host "3. Click 'New repository secret'" -ForegroundColor White
        Write-Host "4. Name: AZURE_WEBAPP_PUBLISH_PROFILE_PRODUCTION" -ForegroundColor White
        Write-Host "5. Value: Paste the XML content" -ForegroundColor White
        Write-Host "6. Click 'Add secret'" -ForegroundColor White
    }
    else {
        Write-Host "? Failed to retrieve publish profile" -ForegroundColor Red
        Write-Host "?? Try checking if the App Service exists and you have permissions" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "? Error occurred: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "?? Troubleshooting steps:" -ForegroundColor Yellow
    Write-Host "1. Make sure you're logged into Azure CLI: az login" -ForegroundColor White
    Write-Host "2. Check if you have access to the resource group: az group show --name smarttask-ai-rg-production" -ForegroundColor White
    Write-Host "3. Verify the App Service exists: az webapp show --name smarttask-ai-api --resource-group smarttask-ai-rg-production" -ForegroundColor White
}