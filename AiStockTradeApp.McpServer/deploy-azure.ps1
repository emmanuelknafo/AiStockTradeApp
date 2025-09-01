# Azure deployment script for AI Stock Trade MCP Server
# This script deploys the MCP server as a container app on Azure

param(
    [string]$ResourceGroup = "rg-aistocktrade-mcp",
    [string]$Location = "East US",
    [string]$ContainerAppEnv = "env-aistocktrade-mcp",
    [string]$ContainerAppName = "aistocktrade-mcp-server",
    [string]$ContainerRegistry = "craistocktrade.azurecr.io",
    [string]$ImageName = "aistocktrade-mcp-server",
    [string]$ImageTag = "latest",
    [string]$StockApiBaseUrl = "https://your-api.azurewebsites.net"
)

Write-Host "🚀 Deploying AI Stock Trade MCP Server to Azure Container Apps" -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Location: $Location"
Write-Host "Container App Environment: $ContainerAppEnv"
Write-Host "Container App Name: $ContainerAppName"
Write-Host "Stock API URL: $StockApiBaseUrl"

try {
    # Create resource group if it doesn't exist
    Write-Host "📦 Creating resource group..." -ForegroundColor Yellow
    az group create `
        --name $ResourceGroup `
        --location $Location `
        --output none

    # Create container app environment if it doesn't exist
    Write-Host "🌐 Creating container app environment..." -ForegroundColor Yellow
    az containerapp env create `
        --name $ContainerAppEnv `
        --resource-group $ResourceGroup `
        --location $Location `
        --output none

    # Deploy the container app
    Write-Host "🚢 Deploying container app..." -ForegroundColor Yellow
    $appUrl = az containerapp create `
        --name $ContainerAppName `
        --resource-group $ResourceGroup `
        --environment $ContainerAppEnv `
        --image "$ContainerRegistry/$ImageName`:$ImageTag" `
        --target-port 80 `
        --ingress external `
        --env-vars `
            "STOCK_API_BASE_URL=$StockApiBaseUrl" `
            "ASPNETCORE_ENVIRONMENT=Production" `
        --cpu 0.25 `
        --memory 0.5Gi `
        --min-replicas 1 `
        --max-replicas 3 `
        --query "properties.configuration.ingress.fqdn" `
        --output tsv

    Write-Host "✅ Deployment completed successfully!" -ForegroundColor Green
    Write-Host "🌐 Your MCP server is available at: https://$appUrl" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "📋 To configure in Claude Desktop, add this to your claude_desktop_config.json:" -ForegroundColor Yellow
    
    $config = @"
{
  "mcpServers": {
    "aistocktrade": {
      "command": "dnx",
      "args": ["--", "emmanuelknafo.AiStockTradeMcpServer"],
      "env": {
        "STOCK_API_BASE_URL": "$StockApiBaseUrl"
      }
    }
  }
}
"@
    
    Write-Host $config -ForegroundColor White
}
catch {
    Write-Error "❌ Deployment failed: $($_.Exception.Message)"
    exit 1
}
