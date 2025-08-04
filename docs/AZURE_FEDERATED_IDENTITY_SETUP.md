# Azure Federated Identity Setup Guide

This guide explains how to set up Azure federated identity (OIDC) authentication for the GitHub Actions workflows instead of using stored credentials.

## Why Federated Identity?

Federated identity provides several advantages over storing service principal credentials:
- **More secure**: No long-lived secrets stored in GitHub
- **Better audit trail**: Clear identity provider relationship
- **Automatic token rotation**: Azure handles token lifecycle
- **Principle of least privilege**: Can be scoped to specific repositories and branches

## Setup Steps

### 1. Create an Azure App Registration

1. Go to [Azure Portal](https://portal.azure.com) → Azure Active Directory → App registrations
2. Click "New registration"
3. Fill in the details:
   - **Name**: `ai-stock-tracker-github-actions`
   - **Supported account types**: "Accounts in this organizational directory only"
   - **Redirect URI**: Leave empty
4. Click "Register"
5. Note down the **Application (client) ID** and **Directory (tenant) ID**

### 2. Create Federated Credentials

1. In your app registration, go to "Certificates & secrets"
2. Click on "Federated credentials" tab
3. Click "Add credential"
4. Select "GitHub Actions deploying Azure resources"
5. Fill in the details:
   - **Organization**: `emmanuelknafo`
   - **Repository**: `ai-stock-trade-app`
   - **Entity type**: `Branch`
   - **Branch**: `main`
   - **Name**: `ai-stock-tracker-main-branch`
6. Click "Add"

### 3. Assign Azure Permissions

The app registration needs permissions to create and manage Azure resources:

```bash
# Get your subscription ID
az account show --query id -o tsv

# Get the app registration object ID
az ad app show --id <APPLICATION_CLIENT_ID> --query id -o tsv

# Assign Contributor role to the app registration
az role assignment create \
  --assignee <APPLICATION_CLIENT_ID> \
  --role "Contributor" \
  --scope "/subscriptions/<SUBSCRIPTION_ID>"
```

Alternative: Use Azure Portal
1. Go to **Subscriptions** → Your subscription → **Access control (IAM)**
2. Click "Add" → "Add role assignment"
3. Select **Contributor** role
4. In "Members", select "User, group, or service principal"
5. Search for your app registration name: `ai-stock-tracker-github-actions`
6. Click "Review + assign"

### 4. Configure GitHub Repository Variables

In your GitHub repository, go to **Settings** → **Secrets and variables** → **Actions**:

#### Repository Variables (Variables tab):
- `AZURE_CLIENT_ID`: The Application (client) ID from step 1
- `AZURE_TENANT_ID`: The Directory (tenant) ID from step 1  
- `AZURE_SUBSCRIPTION_ID`: Your Azure subscription ID

#### Repository Secrets (Secrets tab):
- `ALPHA_VANTAGE_API_KEY`: Your Alpha Vantage API key
- `TWELVE_DATA_API_KEY`: Your Twelve Data API key (if used)

### 5. Test the Setup

1. Go to your repository → **Actions**
2. Find the "Infrastructure Deployment" workflow
3. Click "Run workflow"
4. Select environment (dev/prod) and run
5. Verify that the Azure login step succeeds

## Environment-Specific Setup (Optional)

For production environments, you might want separate app registrations:

### Development Environment
- **App Registration**: `ai-stock-tracker-github-dev`
- **Federated Credential**: Branch = `main` or `develop`
- **Permissions**: Contributor on development subscription/resource group

### Production Environment  
- **App Registration**: `ai-stock-tracker-github-prod`
- **Federated Credential**: Branch = `main` and Environment = `production`
- **Permissions**: Contributor on production subscription/resource group

## Troubleshooting

### Common Issues

1. **"AADSTS70021: No matching federated identity record found"**
   - Check that the repository, branch, and organization match exactly
   - Ensure the federated credential is created correctly

2. **"Insufficient privileges to complete the operation"**
   - Verify the app registration has Contributor role on the subscription
   - Check that the scope is correct (subscription or resource group level)

3. **"Repository variable not found"**
   - Ensure variables are set in the repository settings under Variables (not Secrets)
   - Variable names are case-sensitive

### Verification Commands

```bash
# Test Azure CLI login (local testing)
az login --service-principal \
  --username <AZURE_CLIENT_ID> \
  --tenant <AZURE_TENANT_ID> \
  --federated-token <GITHUB_TOKEN>

# Check role assignments
az role assignment list --assignee <AZURE_CLIENT_ID> --output table

# Verify app registration
az ad app show --id <AZURE_CLIENT_ID> --query "{displayName:displayName, appId:appId}"
```

## Security Best Practices

1. **Principle of Least Privilege**: Only assign the minimum required permissions
2. **Environment Separation**: Use different app registrations for dev/prod
3. **Branch Protection**: Limit federated credentials to protected branches
4. **Regular Auditing**: Review app registrations and role assignments periodically
5. **Environment Gates**: Use GitHub environment protection rules for production

## Migration from Service Principal Secrets

If migrating from the old credential-based approach:

1. Set up federated identity as described above
2. Update the workflow file to use the new authentication method
3. Remove the old `AZURE_CREDENTIALS` secret from GitHub
4. Test the workflow thoroughly
5. Consider revoking the old service principal if no longer needed

## References

- [Azure Workload Identity Federation](https://docs.microsoft.com/en-us/azure/active-directory/develop/workload-identity-federation)
- [GitHub Actions OIDC with Azure](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/configuring-openid-connect-in-azure)
- [Azure CLI Login Action](https://github.com/Azure/login)
