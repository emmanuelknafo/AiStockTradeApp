# CI/CD Setup Guide

This guide walks you through setting up the complete CI/CD pipeline for the AI Stock Trade App using GitHub Actions, Azure Container Registry, and Azure Web App for Containers.

## Prerequisites

- Azure subscription
- GitHub repository
- Azure CLI installed locally
- Docker installed locally (for testing)

## Azure Setup

### 1. Create Azure Resources

First, create the necessary Azure resource groups:

```bash
# Login to Azure
az login

# Set your subscription
az account set --subscription "YOUR_SUBSCRIPTION_ID"

# Create resource groups
az group create --name "ai-stock-tracker-dev-rg" --location "East US"
az group create --name "ai-stock-tracker-prod-rg" --location "East US"
```

### 2. Create Service Principal

Create a service principal for GitHub Actions:

```bash
az ad sp create-for-rbac --name "ai-stock-tracker-github-actions" \
  --role contributor \
  --scopes /subscriptions/YOUR_SUBSCRIPTION_ID \
  --sdk-auth
```

Save the JSON output - you'll need it for GitHub secrets.

### 3. Create Azure Container Registry

```bash
# Create ACR for development
az acr create --name "aistocktrackerdev" \
  --resource-group "ai-stock-tracker-dev-rg" \
  --sku Basic \
  --admin-enabled true

# Create ACR for production  
az acr create --name "aistocktrackerprod" \
  --resource-group "ai-stock-tracker-prod-rg" \
  --sku Basic \
  --admin-enabled true

# Get ACR credentials
az acr credential show --name "aistocktrackerdev"
az acr credential show --name "aistocktrackerprod"
```

## GitHub Setup

### 1. Repository Variables

Go to your GitHub repository ? Settings ? Secrets and variables ? Actions ? Variables tab and add:

- `AZURE_SUBSCRIPTION_ID`: Your Azure subscription ID
- `AZURE_RESOURCE_GROUP_DEV`: `ai-stock-tracker-dev-rg`
- `AZURE_RESOURCE_GROUP_PROD`: `ai-stock-tracker-prod-rg`
- `CONTAINER_REGISTRY_NAME`: `aistocktrackerdev` (for dev) or `aistocktrackerprod` (for prod)

### 2. Repository Secrets

Go to your GitHub repository ? Settings ? Secrets and variables ? Actions ? Secrets tab and add:

- `AZURE_CREDENTIALS`: The JSON output from the service principal creation
- `REGISTRY_USERNAME`: ACR username from step 3 above
- `REGISTRY_PASSWORD`: ACR password from step 3 above
- `ALPHA_VANTAGE_API_KEY`: Your Alpha Vantage API key
- `TWELVE_DATA_API_KEY`: Your Twelve Data API key

### 3. Environment Configuration

Create two environments in your GitHub repository (Settings ? Environments):

#### Development Environment
- Name: `development`
- No protection rules needed for development

#### Production Environment  
- Name: `production`
- Add protection rules:
  - Required reviewers (recommended)
  - Wait timer (optional)
  - Restrict to main branch

## Deployment Process

### Automatic Deployments

The pipeline automatically deploys:
- **Development**: When code is pushed to the `develop` branch
- **Production**: When code is pushed to the `main` branch

### Manual Deployments

You can trigger manual deployments:
1. Go to Actions tab in your GitHub repository
2. Select "CI/CD Pipeline" workflow
3. Click "Run workflow"
4. Choose the environment (dev/prod)

## Infrastructure Components

The Bicep templates create:

### Azure Container Registry
- Stores your container images
- Automatically configured for Web App access

### Azure Web App for Containers
- Hosts your containerized application
- Configured with system-assigned managed identity
- Auto-scaling enabled

### Azure Key Vault
- Securely stores API keys
- Accessed by Web App using managed identity

### Application Insights (Overview)
- Application performance monitoring
- Connected to Log Analytics workspace

### Log Analytics Workspace
- Centralized logging for all components

### (New) Virtual Network & Private Endpoint (Optional but Enabled by Default)

Network resources (created when `enablePrivateSql=true`):

  - App Integration subnet (delegated to Microsoft.Web) for Web App regional VNet integration
  - Private Endpoints subnet (network policies disabled) hosting the Azure SQL private endpoint
- Private DNS Zone for Azure SQL (privatelink.[platform suffix]) linked to the VNet
Effects when enabled:


Key new Bicep parameters:

| `enablePrivateSql` | `true` | Enables VNet, private endpoint, and disables public network access |
| `vnetAddressSpace` | `10.20.0.0/16` | CIDR for created VNet |
| `appIntegrationSubnetPrefix` | `10.20.1.0/27` | Subnet for Web App VNet integration (must be /27 or larger) |
Separate non-overlapping address ranges are used per environment / pipeline (see workflow & pipeline definitions) to avoid conflicts.

## Monitoring and Troubleshooting
* Navigate to Azure Portal → Application Insights
* Monitor application performance, errors, and usage

# View Web App logs
az webapp log tail --name "ai-stock-tracker-dev-webapp" \
  --resource-group "ai-stock-tracker-dev-rg"

* Application exposes `/health` endpoint
* Automatically monitored by Azure Web App
* Used in GitHub Actions for deployment verification

## Cost Optimization

### Development Environment

* Uses B1 App Service Plan (Basic tier)
* Basic Container Registry
* Minimal Log Analytics retention (30 days)

### Production Environment

* Uses P1V3 App Service Plan (Premium tier)
* Can be scaled based on requirements
* Consider upgrading Container Registry to Standard/Premium for geo-replication

## Security Features

### Container Security

* Trivy vulnerability scanning in pull requests
* Results uploaded to GitHub Security tab
* Non-root user in container

### Azure Security
- HTTPS only enforcement
- Managed identity for Key Vault access
- TLS 1.2 minimum
- RBAC for resource access
- Azure SQL public access disabled via private endpoint (when `enablePrivateSql=true`)
- Private DNS zone ensures name resolution inside VNet only
- Managed identity + Azure AD only auth supported (`enableAzureAdOnlyAuth=true`)

### Application Security
- API keys stored in Key Vault
- Environment-specific configuration
- Health check endpoints

## Scaling Considerations

### Horizontal Scaling
- Azure Web App supports auto-scaling rules
- Can scale based on CPU, memory, or custom metrics

### Container Registry
- Basic tier suitable for development
- Consider Standard/Premium for production workloads

## Backup and Disaster Recovery

### Application Data
- Watchlist data is session-based (ephemeral)
- Consider implementing persistent storage for production

### Infrastructure
- Bicep templates serve as infrastructure as code
- Can recreate infrastructure from templates
- Container images stored in ACR

## Troubleshooting Common Issues

### Build Failures
1. Check .NET version compatibility
2. Verify all dependencies are restored
3. Review build logs in GitHub Actions

### Deployment Failures
1. Verify Azure credentials and permissions
2. Check resource group existence
3. Review Bicep template parameters
4. If SQL connectivity errors show `Deny Public Network Access is set to Yes`, ensure:
  - You are connecting from inside the VNet (e.g., from Web App)
  - The private endpoint finished provisioning (check `provisioningState`)
  - DNS resolves `<sql-server>.database.windows.net` to a private IP (use `nslookup` inside an Azure resource in the VNet)

### Runtime Issues
1. Check Application Insights for errors
2. Review Web App logs
3. Verify API key configuration in Key Vault
4. For database login failures with AAD only auth: confirm managed identity was added as server admin and database user was created (see post-deploy notes in `main.bicep`).

### Container Issues
1. Test container locally with Docker
2. Check container registry connectivity
3. Verify Web App container settings

## Additional Configuration

### Custom Domain
```bash
# Add custom domain to Web App
az webapp config hostname add \
  --webapp-name "ai-stock-tracker-prod-webapp" \
  --resource-group "ai-stock-tracker-prod-rg" \
  --hostname "yourdomain.com"
```

### SSL Certificate
```bash
# Create managed certificate
az webapp config ssl create \
  --name "ai-stock-tracker-prod-webapp" \
  --resource-group "ai-stock-tracker-prod-rg" \
  --hostname "yourdomain.com"
```

### Auto-scaling Rules
```bash
# Create auto-scale rule
az monitor autoscale create \
  --name "ai-stock-tracker-autoscale" \
  --resource-group "ai-stock-tracker-prod-rg" \
  --resource "/subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/ai-stock-tracker-prod-rg/providers/Microsoft.Web/serverFarms/ai-stock-tracker-prod-asp" \
  --min-count 1 \
  --max-count 3 \
  --count 1
```