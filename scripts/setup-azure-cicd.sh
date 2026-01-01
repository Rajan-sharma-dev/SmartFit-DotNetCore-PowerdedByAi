#!/bin/bash

# SmartTask AI Assistant - Complete Setup Script for Azure CI/CD
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}?? SmartTask AI Assistant - Azure CI/CD Setup${NC}"
echo -e "${BLUE}================================================${NC}"
echo ""

# Check prerequisites
echo -e "${YELLOW}?? Checking prerequisites...${NC}"

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo -e "${RED}? Azure CLI not found. Please install it first.${NC}"
    echo "Visit: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check if user is logged into Azure CLI
if ! az account show >/dev/null 2>&1; then
    echo -e "${YELLOW}??  Not logged in to Azure CLI. Logging you in...${NC}"
    az login
fi

# Check if git is installed
if ! command -v git &> /dev/null; then
    echo -e "${RED}? Git not found. Please install it first.${NC}"
    exit 1
fi

# Check if jq is installed (for JSON parsing)
if ! command -v jq &> /dev/null; then
    echo -e "${YELLOW}??  jq not found. Installing...${NC}"
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        sudo apt-get update && sudo apt-get install -y jq
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        brew install jq
    else
        echo -e "${RED}? Please install jq manually${NC}"
        exit 1
    fi
fi

echo -e "${GREEN}? All prerequisites are installed${NC}"
echo ""

# Get user configuration
echo -e "${BLUE}?? Configuration Setup${NC}"
echo -e "${BLUE}=====================${NC}"

read -p "Enter your GitHub username: " GITHUB_USERNAME
read -p "Enter your repository name (default: SmartTask-AI-Assistant-Backend): " REPO_NAME
REPO_NAME=${REPO_NAME:-SmartTask-AI-Assistant-Backend}

read -p "Deploy staging environment? (y/n, default: y): " DEPLOY_STAGING
DEPLOY_STAGING=${DEPLOY_STAGING:-y}

read -p "Deploy production environment? (y/n, default: y): " DEPLOY_PRODUCTION  
DEPLOY_PRODUCTION=${DEPLOY_PRODUCTION:-y}

echo ""
echo -e "${YELLOW}?? You will need the following secrets for GitHub:${NC}"
echo "   - OpenAI API Key"
echo "   - JWT Secret Key (32+ characters)"
echo "   - SQL Server Admin Password"
echo ""
read -p "Press Enter to continue..."

# Deploy infrastructure
if [[ $DEPLOY_STAGING == "y" ]]; then
    echo -e "${BLUE}???  Deploying staging infrastructure...${NC}"
    ./scripts/deploy-infrastructure.sh staging
    echo ""
fi

if [[ $DEPLOY_PRODUCTION == "y" ]]; then
    echo -e "${BLUE}???  Deploying production infrastructure...${NC}"
    ./scripts/deploy-infrastructure.sh production
    echo ""
fi

# GitHub repository setup
echo -e "${BLUE}?? GitHub Repository Configuration${NC}"
echo -e "${BLUE}=================================${NC}"

REPO_URL="https://github.com/${GITHUB_USERNAME}/${REPO_NAME}"

echo "Please complete the following steps in GitHub:"
echo ""
echo "1. Go to: ${REPO_URL}/settings/secrets/actions"
echo ""

if [[ $DEPLOY_PRODUCTION == "y" ]]; then
    echo -e "${YELLOW}Production Secrets:${NC}"
    echo "   - AZURE_WEBAPP_PUBLISH_PROFILE_PRODUCTION"
    echo "   - DATABASE_CONNECTION_STRING" 
    echo "   - JWT_SECRET_KEY"
    echo "   - OPENAI_API_KEY"
    echo "   - APPINSIGHTS_CONNECTION_STRING"
    echo ""
fi

if [[ $DEPLOY_STAGING == "y" ]]; then
    echo -e "${YELLOW}Staging Secrets:${NC}"
    echo "   - AZURE_WEBAPP_PUBLISH_PROFILE_STAGING"
    echo "   - STAGING_DATABASE_CONNECTION_STRING"
    echo "   - STAGING_JWT_SECRET_KEY" 
    echo "   - STAGING_OPENAI_API_KEY"
    echo ""
fi

echo "2. Download publish profiles from Azure Portal:"
if [[ $DEPLOY_PRODUCTION == "y" ]]; then
    echo "   - Go to: https://portal.azure.com ? smarttask-ai-api-production ? Get publish profile"
fi
if [[ $DEPLOY_STAGING == "y" ]]; then
    echo "   - Go to: https://portal.azure.com ? smarttask-ai-api-staging ? Get publish profile"
fi
echo ""

# Next steps
echo -e "${GREEN}?? Setup Complete!${NC}"
echo -e "${GREEN}==================${NC}"
echo ""
echo -e "${YELLOW}Next Steps:${NC}"
echo "1. Configure GitHub secrets as shown above"
echo "2. Push your code to trigger the CI/CD pipeline:"
echo "   git add ."
echo "   git commit -m 'Add Azure CI/CD configuration'"
echo "   git push origin develop  # for staging"
echo "   git push origin main     # for production"
echo ""
echo "3. Monitor deployments at: ${REPO_URL}/actions"
echo ""
echo "4. Test your deployed application:"
if [[ $DEPLOY_STAGING == "y" ]]; then
    echo "   Staging: https://smarttask-ai-api-staging.azurewebsites.net/health"
fi
if [[ $DEPLOY_PRODUCTION == "y" ]]; then
    echo "   Production: https://smarttask-ai-api-production.azurewebsites.net/health"
fi
echo ""

echo -e "${BLUE}?? Documentation:${NC}"
echo "   - Full deployment guide: ./DEPLOYMENT.md"
echo "   - Infrastructure: ./infrastructure/main.bicep"
echo "   - CI/CD Pipeline: ./.github/workflows/azure-deploy.yml"
echo ""

echo -e "${GREEN}Happy deploying! ??${NC}"