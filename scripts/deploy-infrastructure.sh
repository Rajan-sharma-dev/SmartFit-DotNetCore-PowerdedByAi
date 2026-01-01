#!/bin/bash

# Deploy Azure Infrastructure using Bicep
set -e

RESOURCE_GROUP_NAME="smarttask-ai-rg"
LOCATION="East US 2"
ENVIRONMENT=$1

if [ -z "$ENVIRONMENT" ]; then
    echo "Usage: $0 <environment>"
    echo "Example: $0 production"
    echo "Valid environments: dev, staging, production"
    exit 1
fi

echo "?? Deploying infrastructure for environment: $ENVIRONMENT"
echo "?? Location: $LOCATION"
echo "?? Resource Group: $RESOURCE_GROUP_NAME-$ENVIRONMENT"

# Prompt for sensitive parameters
echo ""
read -s -p "Enter SQL Server admin password (min 8 characters, must contain uppercase, lowercase, number, and special char): " SQL_ADMIN_PASSWORD
echo ""
read -s -p "Enter OpenAI API Key: " OPENAI_API_KEY  
echo ""
read -s -p "Enter JWT Secret Key (min 32 characters): " JWT_SECRET_KEY
echo ""

# Validate inputs
if [ ${#SQL_ADMIN_PASSWORD} -lt 8 ]; then
    echo "? SQL Server password must be at least 8 characters long"
    exit 1
fi

if [ ${#JWT_SECRET_KEY} -lt 32 ]; then
    echo "? JWT Secret Key must be at least 32 characters long"
    exit 1
fi

if [ -z "$OPENAI_API_KEY" ]; then
    echo "? OpenAI API Key is required"
    exit 1
fi

# Check if Azure CLI is logged in
if ! az account show >/dev/null 2>&1; then
    echo "? Not logged in to Azure CLI. Please run 'az login' first."
    exit 1
fi

echo "? Prerequisites validated"

# Create resource group if it doesn't exist
echo "???  Creating resource group..."
az group create \
    --name "$RESOURCE_GROUP_NAME-$ENVIRONMENT" \
    --location "$LOCATION" \
    --output table

echo "?? Deploying Bicep template..."

# Deploy Bicep template
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group "$RESOURCE_GROUP_NAME-$ENVIRONMENT" \
    --template-file infrastructure/main.bicep \
    --parameters environment="$ENVIRONMENT" \
    --parameters location="$LOCATION" \
    --parameters sqlAdminPassword="$SQL_ADMIN_PASSWORD" \
    --parameters openAiApiKey="$OPENAI_API_KEY" \
    --parameters jwtSecretKey="$JWT_SECRET_KEY" \
    --output json)

if [ $? -eq 0 ]; then
    echo "? Infrastructure deployment completed successfully!"
    
    # Extract outputs
    WEB_APP_URL=$(echo $DEPLOYMENT_OUTPUT | jq -r '.properties.outputs.webAppUrl.value')
    WEB_APP_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.properties.outputs.webAppName.value')
    SQL_SERVER_FQDN=$(echo $DEPLOYMENT_OUTPUT | jq -r '.properties.outputs.sqlServerFqdn.value')
    APP_INSIGHTS_CONNECTION=$(echo $DEPLOYMENT_OUTPUT | jq -r '.properties.outputs.applicationInsightsConnectionString.value')
    
    echo ""
    echo "?? Deployment Summary:"
    echo "  Web App URL: $WEB_APP_URL"
    echo "  Web App Name: $WEB_APP_NAME" 
    echo "  SQL Server: $SQL_SERVER_FQDN"
    echo "  App Insights: Configured"
    echo ""
    echo "?? Next Steps:"
    echo "  1. Download publish profile from Azure Portal for $WEB_APP_NAME"
    echo "  2. Add the publish profile as GitHub secret: AZURE_WEBAPP_PUBLISH_PROFILE_$(echo $ENVIRONMENT | tr '[:lower:]' '[:upper:]')"
    echo "  3. Push code to trigger deployment pipeline"
    echo "  4. Test the application at: $WEB_APP_URL/health"
    echo ""
else
    echo "? Infrastructure deployment failed!"
    exit 1
fi