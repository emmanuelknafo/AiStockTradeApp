#!/bin/bash

# Azure deployment script for AI Stock Trade MCP Server
# This script deploys the MCP server as a container app on Azure

set -e

# Configuration
RESOURCE_GROUP=${RESOURCE_GROUP:-"rg-aistocktrade-mcp"}
LOCATION=${LOCATION:-"East US"}
CONTAINER_APP_ENV=${CONTAINER_APP_ENV:-"env-aistocktrade-mcp"}
CONTAINER_APP_NAME=${CONTAINER_APP_NAME:-"aistocktrade-mcp-server"}
CONTAINER_REGISTRY=${CONTAINER_REGISTRY:-"craistocktrade.azurecr.io"}
IMAGE_NAME=${IMAGE_NAME:-"aistocktrade-mcp-server"}
IMAGE_TAG=${IMAGE_TAG:-"latest"}
STOCK_API_BASE_URL=${STOCK_API_BASE_URL:-"https://your-api.azurewebsites.net"}

echo "🚀 Deploying AI Stock Trade MCP Server to Azure Container Apps"
echo "Resource Group: $RESOURCE_GROUP"
echo "Location: $LOCATION"
echo "Container App Environment: $CONTAINER_APP_ENV"
echo "Container App Name: $CONTAINER_APP_NAME"
echo "Stock API URL: $STOCK_API_BASE_URL"

# Create resource group if it doesn't exist
echo "📦 Creating resource group..."
az group create \
    --name $RESOURCE_GROUP \
    --location "$LOCATION" \
    --output none

# Create container app environment if it doesn't exist
echo "🌐 Creating container app environment..."
az containerapp env create \
    --name $CONTAINER_APP_ENV \
    --resource-group $RESOURCE_GROUP \
    --location "$LOCATION" \
    --output none

# Deploy the container app
echo "🚢 Deploying container app..."
az containerapp create \
    --name $CONTAINER_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --environment $CONTAINER_APP_ENV \
    --image "$CONTAINER_REGISTRY/$IMAGE_NAME:$IMAGE_TAG" \
    --target-port 80 \
    --ingress external \
    --env-vars \
        "STOCK_API_BASE_URL=$STOCK_API_BASE_URL" \
        "ASPNETCORE_ENVIRONMENT=Production" \
    --cpu 0.25 \
    --memory 0.5Gi \
    --min-replicas 1 \
    --max-replicas 3 \
    --query "properties.configuration.ingress.fqdn" \
    --output tsv

echo "✅ Deployment completed successfully!"
echo "🌐 Your MCP server is available at the URL shown above"
echo ""
echo "📋 To configure in Claude Desktop, add this to your claude_desktop_config.json:"
echo "{"
echo "  \"mcpServers\": {"
echo "    \"aistocktrade\": {"
echo "      \"command\": \"dnx\","
echo "      \"args\": [\"--\", \"emmanuelknafo.AiStockTradeMcpServer\"],"
echo "      \"env\": {"
echo "        \"STOCK_API_BASE_URL\": \"$STOCK_API_BASE_URL\""
echo "      }"
echo "    }"
echo "  }"
echo "}"
