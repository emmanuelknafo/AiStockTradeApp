# Playwright UI Tests Setup Guide

## Prerequisites

1. **.NET 9 SDK** - Make sure you have .NET 9 SDK installed
2. **Visual Studio 2022** or **VS Code** with C# extension
3. **Node.js** (optional, for browser management)

## Initial Setup

### 1. Install Playwright Browsers

Before running the tests for the first time, you need to install the Playwright browsers:

```powershell
# Navigate to the UI tests project directory
cd ai-stock-trade-app.UITests

# Build the project first
dotnet build

# Install Playwright browsers
playwright install

# Or install specific browsers
playwright install chromium firefox webkit
```

### 2. Configure Application URL

The `BaseUrl` in `BaseUITest.cs` is already configured to match your application's URL from launchSettings.json:

```csharp
protected string BaseUrl = "https://localhost:7043"; // Matches launchSettings.json
```

**Important**: Make sure your application is running on `https://localhost:7043` before running the tests.

## Running Tests

### Command Line

```powershell
# Start your application first (in a separate terminal)
cd ai-stock-trade-app
dotnet run

# Then run UI tests (in another terminal)
cd ai-stock-trade-app.UITests
dotnet test

# Run specific test class
dotnet test --filter "NavigationTests"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run in parallel (default)
dotnet test --parallel

# Run tests with custom settings
HEADED=1 dotnet test  # Run with visible browser
SLOWMO=1000 dotnet test  # Run with slow motion
```

### Visual Studio

1. **Start the main application** by right-clicking `ai-stock-trade-app` project ? Debug ? Start New Instance
2. Open **Test Explorer** (Test ? Test Explorer)
3. Build the solution
4. All UI tests should appear in the test explorer
5. Right-click and select **Run** or **Debug**

### VS Code

1. **Start the main application** in a terminal: `cd ai-stock-trade-app && dotnet run`
2. Install the **.NET Test Explorer** extension
3. Open the Command Palette (Ctrl+Shift+P)
4. Run **.NET: Test Explorer** 
5. Select tests to run

## Test Structure

### Base Test Class

- **BaseUITest.cs**: Contains common setup and teardown logic
  - Automatic screenshot capture on test failure
  - Trace recording for debugging
  - Common navigation methods
  - **BaseUrl**: `https://localhost:7043` (matches your application)

### Test Categories

1. **NavigationTests**: Basic page navigation and loading
2. **StockManagementTests**: Adding, removing, and managing stocks
3. **UserInterfaceTests**: UI interactions, theme changes, panels
4. **PerformanceTests**: Page load times and performance checks
5. **AccessibilityTests**: Basic accessibility compliance
6. **StockDashboardPageObjectTests**: Tests using the Page Object Model

### Page Objects

- **StockDashboardPage**: Encapsulates dashboard page interactions
  - Provides methods for common actions (AddStock, RemoveStock, etc.)
  - Centralizes element locators
  - Makes tests more maintainable

## Configuration

### Browser Configuration

Tests run on multiple browsers by default. To specify a browser:

```powershell
# Run tests on specific browser
BROWSER=firefox dotnet test
BROWSER=webkit dotnet test
BROWSER=chromium dotnet test
```

### Test Settings

Modify `BaseUITest.cs` to adjust:

- Timeout settings
- Screenshot/video recording options
- Viewport sizes
- Test data cleanup

## Debugging Failed Tests

### Trace Files

Failed tests automatically generate trace files in `playwright-traces/` directory:

1. Open trace files with: `playwright show-trace path/to/trace.zip`
2. View detailed execution steps, screenshots, and network requests

### Running Tests in Headed Mode

```powershell
# Run with visible browser for debugging
HEADED=1 dotnet test
```

### Debugging in VS Code

1. Set breakpoints in test code
2. Use "Debug Test" option in Test Explorer
3. Browser will pause at breakpoints

## CI/CD Integration

### GitHub Actions

```yaml
- name: Start Application
  run: |
    cd ai-stock-trade-app
    dotnet run &
    sleep 10  # Wait for application to start
  
- name: Install Playwright Browsers
  run: playwright install --with-deps

- name: Run UI Tests
  run: dotnet test ai-stock-trade-app.UITests --logger trx --results-directory TestResults

- name: Upload Test Results
  uses: actions/upload-artifact@v3
  if: always()
  with:
    name: test-results
    path: TestResults/
```

### Azure DevOps

```yaml
- task: DotNetCoreCLI@2
  displayName: 'Start Application'
  inputs:
    command: 'run'
    projects: 'ai-stock-trade-app'
    arguments: '--no-build'
  condition: succeededOrFailed()
  continueOnError: true

- task: DotNetCoreCLI@2
  displayName: 'Install Playwright Browsers'
  inputs:
    command: 'custom'
    custom: 'playwright'
    arguments: 'install --with-deps'

- task: DotNetCoreCLI@2
  displayName: 'Run UI Tests'
  inputs:
    command: 'test'
    projects: 'ai-stock-trade-app.UITests'
    arguments: '--logger trx --results-directory $(Agent.TempDirectory)'
```

## Best Practices

1. **Start Application First**: Always ensure the web application is running on `https://localhost:7043` before executing tests
2. **Test Data**: Use test-specific data to avoid conflicts
3. **Wait Strategies**: Use appropriate waits (NetworkIdle, specific elements)
4. **Page Objects**: Use page objects for complex interactions
5. **Cleanup**: Each test should clean up its data
6. **Parallel Execution**: Tests are designed to run in parallel

## Troubleshooting

### Common Issues

1. **Browsers not installed**: Run `playwright install`
2. **Application not running**: Start the web application with `dotnet run` first
3. **Port conflicts**: The application should run on `https://localhost:7043` as configured in launchSettings.json
4. **Connection refused**: Ensure the application is fully started before running tests
5. **Timeout errors**: Increase timeout values or check network connectivity

### Quick Start Checklist

1. ? Install Playwright browsers: `playwright install`
2. ? Start your application: `cd ai-stock-trade-app && dotnet run`
3. ? Wait for application to fully load (check https://localhost:7043 in browser)
4. ? Run tests: `cd ai-stock-trade-app.UITests && dotnet test`

### Support

- Check Playwright documentation: https://playwright.dev/dotnet/
- Review test logs and trace files for detailed error information
- Ensure the main application is running and accessible at `https://localhost:7043`