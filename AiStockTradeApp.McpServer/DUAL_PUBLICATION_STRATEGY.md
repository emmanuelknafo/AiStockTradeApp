# MCP Server Dual Publication Strategy

This document explains how the MCP Server package is configured for dual publication to different package repositories.

## 📦 Publication Targets

### 1. **Azure DevOps Artifacts** (Private Feed)
- **Triggered by**: Azure DevOps Pipeline (`.azuredevops/pipelines/publish-mcpserver.yml`)
- **Package Name**: `MngEnvMCAP675646.AiStockTradeApp.McpServer`
- **Target**: Private organizational feed for internal use
- **Configuration**: `.mcp/server-azuredevops.json`

### 2. **NuGet.org** (Public Feed)
- **Triggered by**: GitHub Actions (`.github/workflows/publish-mcpserver.yml`)
- **Package Name**: `AiStockTradeApp.McpServer`
- **Target**: Public NuGet Gallery for community use
- **Configuration**: `.mcp/server-nuget.json`

## 🏗️ Build Configuration

The project uses conditional MSBuild properties to automatically select the correct configuration:

```xml
<!-- Conditional PackageId based on build environment -->
<PackageId Condition="'$(PublishTarget)' == 'AzureDevOps'">MngEnvMCAP675646.AiStockTradeApp.McpServer</PackageId>
<PackageId Condition="'$(PublishTarget)' != 'AzureDevOps'">AiStockTradeApp.McpServer</PackageId>

<!-- Conditionally include server.json based on build target -->
<None Include=".mcp\server-azuredevops.json" Pack="true" PackagePath="/.mcp/server.json" Condition="'$(PublishTarget)' == 'AzureDevOps'" />
<None Include=".mcp\server-nuget.json" Pack="true" PackagePath="/.mcp/server.json" Condition="'$(PublishTarget)' != 'AzureDevOps'" />
```

## 🚀 Pipeline Configuration

### Azure DevOps Pipeline
Sets `PublishTarget=AzureDevOps` during build and pack:
```yaml
arguments: "--configuration $(BUILD_CONFIGURATION) --no-restore -p:PublishTarget=AzureDevOps"
buildProperties: "PublishTarget=AzureDevOps"
```

### GitHub Actions Workflow
Sets `PublishTarget=NuGet` during build and pack:
```yaml
run: dotnet build --configuration Release --no-restore -p:PublishTarget=NuGet
run: dotnet pack --configuration Release --no-build --output ./nupkg -p:PublishTarget=NuGet
```

## 📄 Configuration Files

### `.mcp/server-azuredevops.json` (Private Package)
```json
{
  "name": "io.github.emmanuelknafo/AiStockTradeApp",
  "packages": [
    {
      "registry_name": "nuget",
      "name": "MngEnvMCAP675646.AiStockTradeApp.McpServer",
      "version": "1.0.0-beta"
    }
  ]
}
```

### `.mcp/server-nuget.json` (Public Package)
```json
{
  "name": "io.github.emmanuelknafo/AiStockTradeApp",
  "packages": [
    {
      "registry_name": "nuget",
      "name": "AiStockTradeApp.McpServer",
      "version": "1.0.0-beta"
    }
  ]
}
```

## 🎯 Installation Instructions

### For Internal Users (Azure DevOps Artifacts)
```bash
# Add the private feed
dotnet nuget add source "https://pkgs.dev.azure.com/MngEnvMCAP675646/AiStockTradeApp/_packaging/AiStockTradeApp-Packages/nuget/v3/index.json" --name "AiStockTradeApp-Packages"

# Install the package
dotnet add package MngEnvMCAP675646.AiStockTradeApp.McpServer --version 1.0.0-beta --source "AiStockTradeApp-Packages"
```

### For Public Users (NuGet.org)
```bash
# Install directly from NuGet.org
dotnet add package AiStockTradeApp.McpServer --version 1.0.0-beta
```

## 🔄 Version Management

- Both packages **share the same version number** from the `.csproj` file
- **Azure DevOps Pipeline** handles version auto-increment for internal releases
- **GitHub Actions** handles version checking and publishing to NuGet.org
- Version changes are **automatically synchronized** across both publication targets

## 🛠️ Maintenance

### To Update Version
1. Update `<PackageVersion>` in `AiStockTradeApp.McpServer.csproj`
2. Update `version` in both `.mcp/server-azuredevops.json` and `.mcp/server-nuget.json`
3. Commit changes - pipelines will automatically publish to both targets

### To Add New Configuration
1. Modify the respective `.mcp/server-*.json` files
2. Test locally with different `PublishTarget` values:
   ```bash
   # Test Azure DevOps configuration
   dotnet pack -p:PublishTarget=AzureDevOps
   
   # Test NuGet configuration  
   dotnet pack -p:PublishTarget=NuGet
   ```

## 🔍 Troubleshooting

### Package Name Conflicts
- **Azure DevOps**: Uses organization-prefixed name to avoid upstream conflicts
- **NuGet.org**: Uses clean name for public consumption

### Build Verification
Use these commands to verify the correct configuration is being packaged:
```bash
# Extract and examine package contents
unzip -l package.nupkg | grep server.json

# Verify PackageId in the .nuspec
unzip -p package.nupkg *.nuspec | grep PackageId
```

This dual-publication strategy ensures that:
- ✅ Internal users get stable packages from private feeds
- ✅ Public users get clean packages from NuGet.org
- ✅ No naming conflicts between publication targets
- ✅ Automated version management across both platforms
