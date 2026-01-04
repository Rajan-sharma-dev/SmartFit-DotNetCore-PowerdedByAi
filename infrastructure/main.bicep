@description('The name of the web app')
param webAppName string = 'smarttask-ai-api'

@description('The SKU of the App Service plan')
@allowed(['B1', 'B2', 'B3', 'S1', 'S2', 'S3', 'P1v2', 'P2v2', 'P3v2'])
param sku string = 'B1'

@description('The location for all resources')
param location string = resourceGroup().location

@description('Environment name')
@allowed(['dev', 'staging', 'production'])
param environment string = 'production'

@description('Database administrator login name')
param sqlAdminLogin string = 'sqladmin'

@description('Database administrator login password')
@secure()
param sqlAdminPassword string

@description('OpenAI API Key')
@secure()
param openAiApiKey string

@description('JWT Secret Key')
@secure()
param jwtSecretKey string

// Variables
var appServicePlanName = '${webAppName}-plan-${environment}'
var webAppFullName = '${webAppName}-${environment}'
var sqlServerName = '${webAppName}-sql-${environment}'
var sqlDatabaseName = 'SmartTaskDB'
var applicationInsightsName = '${webAppName}-ai-${environment}'

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: sku
  }
  properties: {
    reserved: false
  }
}

// Application Insights
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
  }
}

// SQL Server
resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    publicNetworkAccess: 'Enabled' // Enable public network access
  }
}

// SQL Database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: environment == 'production' ? 'S1' : 'Basic'
    tier: environment == 'production' ? 'Standard' : 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: environment == 'production' ? 268435456000 : 2147483648
  }
}

// SQL Server Firewall Rule for Azure Services
resource sqlServerFirewallRuleAzure 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// SQL Server Firewall Rule for Development (only in non-production environments)
resource sqlServerFirewallRuleDev 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = if (environment != 'production') {
  parent: sqlServer
  name: 'AllowDevelopment'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '255.255.255.255'
  }
}

// SQL Server Firewall Rule for Production Admin Access (restrictive)
resource sqlServerFirewallRuleAdmin 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = if (environment == 'production') {
  parent: sqlServer
  name: 'AllowAdminAccess'
  properties: {
    startIpAddress: '0.0.0.0' // Replace with your office/admin IP range
    endIpAddress: '255.255.255.255' // Replace with your office/admin IP range
  }
}

// Web App
resource webApp 'Microsoft.Web/sites@2022-03-01' = {
  name: webAppFullName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      alwaysOn: true
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'FtpsOnly'
      healthCheckPath: '/health'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment
        }
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'false'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
        {
          name: 'OpenAi__ApiKey'
          value: openAiApiKey
        }
        {
          name: 'JwtSettings__SecretKey'
          value: jwtSecretKey
        }
        {
          name: 'JwtSettings__Issuer'
          value: 'SmartTaskAI-${environment}'
        }
        {
          name: 'JwtSettings__Audience'
          value: 'SmartTaskAI-Users-${environment}'
        }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: 'Server=${sqlServer.properties.fullyQualifiedDomainName};Database=${sqlDatabaseName};User Id=${sqlAdminLogin};Password=${sqlAdminPassword};TrustServerCertificate=true;'
          type: 'SQLServer'
        }
      ]
    }
  }
}

// Web App Diagnostic Settings
resource webAppDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${webAppFullName}-diagnostics'
  scope: webApp
  properties: {
    workspaceId: applicationInsights.id
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// Output values
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output webAppName string = webApp.name
output applicationInsightsKey string = applicationInsights.properties.InstrumentationKey
output applicationInsightsConnectionString string = applicationInsights.properties.ConnectionString
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output resourceGroupName string = resourceGroup().name