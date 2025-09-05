# Quick Test Setup Instructions

## Before Running UI Tests

### 1. Start the Application
```bash
# Open a terminal and navigate to the main application
cd AiStockTradeApp

# Start the application (it will run on https://localhost:7043)
dotnet run
```

**Wait for the application to fully start** - you should see output like:
```
Now listening on: https://localhost:7043
Now listening on: http://localhost:5259
Application started. Press Ctrl+C to shut down.
```

### 2. Verify Application is Running
Open your browser and navigate to: `https://localhost:7043`
- You should see the "AI-Powered Stock Tracker" application
- Accept any SSL certificate warnings for localhost

### 3. Run the UI Tests
```bash
# Open a NEW terminal window (keep the first one running the app)
cd AiStockTradeApp.PlaywrightUITests

# Install Playwright browsers (first time only)
playwright install

# Run all tests
dotnet test

# Or run specific test categories
dotnet test --filter "NavigationTests"
dotnet test --filter "StockManagementTests"
```

## Troubleshooting

### "ERR_CONNECTION_REFUSED" Error
- **Cause**: The application is not running
- **Solution**: Ensure step 1 is completed and the app is running at https://localhost:7043

### SSL Certificate Errors
- **Cause**: Development SSL certificate issues
- **Solution**: The tests are configured to ignore SSL errors automatically

### Test Timeouts
- **Cause**: Application is slow to start or respond
- **Solution**: Wait longer for the application to fully start before running tests

### Port Conflicts
- **Cause**: Another application is using port 7043
- **Solution**: Stop other applications or check launchSettings.json for alternative ports

## Running Tests in Different Modes

```bash
# Run with visible browser (for debugging)
$env:HEADED="1"
dotnet test

# Run with slow motion (for debugging)
$env:SLOWMO="1000"
dotnet test

# Run with specific browser
$env:BROWSER="firefox"
dotnet test
```

## Test Output

- **Screenshots**: Automatically captured on test failures
- **Traces**: Saved in `playwright-traces/` directory for failed tests
- **Videos**: Available for debugging test failures

View traces with: `playwright show-trace path/to/trace.zip`