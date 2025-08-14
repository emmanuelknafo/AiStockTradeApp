@description('SQL Server admin username')
param sqlAdminUsername string = 'sqladmin'

@description('SQL Server admin password (ignored when enableAzureAdOnlyAuth = true). Left blank when using Azure AD only auth.')
@secure()
param sqlAdminPassword string = ''

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
@description('Optional application semantic version (injected into APP_VERSION setting)')
param appVersion string = ''

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

@description('Enable private endpoint for Azure SQL and disable public network access (requires App Service Plan SKU that supports VNet integration e.g. S1 or above). When true, a VNet, subnets, private endpoint and DNS zone are provisioned.')
param enablePrivateSql bool = true

@description('If false, networking resources (VNet, subnets, private endpoint, DNS) are not created/updated. Use in app-only CI/CD to avoid touching in-use subnets.')
param manageNetworking bool = true

@description('Enable private endpoint for Azure Key Vault and disable public network access (recommended if org policy blocks public access).')
param enablePrivateKeyVault bool = true

@description('Address space for the virtual network (only used when enablePrivateSql = true)')
param vnetAddressSpace string = '10.20.0.0/16'

@description('Subnet CIDR for App Service regional VNet integration (must be at least /27). Only used when enablePrivateSql = true')
param appIntegrationSubnetPrefix string = '10.20.1.0/27'

@description('Subnet CIDR for private endpoints (at least /28 recommended). Only used when enablePrivateSql = true')
param privateEndpointSubnetPrefix string = '10.20.2.0/28'

@description('Private DNS zone name for Azure SQL private endpoints (override for sovereign clouds). Default derived from sqlServerHostname suffix (handles leading dot).')
param privateSqlPrivateDnsZoneName string = 'privatelink${az.environment().suffixes.sqlServerHostname}'

@description('Private DNS zone name for Azure Key Vault private endpoints.')
param privateKvPrivateDnsZoneName string = 'privatelink.vaultcore.azure.net'

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
var vnetName = 'vnet-${resourceNamePrefix}-${instanceNumber}'
var appIntegrationSubnetName = 'snet-appintegration'
var privateEndpointSubnetName = 'snet-private-endpoints'
// Use Azure AD auth only if explicitly enabled AND admin values provided
var useAzureAdAuth = enableAzureAdOnlyAuth && !empty(azureAdAdminObjectId) && !empty(azureAdAdminLogin)

// Whether any private networking is required for the app
var requireVNetIntegration = enablePrivateSql || enablePrivateKeyVault

// Validate that when private SQL is enabled we are not using a Basic plan (Basic does not support VNet integration)
@sys.description('Ensure App Service Plan SKU supports VNet integration when private networking is enabled and we are managing networking')
var appServicePlanSkuEffective = (requireVNetIntegration && manageNetworking && toLower(appServicePlanSku) == 'b1') ? 'S1' : appServicePlanSku

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
    ...(manageNetworking
      ? {
          publicNetworkAccess: enablePrivateKeyVault ? 'Disabled' : 'Enabled'
        }
      : {})
  }
}

// SQL Server
resource sqlServer 'Microsoft.Sql/servers@2024-11-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    // Only include SQL admin credentials when NOT using Azure AD only authentication
    // Using object spread to avoid sending nulls which can cause deployment errors in AAD-only mode
    // Fallback: if AD-only requested but admin values missing, still provision SQL admin login to avoid deployment failure
    ...(useAzureAdAuth
      ? {}
      : {
          administratorLogin: sqlAdminUsername
          administratorLoginPassword: sqlAdminPassword
        })
    version: '12.0'
    ...(manageNetworking
      ? {
          publicNetworkAccess: enablePrivateSql ? 'Disabled' : 'Enabled'
        }
      : {})
  }
}

// SQL Server Azure AD Administrator (deploy only when all required values provided)
resource sqlServerAzureAdAdmin 'Microsoft.Sql/servers/administrators@2024-11-01-preview' = if (useAzureAdAuth) {
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
resource sqlDatabase 'Microsoft.Sql/servers/databases@2024-11-01-preview' = {
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
resource sqlServerFirewallRuleAzure 'Microsoft.Sql/servers/firewallRules@2024-11-01-preview' = if (!enablePrivateSql && manageNetworking) {
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
    value: useAzureAdAuth
      ? 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=true;TrustServerCertificate=false;Connection Timeout=60;Command Timeout=120;'
      : 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};User ID=${sqlAdminUsername};Password=${sqlAdminPassword};Encrypt=true;TrustServerCertificate=false;Connection Timeout=60;Command Timeout=120;'
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
    name: appServicePlanSkuEffective
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
    // Attach to VNet integration subnet when private SQL enabled
  ...(requireVNetIntegration && manageNetworking
      ? {
          virtualNetworkSubnetId: resourceId(
            'Microsoft.Network/virtualNetworks/subnets',
            vnetName,
            appIntegrationSubnetName
          )
        }
      : {})
    siteConfig: {
      linuxFxVersion: deployContainerRegistry
        ? 'DOCKER|${containerRegistry!.properties.loginServer}/${containerImage}'
        : 'DOCKER|${containerImage}'
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
        {
          name: 'APP_VERSION'
          value: empty(appVersion) ? '' : appVersion
        }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: useAzureAdAuth
            ? 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=true;TrustServerCertificate=false;Connection Timeout=60;Command Timeout=120;'
            : 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};User ID=${sqlAdminUsername};Password=${sqlAdminPassword};Encrypt=true;TrustServerCertificate=false;Connection Timeout=60;Command Timeout=120;'
          type: 'SQLAzure'
        }
      ]
      healthCheckPath: '/health'
    }
    httpsOnly: true
  }
}

// Networking (only when private SQL requested)
resource vnet 'Microsoft.Network/virtualNetworks@2024-07-01' = if (requireVNetIntegration && manageNetworking) {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressSpace
      ]
    }
    subnets: [
      // Subnet for App Service regional VNet integration
      {
        name: appIntegrationSubnetName
        properties: {
          addressPrefix: appIntegrationSubnetPrefix
          delegations: [
            {
              name: 'webappDelegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
      // Subnet for private endpoints
      {
        name: privateEndpointSubnetName
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
          privateLinkServiceNetworkPolicies: 'Enabled'
        }
      }
    ]
  }
}

// Private DNS Zone for SQL
resource privateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = if (enablePrivateSql && manageNetworking) {
  name: privateSqlPrivateDnsZoneName
  location: 'global'
}

// Link VNet to Private DNS Zone
resource privateDnsVnetLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = if (enablePrivateSql && manageNetworking) {
  name: 'vnet-link'
  parent: privateDnsZone
  location: 'global'
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

// Private Endpoint for SQL Server
resource sqlPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-07-01' = if (enablePrivateSql && manageNetworking) {
  name: 'pe-${sqlServerName}'
  location: location
  properties: {
    subnet: {
      id: resourceId('Microsoft.Network/virtualNetworks/subnets', vnetName, privateEndpointSubnetName)
    }
    privateLinkServiceConnections: [
      {
        name: 'plsc-sql'
        properties: {
          groupIds: ['sqlServer']
          privateLinkServiceId: sqlServer.id
        }
      }
    ]
  }
}

// Associate Private Endpoint with DNS Zone (creates A record)
resource sqlPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-07-01' = if (enablePrivateSql && manageNetworking) {
  name: 'pdzg-sql'
  parent: sqlPrivateEndpoint
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'config'
        properties: {
          privateDnsZoneId: privateDnsZone.id
        }
      }
    ]
  }
}

// Private DNS Zone for Key Vault
resource privateDnsZoneKv 'Microsoft.Network/privateDnsZones@2024-06-01' = if (enablePrivateKeyVault && manageNetworking) {
  name: privateKvPrivateDnsZoneName
  location: 'global'
}

// Link VNet to Key Vault Private DNS Zone
resource privateDnsVnetLinkKv 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = if (enablePrivateKeyVault && manageNetworking) {
  name: 'vnet-link-kv'
  parent: privateDnsZoneKv
  location: 'global'
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

// Private Endpoint for Key Vault
resource keyVaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-07-01' = if (enablePrivateKeyVault && manageNetworking) {
  name: 'pe-${keyVaultName}'
  location: location
  properties: {
    subnet: {
      id: resourceId('Microsoft.Network/virtualNetworks/subnets', vnetName, privateEndpointSubnetName)
    }
    privateLinkServiceConnections: [
      {
        name: 'plsc-kv'
        properties: {
          groupIds: ['vault']
          privateLinkServiceId: keyVault.id
        }
      }
    ]
  }
}

// Associate Key Vault Private Endpoint with DNS Zone
resource keyVaultPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-07-01' = if (enablePrivateKeyVault && manageNetworking) {
  name: 'pdzg-kv'
  parent: keyVaultPrivateEndpoint
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'config'
        properties: {
          privateDnsZoneId: privateDnsZoneKv.id
        }
      }
    ]
  }
}

// Grant Key Vault access to Web App
resource keyVaultAccessPolicy 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, webApp.id, 'Key Vault Secrets User')
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6'
    ) // Key Vault Secrets User
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
output containerRegistryLoginServer string = deployContainerRegistry
  ? containerRegistry!.properties.loginServer
  : 'not-deployed'
output keyVaultName string = keyVault.name
output applicationInsightsName string = applicationInsights.name
output vnetName string = (requireVNetIntegration && manageNetworking) ? vnetName : 'not-deployed'
output privateSqlEnabled bool = enablePrivateSql
