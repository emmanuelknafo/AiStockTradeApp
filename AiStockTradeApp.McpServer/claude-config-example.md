# Claude Desktop MCP Configuration Example

## For Local Development

Add this configuration to your Claude Desktop `claude_desktop_config.json` file:

```json
{
  "mcpServers": {
    "aistocktrade": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\src\\GitHub\\emmanuelknafo\\AiStockTradeApp\\AiStockTradeApp.McpServer"
      ],
      "env": {
        "STOCK_API_BASE_URL": "http://localhost:5000"
      }
    }
  }
}
```

## For Published NuGet Package

Once published to NuGet.org, use this configuration:

```json
{
  "mcpServers": {
    "aistocktrade": {
      "command": "dnx",
      "args": ["--", "emmanuelknafo.AiStockTradeMcpServer"],
      "env": {
        "STOCK_API_BASE_URL": "http://localhost:5000"
      }
    }
  }
}
```

## For Production Azure Deployment

```json
{
  "mcpServers": {
    "aistocktrade": {
      "command": "dnx",
      "args": ["--", "emmanuelknafo.AiStockTradeMcpServer"],
      "env": {
        "STOCK_API_BASE_URL": "https://your-api.azurewebsites.net"
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
