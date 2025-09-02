# MCP Server NuGet Publishing Setup

## 🚀 Quick Setup Guide

This document provides step-by-step instructions for setting up automatic NuGet publishing for the AI Stock Trade MCP Server.

## 📋 Prerequisites

1. **NuGet.org Account**: You need a NuGet.org account
2. **API Key**: Generate an API key from your NuGet.org account
3. **Repository Access**: Admin access to configure GitHub secrets

## 🔧 Setup Steps

### Step 1: Get NuGet API Key

1. Go to [NuGet.org](https://www.nuget.org/)
2. Sign in to your account
3. Click on your username → "API Keys"
4. Click "Create" to generate a new API key
5. Set the key name (e.g., "AiStockTradeApp-GitHub-Actions")
6. Set "Glob Pattern" to match your package: `emmanuelknafo.AiStockTradeMcpServer`
7. Select "Push" permissions
8. Set expiration (recommended: 1 year)
9. Copy the generated API key

### Step 2: Configure GitHub Secret

1. Go to your GitHub repository
2. Navigate to **Settings** → **Secrets and variables** → **Actions**
3. Click "New repository secret"
4. Name: `NUGET_API_KEY`
5. Value: Paste your NuGet.org API key
6. Click "Add secret"

### Step 3: Verify Project Configuration

The MCP Server project should already be configured with:

```xml
<!-- In AiStockTradeApp.McpServer.csproj -->
<PackAsTool>true</PackAsTool>
<PackageType>McpServer</PackageType>
<PackageId>emmanuelknafo.AiStockTradeMcpServer</PackageId>
<PackageVersion>1.0.0-beta</PackageVersion>
```

And include the MCP server configuration:

```json
// In .mcp/server.json
{
  "description": "MCP Server for AI-powered stock trading operations",
  "name": "io.github.emmanuelknafo/AiStockTradeApp",
  "packages": [
    {
      "registry_name": "nuget",
      "name": "emmanuelknafo.AiStockTradeMcpServer",
      "version": "1.0.0-beta"
    }
  ]
}
```

## 🎯 How It Works

1. **Automatic Trigger**: Workflow runs when you push changes to any non-`.md` file in `AiStockTradeApp.McpServer/`
2. **Version Check**: Checks if the current version already exists on NuGet.org
3. **Build & Pack**: Compiles and packages the MCP server
4. **Validation**: Ensures required files (`.mcp/server.json`, `README.md`) are included
5. **Publish**: Uploads to NuGet.org if version is new
6. **Release**: Creates a GitHub release with installation instructions

## 🔄 Publishing a New Version

1. **Update Version**: Edit `<PackageVersion>` in the `.csproj` file
2. **Update MCP Config**: Update `version` in `.mcp/server.json`
3. **Commit & Push**: Push changes to the `main` branch
4. **Automatic Publishing**: GitHub Actions will handle the rest

Example version update:

```xml
<!-- Before -->
<PackageVersion>1.0.0-beta</PackageVersion>

<!-- After -->
<PackageVersion>1.0.1-beta</PackageVersion>
```

```json
// Update .mcp/server.json accordingly
{
  "version_detail": {
    "version": "1.0.1-beta"
  }
}
```

## 📦 Using the Published Package

Once published, users can install the MCP server in VS Code or Visual Studio:

### VS Code Configuration (`.vscode/mcp.json`)

```json
{
  "inputs": [
    {
      "type": "promptString",
      "id": "stock_api_base_url",
      "description": "Base URL for the Stock Trading API",
      "password": false
    }
  ],
  "servers": {
    "emmanuelknafo.AiStockTradeMcpServer": {
      "type": "stdio",
      "command": "dnx",
      "args": [
        "emmanuelknafo.AiStockTradeMcpServer",
        "--version",
        "1.0.0-beta",
        "--yes"
      ],
      "env": {
        "STOCK_API_BASE_URL": "${input:stock_api_base_url}"
      }
    }
  }
}
```

### Visual Studio Configuration (`.mcp.json`)

Same configuration as above, just place in the solution root.

## 🔍 Monitoring & Troubleshooting

### Check Workflow Status

1. Go to **Actions** tab in your GitHub repository
2. Look for "Publish MCP Server to NuGet" workflow
3. Click on any run to see detailed logs

### Common Issues

- **Secret not configured**: Ensure `NUGET_API_KEY` is set in repository secrets
- **Version conflict**: Update version numbers if package already exists
- **Build failures**: Check compilation errors in the workflow logs
- **Package validation**: Ensure `.mcp/server.json` format is valid

### Manual Testing

You can manually trigger the workflow:

1. Go to **Actions** tab
2. Click "Publish MCP Server to NuGet"
3. Click "Run workflow"
4. Select branch and click "Run workflow"

## 🎉 Success Indicators

When everything works correctly, you'll see:

1. ✅ Workflow completes successfully
2. 📦 Package appears on [NuGet.org](https://www.nuget.org/packages/emmanuelknafo.AiStockTradeMcpServer)
3. 🏷️ GitHub release is created with installation instructions
4. 📊 Package is discoverable with the "MCP Server" filter on NuGet.org

---

**Next Steps**: After successful publishing, share the NuGet package link with the community and update documentation with installation instructions!
