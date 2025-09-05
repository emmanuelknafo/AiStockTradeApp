# AiStockTradeApp.Cli

> Standardized Documentation Header (2025-09-05)
> Unified format. ADO test case description automation: `scripts/Update-AdoTestCaseDescriptions.ps1`.

A command-line interface tool for stock data management and automation tasks in the AI Stock Trade application.

## Overview

The CLI provides automation tools for downloading and importing historical stock data, primarily designed to work with NASDAQ.com data sources and the AiStockTradeApp.Api backend.

## Available Commands

### 1. download-historical

Automates web scraping of NASDAQ.com to download historical CSV data for stock symbols.

**Usage:**

```bash
dotnet run --project AiStockTradeApp.Cli -- download-historical -s GOOG -d ./goog.csv
```

**Parameters:**

- `-s, --symbol` - Stock ticker symbol (e.g., AAPL, GOOG, MSFT)
- `-d, --destination` - Output file path for downloaded CSV
- `--headful` - Show browser during automation (for debugging)

**Features:**

- **Web automation** using Playwright
- **NASDAQ.com integration** for reliable historical data
- **CSV format output** compatible with import tools
- **Error handling** for network and parsing issues
- **Headless operation** by default for automated scenarios

### 2. import-historical

Imports historical stock data from CSV files into the AiStockTradeApp.Api database.

**Usage:**

```bash
dotnet run --project AiStockTradeApp.Cli -- import-historical -s AAPL --file ./data/HistoricalData_AAPL.csv --api https://localhost:7043 --watch
```

**Parameters:**

- `-s, --symbol` - Stock ticker symbol for the import
- `--file` - Path to CSV file containing historical data
- `--api` - Base URL of the AiStockTradeApp.Api service
- `--watch` - Monitor import job progress and display status updates

**Features:**

- **HTTP API integration** with AiStockTradeApp.Api
- **Job status monitoring** with real-time progress updates
- **CSV validation** before upload
- **Error reporting** for failed imports
- **Asynchronous processing** through background job system

## Prerequisites

### First-Time Setup

1. **Install Playwright browsers** (required for download-historical command):

   ```bash
   # Install Playwright CLI globally
   dotnet tool install --global Microsoft.Playwright.CLI

   # Install Chromium browser with dependencies
   playwright install --with-deps chromium
   ```

2. **Verify API availability** (required for import-historical command):
   - Ensure AiStockTradeApp.Api is running on the target URL
   - Default API URL: `https://localhost:7043`

## CSV Data Format

The CLI expects CSV files in NASDAQ.com format with the following columns:

```csv
Date,Close/Last,Volume,Open,High,Low
01/02/2024,$190.85,"49,128,406",$187.15,$192.42,$184.35
01/03/2024,$184.25,"66,831,572",$190.90,$191.95,$182.73
```

**Column Details:**

- **Date** - Trading date (MM/dd/yyyy format)
- **Close/Last** - Closing price (may include $ symbol)
- **Volume** - Trading volume (may include commas)
- **Open** - Opening price
- **High** - Day's highest price
- **Low** - Day's lowest price

## Example Workflows

### Complete Data Pipeline

1. **Download historical data**:
   ```bash
   dotnet run --project AiStockTradeApp.Cli -- download-historical -s AAPL -d ./data/AAPL_historical.csv
   ```

2. **Import into database**:
   ```bash
   dotnet run --project AiStockTradeApp.Cli -- import-historical -s AAPL --file ./data/AAPL_historical.csv --api https://localhost:7043 --watch
   ```

### Batch Processing Multiple Stocks

```bash
# Download multiple stocks
dotnet run --project AiStockTradeApp.Cli -- download-historical -s AAPL -d ./data/AAPL.csv
dotnet run --project AiStockTradeApp.Cli -- download-historical -s GOOGL -d ./data/GOOGL.csv
dotnet run --project AiStockTradeApp.Cli -- download-historical -s MSFT -d ./data/MSFT.csv

# Import all downloaded files
dotnet run --project AiStockTradeApp.Cli -- import-historical -s AAPL --file ./data/AAPL.csv --api https://localhost:7043
dotnet run --project AiStockTradeApp.Cli -- import-historical -s GOOGL --file ./data/GOOGL.csv --api https://localhost:7043
dotnet run --project AiStockTradeApp.Cli -- import-historical -s MSFT --file ./data/MSFT.csv --api https://localhost:7043
```

## Configuration

### Environment Variables

- `PLAYWRIGHT_BASE_URL` - Override default NASDAQ.com URL
- `DEFAULT_API_URL` - Default API URL if not specified in command
- `PLAYWRIGHT_HEADLESS` - Set to "false" to show browser (overrides --headful)

### API Configuration

The CLI communicates with the API using standard HTTP requests:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7043",
    "TimeoutSeconds": 300,
    "RetryAttempts": 3
  }
}
```

## Troubleshooting

### Common Issues

#### Browser Installation Problems

```bash
# Reinstall Playwright browsers
playwright install --force --with-deps chromium
```

#### API Connection Issues

- Verify API is running: `curl https://localhost:7043/swagger`
- Check firewall settings for localhost connections
- Ensure correct API URL in command parameters

#### CSV Format Issues

- Verify file contains expected columns
- Check for proper date format (MM/dd/yyyy)
- Ensure numeric values are properly formatted

#### Download Failures

```bash
# Use headful mode to see what's happening
dotnet run --project AiStockTradeApp.Cli -- download-historical -s AAPL -d ./test.csv --headful
```

### Debug Mode

For troubleshooting, use the `--headful` flag to see browser automation:

```bash
dotnet run --project AiStockTradeApp.Cli -- download-historical -s AAPL -d ./debug.csv --headful
```

## Performance Considerations

### Download Performance

- Downloads are limited by NASDAQ.com rate limits
- Allow 5-10 seconds between requests for multiple symbols
- Use headless mode for better performance

### Import Performance

- Large CSV files are processed asynchronously
- Monitor job status with `--watch` flag
- API handles batching automatically for optimal performance

## Development

### Adding New Commands

1. Create command class implementing appropriate interface
2. Register command in Program.cs
3. Add command-line argument parsing
4. Implement business logic with error handling
5. Add tests for new functionality

### Dependencies

The CLI project uses:

- **Microsoft.Playwright** - Web automation
- **System.CommandLine** - Command-line parsing
- **Microsoft.Extensions.Http** - HTTP client factory
- **Microsoft.Extensions.Logging** - Structured logging

## Integration with Main Application

The CLI is designed to work seamlessly with the main application:

- **Data Format Compatibility** - Uses same CSV format as manual imports
- **API Integration** - Directly calls AiStockTradeApp.Api endpoints
- **Job Monitoring** - Integrates with background job system
- **Error Handling** - Consistent error reporting across tools

## Security Considerations

- **No API keys required** for NASDAQ.com downloads (public data)
- **Local API calls** only (no external authentication)
- **File system access** limited to specified directories
- **Browser automation** runs in isolated environment

## Related Projects

- **[AiStockTradeApp.Api](../AiStockTradeApp.Api/)** - API backend for data import
- **[AiStockTradeApp](../AiStockTradeApp/)** - Main web application
- **[AiStockTradeApp.Entities](../AiStockTradeApp.Entities/)** - Shared data models
