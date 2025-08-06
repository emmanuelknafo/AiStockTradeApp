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

// Variables
var resourceNamePrefix = '${appName}-${environment}'
var appServicePlanName = 'asp-${resourceNamePrefix}-${instanceNumber}'
var webAppName = 'app-${resourceNamePrefix}-${instanceNumber}'
var containerRegistryResourceName = 'cr${toLower(replace(containerRegistryName, '-', ''))}${toLower(environment)}${instanceNumber}${uniqueString(resourceGroup().id)}'
var keyVaultName = 'kv-${resourceNamePrefix}-${instanceNumber}'
var applicationInsightsName = 'appi-${resourceNamePrefix}-${instanceNumber}'
var logAnalyticsWorkspaceName = 'log-${resourceNamePrefix}-${instanceNumber}'

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
      ]
      healthCheckPath: '/health'
    }
    httpsOnly: true
  }
}

// Enable Application Insights site extension
resource webAppSiteExtension 'Microsoft.Web/sites/siteextensions@2022-09-01' = {
  parent: webApp
  name: 'Microsoft.ApplicationInsights.AzureWebSites'
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
output containerRegistryName string = deployContainerRegistry ? containerRegistry!.name : 'not-deployed'
output containerRegistryLoginServer string = deployContainerRegistry ? containerRegistry!.properties.loginServer : 'not-deployed'
output keyVaultName string = keyVault.name
output applicationInsightsName string = applicationInsights.name
