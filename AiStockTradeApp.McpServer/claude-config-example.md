# Claude Desktop MCP Configuration Example

## For Local Development

Add this configuration to your Claude Desktop `claude_desktop_config.json` file:

```json
{
  "inputs": [
    {
      "type": "promptString",
      "id": "stock_api_base_url",
      "description": "Base URL for the Stock Trading API (e.g., http://localhost:5000)",
      "password": false
    }
  ],
  "mcpServers": {
    "aistocktrade": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\src\\GitHub\\emmanuelknafo\\AiStockTradeApp\\AiStockTradeApp.McpServer"
      ],
      "env": {
        "STOCK_API_BASE_URL": "${input:stock_api_base_url}"
      }
    }
  }
}
```

## For Published NuGet Package

Once published to NuGet.org, use this configuration with proper input handling:

```json
{
  "inputs": [
    {
      "type": "promptString",
      "id": "stock_api_base_url",
      "description": "Base URL for the Stock Trading API (e.g., http://localhost:5000 or https://your-api.azurewebsites.net)",
      "password": false
    }
  ],
  "mcpServers": {
    "aistocktrade": {
      "command": "dnx",
      "args": ["--", "emmanuelknafo.AiStockTradeMcpServer"],
      "env": {
        "STOCK_API_BASE_URL": "${input:stock_api_base_url}"
      }
    }
  }
}
```

## For Production Azure Deployment

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
  "mcpServers": {
    "aistocktrade": {
      "command": "dnx",
      "args": ["--", "emmanuelknafo.AiStockTradeMcpServer"],
      "env": {
        "STOCK_API_BASE_URL": "${input:stock_api_base_url}"
      }
    }
  }
}
```

## Configuration Notes

- **STOCK_API_BASE_URL**: Must point to your running AI Stock Trade API
- Make sure the API is accessible from where the MCP server is running
- For local development, ensure both API and UI projects are running
- The MCP server will log connection attempts and errors to help with troubleshooting
