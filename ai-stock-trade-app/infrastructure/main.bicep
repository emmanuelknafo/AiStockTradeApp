@description('The location for all resources')
param location string = resourceGroup().location

@description('The name of the application')
param appName string

@description('The environment (dev, staging, prod)')
param environment string = 'dev'

@description('The SKU of the App Service Plan')
param appServicePlanSku string = 'B1'

@description('Container registry name')
param containerRegistryName string

@description('Container image name and tag')
param containerImage string

@description('Alpha Vantage API Key')
@secure()
param alphaVantageApiKey string = ''

@description('Twelve Data API Key')
@secure()
param twelveDataApiKey string = ''

// Variables
var resourceNamePrefix = '${appName}-${environment}'
var appServicePlanName = '${resourceNamePrefix}-asp'
var webAppName = '${resourceNamePrefix}-webapp'
var containerRegistryResourceName = '${containerRegistryName}${environment}'
var keyVaultName = '${resourceNamePrefix}-kv'
var applicationInsightsName = '${resourceNamePrefix}-ai'
var logAnalyticsWorkspaceName = '${resourceNamePrefix}-law'

// Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
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

// Container Registry
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
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
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
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

// Store API keys in Key Vault
resource alphaVantageSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(alphaVantageApiKey)) {
  parent: keyVault
  name: 'AlphaVantageApiKey'
  properties: {
    value: alphaVantageApiKey
  }
}

resource twelveDataSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(twelveDataApiKey)) {
  parent: keyVault
  name: 'TwelveDataApiKey'
  properties: {
    value: twelveDataApiKey
  }
}

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
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
resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerRegistry.properties.loginServer}/${containerImage}'
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
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${containerRegistry.properties.loginServer}'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_USERNAME'
          value: containerRegistry.listCredentials().username
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_PASSWORD'
          value: containerRegistry.listCredentials().passwords[0].value
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
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

// Outputs
output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output containerRegistryName string = containerRegistry.name
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
output keyVaultName string = keyVault.name
output applicationInsightsName string = applicationInsights.name