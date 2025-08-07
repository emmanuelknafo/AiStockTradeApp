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

### IMPORTANT: Start Application First
```bash
# Step 1: Start your application (in one terminal)
cd ai-stock-trade-app
dotnet run

# Step 2: Wait for application to start at https://localhost:7043

# Step 3: Run tests (in another terminal)
cd ai-stock-trade-app.UITests
dotnet test
```

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

Application URL is configured to match launchSettings.json:
- `BaseUrl`: `https://localhost:7043` (HTTPS profile from launchSettings.json)
- SSL certificate errors are automatically ignored for localhost
- Viewport settings: 1280x720 (configurable)
- Timeout configurations: 10-15 seconds for most operations

**Critical**: The application MUST be running at `https://localhost:7043` before executing tests.

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
1. Application running: Visit `https://localhost:7043` in your browser
2. Playwright installed: `playwright install` completed successfully
3. Tests building: `dotnet build` in UITests directory succeeds