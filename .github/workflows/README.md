# GitHub Actions Workflows

This directory contains GitHub Actions workflows for automated building, testing, and deployment of the AI Stock Trade App.

## 📦 MCP Server NuGet Publishing

### `publish-mcpserver.yml`

Automatically publishes the MCP Server to NuGet.org when non-markdown files are modified in the `AiStockTradeApp.McpServer` project.

#### 🚀 **Trigger Conditions**
- **Automatic**: Pushes to `main` branch with changes to `AiStockTradeApp.McpServer/**` (excluding `.md` files)
- **Manual**: Can be triggered manually via GitHub Actions UI

#### 🔧 **Prerequisites**

1. **NuGet API Key**: Add your NuGet.org API key as a repository secret
   ```
   Repository Settings → Secrets and variables → Actions → New repository secret
   Name: NUGET_API_KEY
   Value: Your NuGet.org API key
   ```

2. **NuGet.org Account**: Ensure you have a NuGet.org account and API key with push permissions

#### 📋 **What the Workflow Does**

1. **Version Check**: Verifies if the current version already exists on NuGet.org
2. **Build & Test**: Compiles the project and runs tests (if available)
3. **Package Validation**: Ensures `.mcp/server.json` and `README.md` are included
4. **NuGet Publishing**: Publishes to NuGet.org if version doesn't exist
5. **GitHub Release**: Creates a tagged release with installation instructions
6. **Summary**: Provides detailed feedback on the workflow results

#### 🔄 **Version Management**

The workflow automatically extracts version information from:
- `<PackageVersion>` in `AiStockTradeApp.McpServer.csproj`
- `version` in `.mcp/server.json`

**To publish a new version:**
1. Update `<PackageVersion>` in the `.csproj` file
2. Update `version` in `.mcp/server.json` 
3. Commit and push changes to `main` branch

#### 📈 **Workflow Outputs**

- **Success**: Package published to NuGet.org with GitHub release
- **Skip**: If version already exists, workflow skips publishing
- **Failure**: Detailed error information in workflow logs

#### 🔍 **Package Validation**

The workflow validates that the package includes:
- ✅ `.mcp/server.json` - MCP server configuration
- ✅ `README.md` - Package documentation  
- ✅ `PackageType: McpServer` - Proper NuGet package type
- ✅ `PackAsTool: true` - Configured as .NET tool

#### 🎯 **Usage After Publishing**

Once published, users can install and configure the MCP server:

```json
{
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
        "STOCK_API_BASE_URL": "https://your-api-url.com"
      }
    }
  }
}
```

#### 🛡️ **Security Considerations**

- API keys are stored as GitHub secrets
- Package validation prevents malformed packages
- Version checking prevents accidental overwrites
- Pre-release detection for beta/alpha versions

#### 🔧 **Troubleshooting**

**Common Issues:**
- **Missing API Key**: Ensure `NUGET_API_KEY` secret is configured
- **Version Exists**: Update version numbers in both `.csproj` and `.mcp/server.json`
- **Package Validation Fails**: Check that required files are included in the package
- **Build Failures**: Review build logs for compilation errors

**Debug Steps:**
1. Check workflow logs in GitHub Actions tab
2. Verify version numbers are unique
3. Ensure all dependencies are properly referenced
4. Validate `.mcp/server.json` format

---

## 🔄 Future Workflows

Additional workflows can be added for:
- **API deployment** to Azure App Service
- **UI deployment** to Azure Static Web Apps  
- **Docker image publishing** to Azure Container Registry
- **Integration testing** across all projects
- **Security scanning** and vulnerability assessment

---

## 📝 Workflow Customization

Edit the YAML files to customize CI triggers and secrets. All workflows follow GitHub Actions best practices for security, caching, and performance.
