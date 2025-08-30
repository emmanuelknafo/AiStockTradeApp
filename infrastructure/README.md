# Infrastructure

This folder contains infrastructure-as-code for cloud deployments.

## Contents

- `main.bicep` - Primary Bicep template that provisions Azure resources for the application
- `parameters.dev.json` / `parameters.prod.json` - Environment-specific parameter files
- `APPLICATION_INSIGHTS_SEPARATION.md` - Documentation for the Application Insights separation changes

## Architecture

The infrastructure deploys:

### Core Resources
- **App Service Plan**: Shared hosting plan for both applications
- **Web App (UI)**: Main web application with MVC interface
- **Web App (API)**: REST API backend service
- **Azure SQL Database**: Primary data storage with optional private endpoint
- **Key Vault**: Secure storage for connection strings and API keys

### Monitoring & Observability
- **Application Insights (UI)**: Dedicated telemetry for web application
- **Application Insights (API)**: Dedicated telemetry for API service  
- **Log Analytics Workspace**: Shared workspace for unified logging and correlation

### Networking (Optional)
- **Virtual Network**: When private endpoints are enabled
- **Private Endpoints**: For SQL Database and Key Vault when private access is required
- **Private DNS Zones**: For private endpoint DNS resolution

### Container Registry (Dev Only)
- **Azure Container Registry**: For storing application container images

## Key Features

### Security
- **Azure AD Authentication**: Default authentication method for SQL Database
- **Managed Identity**: System-assigned identities for secure Key Vault access
- **Private Endpoints**: Optional private networking for SQL and Key Vault
- **HTTPS Only**: Enforced TLS encryption for all web applications

### Monitoring
- **Separated Telemetry**: Dedicated Application Insights instances for UI and API
- **Health Checks**: Configured endpoints for application health monitoring
- **Correlation**: Shared Log Analytics workspace enables cross-component analysis

### High Availability
- **Always On**: Configured for App Service applications
- **Auto-scaling**: App Service Plan supports scaling based on demand
- **Geo-redundant Storage**: SQL Database includes backup and recovery

## Deployment

Use the Azure Developer CLI (`azd`) or `az deployment` to deploy this Bicep template. Review parameter files before deploying.

### Example Deployment
```bash
# Deploy to development environment
az deployment group create \
  --resource-group rg-aistock-dev-002 \
  --template-file main.bicep \
  --parameters @parameters.dev.json

# Deploy to production environment  
az deployment group create \
  --resource-group rg-aistock-prod-002 \
  --template-file main.bicep \
  --parameters @parameters.prod.json
```

## Configuration

### Required Parameters
- `appName`: Application name (e.g., "aistock")
- `environment`: Environment name ("dev" or "prod")
- `containerRegistryName`: Name for the container registry
- `alphaVantageApiKey`: API key for Alpha Vantage stock data service
- `twelveDataApiKey`: API key for Twelve Data stock service

### Optional Parameters
- `appServicePlanSku`: App Service Plan size (default: "P0v3")
- `instanceNumber`: Resource differentiation number (default: "002")
- `enablePrivateSql`: Enable private endpoint for SQL Database (default: true)
- `enablePrivateKeyVault`: Enable private endpoint for Key Vault (default: true)
- `deployContainerRegistry`: Deploy container registry (default: true for dev)

## Outputs

The template provides the following outputs for integration with CI/CD pipelines:

### Application Endpoints
- `webAppUrl`: URL of the UI web application
- `webApiUrl`: URL of the API service

### Monitoring Resources
- `applicationInsightsUiName`: Name of the UI Application Insights instance
- `applicationInsightsApiName`: Name of the API Application Insights instance
- `applicationInsightsUiConnectionString`: Connection string for UI telemetry
- `applicationInsightsApiConnectionString`: Connection string for API telemetry

### Infrastructure Resources
- `sqlServerName`: Name of the Azure SQL Server
- `keyVaultName`: Name of the Key Vault
- `containerRegistryLoginServer`: Login server for the container registry
