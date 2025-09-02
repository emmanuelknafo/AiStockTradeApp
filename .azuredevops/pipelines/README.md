# Azure DevOps Pipelines

This folder contains Azure DevOps pipeline definitions used for CI/CD and other automation.

If you are using Azure DevOps, review and enable these pipelines in your organization and adjust variables for your subscription and resource names.

## Available Pipelines

### Main CI/CD Pipeline

- **ci-cd.yml** - Main continuous integration and deployment pipeline for the web application
- **infrastructure.yml** - Infrastructure deployment pipeline using Bicep templates

### Specialized Pipelines

- **publish-mcpserver.yml** - Publishes the MCP Server package to Azure DevOps Artifacts feed
- **cli-ci.yml** - CLI tool continuous integration
- **load-tests.yml** - Load testing pipeline
- **alt-load-tests.yml** - Alternative load testing configuration
- **generate-changelog.yml** - Automated changelog generation

### Configuration Files

- **alt-config.yml** - Alternative pipeline configuration
- **checkout-fallback-example.yml** - Example of checkout fallback strategies

## MCP Server Publishing

The `publish-mcpserver.yml` pipeline publishes the MCP Server NuGet package to Azure DevOps Artifacts instead of public NuGet.org.

### Prerequisites for MCP Server Pipeline

1. **Azure DevOps Artifacts Feed**: Create a NuGet feed in your Azure DevOps project
2. **Variable Group**: Update the `AiStockTradeApp` variable group with:
   - `AZURE_DEVOPS_FEED_NAME`: Name of your Azure DevOps Artifacts feed
   - `AZURE_DEVOPS_ORGANIZATION`: Your Azure DevOps organization name
   - `AZURE_DEVOPS_PROJECT`: Your Azure DevOps project name

### Usage

The pipeline triggers automatically on changes to:

- `AiStockTradeApp.McpServer/**` (excluding markdown files)
- The pipeline definition itself

It can also be triggered manually with an option to force publish even if the version already exists.

### Features

- **Automatic version checking**: Checks if version already exists in Azure DevOps Artifacts
- **Auto-increment versioning**: Automatically bumps version if current version exists
- **Package validation**: Ensures required files are included in the package
- **Git tagging**: Creates Git tags for published versions
- **Package verification**: Tests package installation from the artifacts feed
