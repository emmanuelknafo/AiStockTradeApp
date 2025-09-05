# Selenium UI Tests

This project contains Selenium-based UI tests for the AI Stock Trade application. The tests use Selenium WebDriver to automate browser interactions and validate the application's user interface functionality.

## Features

- **Auto-Start Capability**: Tests automatically start the UI and API applications if they're not already running
- **Cross-Browser Support**: Configurable to run on Chrome, Firefox, or Edge
- **Test Categories**: Organized tests for different scenarios (Anonymous, Authenticated, CrossCutting)
- **Page Object Model**: Clean separation of test logic and page interactions
- **Environment Configuration**: Flexible configuration via JSON and environment variables
- **CI/CD Ready**: Supports headless execution for continuous integration

## Project Structure

```
AiStockTradeApp.SeleniumTests/
├── Infrastructure/
│   ├── TestBase.cs              # Base class for all tests with auto-start capability
│   ├── TestSetupHelper.cs       # Auto-start helper for UI and API applications
│   └── TestDataSeeder.cs        # Test data seeding utilities
├── Pages/
│   ├── DashboardPage.cs         # Dashboard page object
│   └── AuthPage.cs              # Authentication page object
├── Tests/
│   ├── AnonymousWatchlistTests.cs     # Tests for anonymous user functionality
│   ├── AuthenticatedWatchlistTests.cs # Tests for authenticated user features
│   └── CrossCuttingTests.cs           # Cross-cutting concern tests
├── Drivers/
│   ├── WebDriverFactory.cs     # WebDriver creation and configuration
│   └── TestSettings.cs          # Test configuration models
├── Utils/
│   └── CultureSwitcher.cs       # Localization testing utilities
├── AssemblyTeardownTests.cs     # Global cleanup for auto-started processes
├── TestSettings.json            # Default test configuration
└── GlobalUsings.cs              # Global using statements
```

## Getting Started

### Prerequisites

1. **.NET 9 SDK** installed
2. **Google Chrome** browser installed (default driver)
3. **Visual Studio 2022** or **VS Code** with C# extension

### Configuration

The tests can be configured via `TestSettings.json` or environment variables:

#### TestSettings.json
```json
{
  "BaseUrl": "https://localhost:7043",
  "Browser": "Chrome",
  "Headless": true,
  "ImplicitWaitMs": 2000,
  "PageLoadTimeoutSec": 60,
  "Credentials": {
    "Username": "",
    "Password": ""
  },
  "Culture": "en"
}
```

#### Environment Variables
- `SELENIUM_BASE_URL`: Override base URL (e.g., "https://localhost:7043")
- `SELENIUM_API_BASE_URL`: Override API base URL (e.g., "https://localhost:7032")
- `SELENIUM_HEADLESS`: Run in headless mode (true/false)
- `SELENIUM_CULTURE`: Set test culture ("en" or "fr")
- `SELENIUM_USERNAME`: Test user username
- `SELENIUM_PASSWORD`: Test user password
- `DISABLE_SELENIUM_TEST_AUTOSTART`: Disable auto-start functionality

## Running Tests

### Auto-Start Mode (Recommended)

The tests will automatically start the UI and API applications if they're not running:

```powershell
# Run all tests (applications will auto-start)
dotnet test AiStockTradeApp.SeleniumTests

# Run from Visual Studio Test Explorer
# 1. Build the solution
# 2. Open Test Explorer (Test > Test Explorer)
# 3. Run tests - applications will start automatically
```

### Manual Start Mode

If you prefer to start applications manually:

```powershell
# Terminal 1: Start the application stack
.\scripts\start.ps1 -Mode Local

# Terminal 2: Run tests with auto-start disabled
$env:DISABLE_SELENIUM_TEST_AUTOSTART = "true"
dotnet test AiStockTradeApp.SeleniumTests
```

### Test Categories

Run specific test categories:

```powershell
# Anonymous user tests
dotnet test --filter "Category=Anonymous"

# Authenticated user tests  
dotnet test --filter "Category=Authenticated"

# Cross-cutting concern tests
dotnet test --filter "Category=CrossCutting"

# Specific test by ADO ID
dotnet test --filter "AdoId=1408"
```

### CI/CD Configuration

For continuous integration environments:

```yaml
# GitHub Actions example
- name: Run Selenium Tests
  run: |
    dotnet test AiStockTradeApp.SeleniumTests \
      --logger trx \
      --results-directory TestResults
  env:
    SELENIUM_HEADLESS: true
    SELENIUM_BASE_URL: https://localhost:7043
    USE_INMEMORY_DB: true
```

## Test Development

### Creating New Tests

1. **Inherit from TestBase**: All test classes should inherit from `TestBase`
2. **Use Page Objects**: Interact with pages through page object classes
3. **Add Test Categories**: Use `[Trait("Category", "...")]` for organization
4. **Add ADO IDs**: Use `[Trait("AdoId", "...")]` for traceability

```csharp
public class NewFeatureTests : TestBase
{
    [Trait("Category", "Anonymous")]
    [Trait("AdoId", "1234")]
    [Fact]
    public async Task NewFeature_ShouldWork_WhenConditionMet()
    {
        // Test setup is automatic via TestBase
        var dashboard = new DashboardPage(Driver);
        
        // Test implementation
        dashboard.Go(Settings.BaseUrl)
                 .PerformAction();
        
        // Assertions
        Assert.True(dashboard.IsExpectedStateVisible());
    }
}
```

### Page Object Pattern

Create page objects for complex UI interactions:

```csharp
public class NewFeaturePage
{
    private readonly IWebDriver _driver;
    
    public NewFeaturePage(IWebDriver driver)
    {
        _driver = driver;
    }
    
    public NewFeaturePage PerformAction()
    {
        _driver.FindElement(By.Id("action-button")).Click();
        return this;
    }
    
    public bool IsExpectedStateVisible()
    {
        return _driver.FindElement(By.Id("expected-element")).Displayed;
    }
}
```

## Auto-Start Architecture

The auto-start functionality ensures tests can run independently without manual application startup:

### Startup Process

1. **Check Application Status**: Tests first check if UI and API are already running
2. **Start API**: If not running, starts the API project with in-memory database
3. **Start UI**: If not running, starts the UI project configured to call the API
4. **Wait for Ready**: Waits for both applications to respond to health checks
5. **Run Tests**: Proceeds with test execution once applications are ready

### Process Management

- **Shared Process Management**: Multiple test classes share the same auto-started processes
- **Automatic Cleanup**: Processes are automatically terminated when tests complete
- **Error Handling**: Clear error messages if auto-start fails
- **Opt-out Support**: Can be disabled via `DISABLE_SELENIUM_TEST_AUTOSTART` environment variable

### Configuration for Auto-Start

The auto-start feature uses these default configurations:
- **UI Application**: `https://localhost:7043` (configurable via `SELENIUM_BASE_URL`)
- **API Application**: `https://localhost:7032` (configurable via `SELENIUM_API_BASE_URL`)
- **Database**: In-memory database to avoid SQL Server dependency
- **Environment**: Development mode with optimized startup

## Browser Configuration

### Supported Browsers

- **Chrome** (default): Fast and reliable
- **Firefox**: Alternative browser testing
- **Edge**: Microsoft browser testing

### Driver Management

Selenium WebDriver automatically manages browser drivers. No manual driver downloads required.

### Headless Mode

Tests run in headless mode by default for CI/CD. Enable headed mode for debugging:

```powershell
$env:SELENIUM_HEADLESS = "false"
dotnet test AiStockTradeApp.SeleniumTests
```

## Debugging

### Debug Failed Tests

1. **Set Headless to False**: `$env:SELENIUM_HEADLESS = "false"`
2. **Add Breakpoints**: Set breakpoints in test code
3. **Use Visual Studio Debugger**: Right-click test > Debug Test
4. **Check Application Logs**: Review auto-started application output

### Common Issues

1. **Browser Not Found**: Ensure Chrome is installed in default location
2. **Port Conflicts**: Check if ports 7043/7032 are available
3. **Slow Tests**: Increase timeout values in TestSettings.json
4. **Authentication Issues**: Verify test credentials are configured

### Logs and Diagnostics

Auto-started applications log to console. Enable detailed logging:

```csharp
// In test code, temporarily add:
Console.WriteLine($"Current URL: {Driver.Url}");
Console.WriteLine($"Page source: {Driver.PageSource}");
```

## Best Practices

1. **Test Independence**: Each test should be independent and not rely on other tests
2. **Data Cleanup**: Clean up test data after each test
3. **Stable Selectors**: Use `data-testid` attributes for reliable element selection
4. **Wait Strategies**: Use explicit waits instead of Thread.Sleep
5. **Page Objects**: Encapsulate page interactions in page object classes
6. **Error Messages**: Provide clear assertion messages for failures

## Integration with Test Explorer

The Selenium tests integrate seamlessly with Visual Studio Test Explorer:

1. **Build Solution**: Build the entire solution
2. **Open Test Explorer**: Test > Test Explorer
3. **Discover Tests**: Tests appear automatically after build
4. **Run Tests**: Click run - applications auto-start if needed
5. **Debug Tests**: Right-click > Debug Test for debugging
6. **Filter Tests**: Use categories and traits to filter tests

## Maintenance

### Updating Dependencies

```powershell
# Update Selenium packages
dotnet add package Selenium.WebDriver
dotnet add package Selenium.Support

# Update test framework
dotnet add package Microsoft.NET.Test.Sdk
dotnet add package xunit
```

### Test Data Management

TestSettings.json is copied to output automatically. Update configuration as needed for different environments.

### Performance Optimization

- Tests use parallel execution where safe
- In-memory database for faster test execution
- Optimized WebDriver settings for CI/CD environments
- Automatic process cleanup to prevent resource leaks
