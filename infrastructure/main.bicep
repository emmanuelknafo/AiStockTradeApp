// Entra-only: SQL admin username/password removed. Server will be created without SQL logins.

@description('The location for all resources')
param location string = resourceGroup().location

@description('The name of the application')
param appName string

@description('The environment (dev, staging, prod)')
param environment string = 'dev'

@description('The SKU of the App Service Plan')
param appServicePlanSku string = 'P0v3'

@description('Container registry name')
@minLength(5)
param containerRegistryName string

@description('Container image name and tag')
param containerImage string

@description('API container image name and tag')
param containerImageApi string = 'aistocktradeapp-api:latest'

@description('MCP server container image name and tag')
param containerImageMcp string = 'aistocktradeapp-mcp:latest'

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

@description('Whether to deploy Azure Load Testing resource as part of infrastructure')
param deployLoadTesting bool = false

@description('Azure AD admin object ID for SQL Server')
param azureAdAdminObjectId string = ''

@description('Azure AD admin login name for SQL Server')
param azureAdAdminLogin string = ''

@description('Whether to enable Azure AD only authentication (required for some subscriptions)')
param enableAzureAdOnlyAuth bool = false

@description('Temporary SQL administrator login for initial server creation (SQL auth will be disabled when enableAzureAdOnlyAuth = true). Provide via pipeline; do not store in files.')
param sqlAdministratorLogin string = ''

@description('Temporary SQL administrator password for initial server creation (SQL auth will be disabled when enableAzureAdOnlyAuth = true). Provide via pipeline; do not store in files.')
@secure()
param sqlAdministratorPassword string = ''

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

@description('If false, skip creating/updating SQL Server and Database (use existing). Useful for app-only CI/CD deploys when Azure AD-only is already enabled.')
param manageSql bool = true

// Variables
var resourceNamePrefix = '${appName}-${environment}'
var appServicePlanName = 'asp-${resourceNamePrefix}-${instanceNumber}'
var webAppName = 'app-${resourceNamePrefix}-${instanceNumber}'
var webApiName = 'api-${resourceNamePrefix}-${instanceNumber}'
var webMcpName = 'mcp-${resourceNamePrefix}-${instanceNumber}'
var containerRegistryResourceName = 'cr${toLower(replace(containerRegistryName, '-', ''))}${toLower(environment)}${instanceNumber}${uniqueString(resourceGroup().id)}'
var keyVaultName = 'kv-${resourceNamePrefix}-${instanceNumber}'
var userAssignedIdentityName = 'ui-${resourceNamePrefix}-${instanceNumber}'
var applicationInsightsUiName = 'appi-ui-${resourceNamePrefix}-${instanceNumber}'
var applicationInsightsApiName = 'appi-api-${resourceNamePrefix}-${instanceNumber}'
var applicationInsightsMcpName = 'appi-mcp-${resourceNamePrefix}-${instanceNumber}'
var logAnalyticsWorkspaceName = 'log-${resourceNamePrefix}-${instanceNumber}'
var sqlServerName = 'sql-${resourceNamePrefix}-${instanceNumber}'
var sqlDatabaseName = 'sqldb-${resourceNamePrefix}-${instanceNumber}'
var vnetName = 'vnet-${resourceNamePrefix}-${instanceNumber}'
var appIntegrationSubnetName = 'snet-appintegration'
var privateEndpointSubnetName = 'snet-private-endpoints'
var loadTestResourceName = 'lt-${resourceNamePrefix}-${instanceNumber}'
// Entra-only: Always use Azure AD auth for connection strings.

// Defaults for temporary SQL admin when not provided
var defaultSqlAdminLogin = 'tempSqlAdmin'
// Build a password with upper, lower, digits, and special chars; deterministic per RG/env/instance
var defaultSqlAdminPassword = '${toUpper(substring(uniqueString(resourceGroup().id, appName, environment, instanceNumber), 0, 6))}!${uniqueString(resourceGroup().id, 'sql', instanceNumber)}a1@'

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

// Application Insights for UI
resource applicationInsightsUI 'Microsoft.Insights/components@2020-02-02' = {
	name: applicationInsightsUiName
	location: location
	kind: 'web'
	properties: {
		Application_Type: 'web'
		WorkspaceResourceId: logAnalyticsWorkspace.id
	}
}

// Application Insights for API
resource applicationInsightsAPI 'Microsoft.Insights/components@2020-02-02' = {
	name: applicationInsightsApiName
	location: location
	kind: 'web'
	properties: {
		Application_Type: 'web'
		WorkspaceResourceId: logAnalyticsWorkspace.id
	}
}

// Application Insights for MCP Server
resource applicationInsightsMCP 'Microsoft.Insights/components@2020-02-02' = {
	name: applicationInsightsMcpName
	location: location
	kind: 'web'
	properties: {
		Application_Type: 'web'
		WorkspaceResourceId: logAnalyticsWorkspace.id
	}
}

// Azure Load Testing resource (optional)
resource loadTest 'Microsoft.LoadTestService/loadTests@2022-12-01' = if (deployLoadTesting) {
  name: loadTestResourceName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    description: 'Load Test resource for ${appName}-${environment}-${instanceNumber}'
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

// Shared User-Assigned Managed Identity for UI + API
resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: userAssignedIdentityName
  location: location
}

// SQL Server (create/update when manageSql=true)
resource sqlServer 'Microsoft.Sql/servers@2024-11-01-preview' = if (manageSql) {
  name: sqlServerName
  location: location
  tags: {
    aadOnlyAuth: string(enableAzureAdOnlyAuth)
  }
  properties: {
    version: '12.0'
    // Although we operate Entra-only, Azure SQL requires a SQL admin at creation time.
    // If none provided, use safe defaults; SQL auth is disabled post-deploy when enableAzureAdOnlyAuth = true.
    administratorLogin: empty(sqlAdministratorLogin) ? defaultSqlAdminLogin : sqlAdministratorLogin
    administratorLoginPassword: empty(sqlAdministratorPassword) ? defaultSqlAdminPassword : sqlAdministratorPassword
    ...(manageNetworking
      ? {
          publicNetworkAccess: enablePrivateSql ? 'Disabled' : 'Enabled'
        }
      : {})
  }
}

// When skipping SQL management, avoid referencing conditional resource properties.
// Derive the public FQDN; private DNS will resolve to private IP when private endpoint is used.
var sqlServerFqdnValue = '${sqlServerName}${az.environment().suffixes.sqlServerHostname}'

// Enforce Azure AD-only authentication when requested
resource sqlServerAadOnly 'Microsoft.Sql/servers/azureADOnlyAuthentications@2021-11-01' = if (manageSql && !empty(azureAdAdminObjectId) && !empty(azureAdAdminLogin) && enableAzureAdOnlyAuth) {
  parent: sqlServer
  name: 'Default'
  properties: {
    azureADOnlyAuthentication: enableAzureAdOnlyAuth
  }
}

// SQL Server Azure AD Administrator (deploy only when all required values provided)
resource sqlServerAzureAdAdmin 'Microsoft.Sql/servers/administrators@2024-11-01-preview' = if (manageSql && !empty(azureAdAdminObjectId) && !empty(azureAdAdminLogin)) {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: azureAdAdminLogin
    sid: azureAdAdminObjectId
    tenantId: subscription().tenantId
  }
}

// NOTE: We no longer auto-assign the SQL AAD admin in ARM to avoid timing/conflict issues.
// The pipelines assign the Web/App MI as SQL AAD admin after the server exists via CLI.

// SQL Database (create/update when manageSql=true)
resource sqlDatabase 'Microsoft.Sql/servers/databases@2024-11-01-preview' = if (manageSql) {
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
resource sqlServerFirewallRuleAzure 'Microsoft.Sql/servers/firewallRules@2024-11-01-preview' = if (manageSql && !enablePrivateSql && manageNetworking) {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Store SQL connection info in Key Vault
// NOTE (Approach A - Managed Identity): We intentionally do NOT store the SQL connection string
// in Key Vault any longer to avoid partial @Microsoft.KeyVault expansion issues inside
// the application. If a secret is ever required again, re‑introduce this resource and
// wire it only if absolutely needed – but keep the app’s connection string pointing
// directly to the Managed Identity form in the Web App connectionStrings collection.
// (Previous secret resource removed: SqlConnectionString)

// Store API keys in Key Vault
// Secret naming uses the double-dash convention so the app bootstrap can transform
// SecretName with "--" into configuration key segment ':' (e.g. AlphaVantage--ApiKey -> AlphaVantage:ApiKey)
resource alphaVantageSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = if (!empty(alphaVantageApiKey)) {
  parent: keyVault
  name: 'AlphaVantage--ApiKey'
  properties: {
    value: alphaVantageApiKey
  }
}

resource twelveDataSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = if (!empty(twelveDataApiKey)) {
  parent: keyVault
  name: 'TwelveData--ApiKey'
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
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
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
          name: 'WEBSITES_CONTAINER_START_TIME_LIMIT'
          value: '600'
        }
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
        {
          name: 'ASPNETCORE_URLS'
          value: 'http://0.0.0.0:8080'
        }
        {
          name: 'WEBSITE_HEALTHCHECK_MAXPINGFAILURES'
          value: '20'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsUI.properties.ConnectionString
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: applicationInsightsUI.properties.InstrumentationKey
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
        // Leave API key app settings blank; runtime bootstrap loads real values from Key Vault secrets (double-dash names)
        {
          name: 'AlphaVantage__ApiKey'
          value: ''
        }
        {
          name: 'TwelveData__ApiKey'
          value: ''
        }
        // Provide Key Vault URI + UAMI ClientId so app can proactively bootstrap secrets
        {
          name: 'KeyVault__Uri'
          // Use environment suffix for sovereign cloud compatibility
          value: 'https://${keyVaultName}.${az.environment().suffixes.keyvaultDns}/'
        }
        {
          name: 'ManagedIdentity__ClientId'
          value: userAssignedIdentity.properties.clientId
        }
  // ConnectionStrings__DefaultConnection setting removed (Approach A – MI). The
  // platform connectionStrings section below already injects the full MI string.
        {
          name: 'APP_VERSION'
          value: empty(appVersion) ? '' : appVersion
        }
        // Configure UI to call the Azure API endpoint
        {
          name: 'StockApi__BaseUrl'
          value: 'https://${webApi.properties.defaultHostName}'
        }
        {
          name: 'StockApi__HttpBaseUrl'
          value: 'http://${webApi.properties.defaultHostName}'
        }
        // Data Protection configuration for persistent authentication keys
        {
          name: 'DataProtection__KeysPath'
          value: '/home/data-protection-keys'
        }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: 'Server=tcp:${sqlServerFqdnValue},1433;Database=${sqlDatabaseName};Authentication=Active Directory Managed Identity;User Id=${userAssignedIdentity.properties.clientId};Encrypt=true;TrustServerCertificate=false;Connection Timeout=60;Command Timeout=120;'
          type: 'SQLAzure'
        }
  ]
  healthCheckPath: '/health'
    }
    httpsOnly: true
  }
}

// API Web App
resource webApi 'Microsoft.Web/sites@2024-11-01' = {
  name: webApiName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
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
        ? 'DOCKER|${containerRegistry!.properties.loginServer}/${containerImageApi}'
        : 'DOCKER|${containerImageApi}'
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
          name: 'WEBSITES_CONTAINER_START_TIME_LIMIT'
          value: '600'
        }
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
        {
          name: 'ASPNETCORE_URLS'
          value: 'http://0.0.0.0:8080'
        }
        {
          name: 'WEBSITE_HEALTHCHECK_MAXPINGFAILURES'
          value: '20'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsAPI.properties.ConnectionString
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: applicationInsightsAPI.properties.InstrumentationKey
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
          value: ''
        }
        {
          name: 'TwelveData__ApiKey'
          value: ''
        }
        {
          name: 'KeyVault__Uri'
          value: 'https://${keyVaultName}.${az.environment().suffixes.keyvaultDns}/'
        }
        {
          name: 'ManagedIdentity__ClientId'
          value: userAssignedIdentity.properties.clientId
        }
  // (MI) Removed Key Vault reference for DefaultConnection – using direct
  // managed identity connection string via connectionStrings block.
        {
          name: 'APP_VERSION'
          value: empty(appVersion) ? '' : appVersion
        }
        // Data Protection configuration for persistent keys
        {
          name: 'DataProtection__KeysPath'
          value: '/home/data-protection-keys'
        }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: 'Server=tcp:${sqlServerFqdnValue},1433;Database=${sqlDatabaseName};Authentication=Active Directory Managed Identity;User Id=${userAssignedIdentity.properties.clientId};Encrypt=true;TrustServerCertificate=false;Connection Timeout=60;Command Timeout=120;'
          type: 'SQLAzure'
        }
  ]
  healthCheckPath: '/health'
    }
    httpsOnly: true
  }
}

// MCP Server Web App
resource webMcp 'Microsoft.Web/sites@2024-11-01' = {
  name: webMcpName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
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
        ? 'DOCKER|${containerRegistry!.properties.loginServer}/${containerImageMcp}'
        : 'DOCKER|${containerImageMcp}'
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
          name: 'WEBSITES_CONTAINER_START_TIME_LIMIT'
          value: '600'
        }
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
        {
          name: 'ASPNETCORE_URLS'
          value: 'http://0.0.0.0:8080'
        }
        {
          name: 'WEBSITE_HEALTHCHECK_MAXPINGFAILURES'
          value: '20'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsMCP.properties.ConnectionString
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: applicationInsightsMCP.properties.InstrumentationKey
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
          value: ''
        }
        {
          name: 'TwelveData__ApiKey'
          value: ''
        }
        {
          name: 'KeyVault__Uri'
          value: 'https://${keyVaultName}.${az.environment().suffixes.keyvaultDns}/'
        }
  // (MI) Removed Key Vault reference for DefaultConnection – using direct
  // managed identity connection string via connectionStrings block.
        {
          name: 'APP_VERSION'
          value: empty(appVersion) ? '' : appVersion
        }
        // Configure MCP server with stock API endpoint
        {
          name: 'STOCK_API_BASE_URL'
          value: 'https://${webApi.properties.defaultHostName}'
        }
        // Data Protection configuration for persistent keys
        {
          name: 'DataProtection__KeysPath'
          value: '/home/data-protection-keys'
        }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: 'Server=tcp:${sqlServerFqdnValue},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=true;TrustServerCertificate=false;Connection Timeout=60;Command Timeout=120;'
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
resource sqlPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-07-01' = if (manageSql && enablePrivateSql && manageNetworking) {
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
resource sqlPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-07-01' = if (manageSql && enablePrivateSql && manageNetworking) {
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

// Grant Key Vault access to the shared User-Assigned Managed Identity (covers both UI and API)
resource keyVaultAccessPolicyUami 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, userAssignedIdentity.id, 'Key Vault Secrets User')
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6'
    ) // Key Vault Secrets User
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Grant Key Vault access to MCP Web App
resource keyVaultAccessPolicyMcp 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, webMcp.id, 'Key Vault Secrets User')
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6'
    )
    principalId: webMcp.identity.principalId
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
// For user-assigned identity, principalId is provided by the UAMI itself
output webAppPrincipalId string = userAssignedIdentity.properties.principalId
output webApiName string = webApi.name
output webApiUrl string = 'https://${webApi.properties.defaultHostName}'
// For user-assigned identity, principalId is provided by the UAMI itself
output webApiPrincipalId string = userAssignedIdentity.properties.principalId
output webMcpName string = webMcp.name
output webMcpUrl string = 'https://${webMcp.properties.defaultHostName}'
output webMcpPrincipalId string = webMcp.identity.principalId
output userAssignedIdentityName string = userAssignedIdentity.name
output userAssignedIdentityPrincipalId string = userAssignedIdentity.properties.principalId
output userAssignedIdentityClientId string = userAssignedIdentity.properties.clientId
output sqlServerName string = sqlServerName
output sqlServerFqdn string = sqlServerFqdnValue
output sqlDatabaseName string = sqlDatabaseName
output containerRegistryName string = deployContainerRegistry ? containerRegistry!.name : 'not-deployed'
output containerRegistryLoginServer string = deployContainerRegistry
  ? containerRegistry!.properties.loginServer
  : 'not-deployed'
output keyVaultName string = keyVault.name
output applicationInsightsUiName string = applicationInsightsUI.name
output applicationInsightsApiName string = applicationInsightsAPI.name
output applicationInsightsMcpName string = applicationInsightsMCP.name
output applicationInsightsUiConnectionString string = applicationInsightsUI.properties.ConnectionString
output applicationInsightsApiConnectionString string = applicationInsightsAPI.properties.ConnectionString
output applicationInsightsMcpConnectionString string = applicationInsightsMCP.properties.ConnectionString
output vnetName string = (requireVNetIntegration && manageNetworking) ? vnetName : 'not-deployed'
output privateSqlEnabled bool = enablePrivateSql

output loadTestName string = loadTestResourceName
output loadTestResourceId string = resourceId('Microsoft.LoadTestService/loadTests', loadTestResourceName)
