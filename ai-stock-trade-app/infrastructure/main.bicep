@description('SQL Server admin username')
param sqlAdminUsername string = 'sqladmin'

@description('SQL Server admin password')
@secure()
param sqlAdminPassword string

@description('The location for all resources')
param location string = resourceGroup().location

@description('The name of the application')
param appName string

@description('The environment (dev, staging, prod)')
param environment string = 'dev'

@description('The SKU of the App Service Plan')
param appServicePlanSku string = 'B1'

@description('Container registry name')
@minLength(5)
param containerRegistryName string

@description('Container image name and tag')
param containerImage string

@description('Alpha Vantage API Key')
@secure()
param alphaVantageApiKey string = ''

@description('Twelve Data API Key')
@secure()
param twelveDataApiKey string = ''

@description('Instance number for resource differentiation')
param instanceNumber string = '002'

@description('Whether to deploy container registry (only for dev environment)')
param deployContainerRegistry bool = true

@description('Azure AD admin object ID for SQL Server')
param azureAdAdminObjectId string = ''

@description('Azure AD admin login name for SQL Server')
param azureAdAdminLogin string = ''

@description('Whether to enable Azure AD only authentication (required for some subscriptions)')
param enableAzureAdOnlyAuth bool = false

// Variables
var resourceNamePrefix = '${appName}-${environment}'
var appServicePlanName = 'asp-${resourceNamePrefix}-${instanceNumber}'
var webAppName = 'app-${resourceNamePrefix}-${instanceNumber}'
var containerRegistryResourceName = 'cr${toLower(replace(containerRegistryName, '-', ''))}${toLower(environment)}${instanceNumber}${uniqueString(resourceGroup().id)}'
var keyVaultName = 'kv-${resourceNamePrefix}-${instanceNumber}'
var applicationInsightsName = 'appi-${resourceNamePrefix}-${instanceNumber}'
var logAnalyticsWorkspaceName = 'log-${resourceNamePrefix}-${instanceNumber}'
var sqlServerName = 'sql-${resourceNamePrefix}-${instanceNumber}'
var sqlDatabaseName = 'sqldb-${resourceNamePrefix}-${instanceNumber}'

// Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Application Insights
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

// Container Registry (only deployed for dev environment)
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2025-04-01' = if (deployContainerRegistry) {
  name: containerRegistryResourceName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForTemplateDeployment: true
    enableRbacAuthorization: true
  }
}

// SQL Server
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: enableAzureAdOnlyAuth ? null : sqlAdminUsername
    administratorLoginPassword: enableAzureAdOnlyAuth ? null : sqlAdminPassword
    version: '12.0'
    publicNetworkAccess: 'Enabled'
  }
}

// SQL Server Azure AD Administrator (when Azure AD auth is enabled)
resource sqlServerAzureAdAdmin 'Microsoft.Sql/servers/administrators@2023-08-01-preview' = if (enableAzureAdOnlyAuth && !empty(azureAdAdminObjectId)) {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: azureAdAdminLogin
    sid: azureAdAdminObjectId
    tenantId: subscription().tenantId
  }
}

// SQL Database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2GB
  }
}

// SQL Server Firewall Rule for Azure Services
resource sqlServerFirewallRuleAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Store SQL connection info in Key Vault
resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'SqlConnectionString'
  properties: {
    value: enableAzureAdOnlyAuth 
      ? 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;'
      : 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};User ID=${sqlAdminUsername};Password=${sqlAdminPassword};Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;'
  }
}

// Store API keys in Key Vault
resource alphaVantageSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = if (!empty(alphaVantageApiKey)) {
  parent: keyVault
  name: 'AlphaVantageApiKey'
  properties: {
    value: alphaVantageApiKey
  }
}

resource twelveDataSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = if (!empty(twelveDataApiKey)) {
  parent: keyVault
  name: 'TwelveDataApiKey'
  properties: {
    value: twelveDataApiKey
  }
}

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  properties: {
    reserved: true
  }
  sku: {
    name: appServicePlanSku
  }
}

// Web App
resource webApp 'Microsoft.Web/sites@2024-11-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: deployContainerRegistry ? 'DOCKER|${containerRegistry!.properties.loginServer}/${containerImage}' : 'DOCKER|${containerImage}'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: applicationInsights.properties.InstrumentationKey
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'XDT_MicrosoftApplicationInsights_Mode'
          value: 'Recommended'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment == 'prod' ? 'Production' : 'Development'
        }
        {
          name: 'AlphaVantage__ApiKey'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=AlphaVantageApiKey)'
        }
        {
          name: 'TwelveData__ApiKey'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=TwelveDataApiKey)'
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=SqlConnectionString)'
        }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: enableAzureAdOnlyAuth 
            ? 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;'
            : 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};User ID=${sqlAdminUsername};Password=${sqlAdminPassword};Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;'
          type: 'SQLAzure'
        }
      ]
      healthCheckPath: '/health'
    }
    httpsOnly: true
  }
}

// Grant Key Vault access to Web App
resource keyVaultAccessPolicy 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, webApp.id, 'Key Vault Secrets User')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// NOTE: When enableAzureAdOnlyAuth is true, after deployment you need to manually:
// 1. Connect to the SQL database as the Azure AD admin
// 2. Run: CREATE USER [app-aistock-{environment}-{instanceNumber}] FROM EXTERNAL PROVIDER
// 3. Run: ALTER ROLE db_datareader ADD MEMBER [app-aistock-{environment}-{instanceNumber}]
// 4. Run: ALTER ROLE db_datawriter ADD MEMBER [app-aistock-{environment}-{instanceNumber}]
// 5. Run: ALTER ROLE db_ddladmin ADD MEMBER [app-aistock-{environment}-{instanceNumber}]

// Outputs
output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output webAppPrincipalId string = webApp.identity.principalId
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabaseName
output containerRegistryName string = deployContainerRegistry ? containerRegistry!.name : 'not-deployed'
output containerRegistryLoginServer string = deployContainerRegistry ? containerRegistry!.properties.loginServer : 'not-deployed'
output keyVaultName string = keyVault.name
output applicationInsightsName string = applicationInsights.name
