# Playwright Test Configuration

## Browser Support
- Chromium (default)
- Firefox
- WebKit (Safari)

## Test Categories
- Navigation Tests
- Stock Management Tests  
- User Interface Tests
- Performance Tests
- Accessibility Tests

## Running Tests

### Prerequisites
```bash
# Install Playwright browsers
dotnet build
playwright install
```

### Application Startup (Auto-Start Enabled)

By default the UI tests will attempt to auto-start the application if it is not already running.

```bash
# Standard run (auto-starts app on http://localhost:5000 if not running)
dotnet test

# Opt-out of auto-start (e.g., if you want to run the app yourself)
$env:DISABLE_UI_TEST_AUTOSTART = "true"  # PowerShell
export DISABLE_UI_TEST_AUTOSTART=true     # bash
dotnet run --project ./AiStockTradeApp/AiStockTradeApp.csproj
dotnet test
```

The default base URL now falls back to `http://localhost:5000` unless `PLAYWRIGHT_BASE_URL` is set.

### In-Memory Database Option

For faster UI test execution (and to avoid requiring a local SQL instance) the tests can run with an in-memory EF Core database:

```bash
# Enable in-memory DB (skips migrations) for all app code executed during UI tests
$env:USE_INMEMORY_DB = "true"  # PowerShell
export USE_INMEMORY_DB=true     # bash
dotnet test
```

When `USE_INMEMORY_DB=true` migrations are skipped and a volatile store is used (`UiTestDb`).

### Run Specific Test Categories

```bash
# Run only navigation tests
dotnet test --filter "NavigationTests"

# Run only UI tests
dotnet test --filter "UserInterfaceTests"

# Run only performance tests
dotnet test --filter "PerformanceTests"
```

### Run with Different Browsers

```bash
# Run with Firefox
BROWSER=firefox dotnet test

# Run with WebKit
BROWSER=webkit dotnet test
```

### Debugging Tests

```bash
# Run tests in headed mode (visible browser)
HEADED=1 dotnet test

# Run tests with slow motion
SLOWMO=1000 dotnet test
```

## Test Results and Traces

Failed tests automatically generate:

- Screenshots
- Video recordings  
- Trace files (playwright-traces/ directory)

## Configuration

Application URL defaults:

- `BaseUrl` (fallback): `http://localhost:5000`
- Override via `PLAYWRIGHT_BASE_URL` environment variable
- SSL certificate errors are ignored (still works if you point at `https://localhost:7043`)
- Viewport settings: 1280x720 (configurable)
- Timeout configurations: 10-15 seconds for most operations

If auto-start is disabled you must ensure the application is running at the chosen BaseUrl before executing tests.

## Test Resilience Features

1. **Application Startup Check**: Tests verify the application is running before starting
2. **SSL Certificate Handling**: Automatically ignores localhost SSL certificate errors
3. **Graceful API Failures**: Tests handle cases where stock APIs are unavailable
4. **Timeout Management**: Reasonable timeouts with fallback strategies
5. **Error Recovery**: Better error messages when application is not running

## Best Practices

1. **Start Application First**: Always start the web application before running tests
2. **Page Object Model**: Complex pages use page objects for maintainability
3. **Test Data Independence**: Each test cleans up its data
4. **Resilient Assertions**: Tests handle API failures gracefully
5. **Isolation**: Tests can run independently and in parallel
6. **Meaningful Error Messages**: Clear guidance when tests fail

## Troubleshooting

### Common Issues

1. **ERR_CONNECTION_REFUSED**: Application not running - start with `dotnet run`
2. **SSL Certificate Errors**: Automatically handled by test configuration
3. **Timeouts**: Application may be slow to start - wait longer before running tests
4. **API Failures**: Tests are designed to handle external API failures gracefully

### Quick Verification

Check if your setup is correct:

1. Application running: Visit `https://localhost:7043` (or chosen BaseUrl) in your browser
2. Playwright installed: `playwright install` completed successfully
3. Tests building: `dotnet build` in UITests directory succeeds
