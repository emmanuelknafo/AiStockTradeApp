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

### Run All Tests
```bash
# Start your application first
cd ai-stock-trade-app
dotnet run

# In another terminal, run tests
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
- `BaseUrl`: `https://localhost:7043` (matches your application configuration)
- Viewport settings
- Timeout configurations

**Important**: Ensure your application is running on `https://localhost:7043` before executing tests.

## Best Practices

1. **Start Application First**: Always start the web application before running tests
2. **Page Object Model**: Consider creating page objects for complex pages
3. **Test Data**: Use test-specific data to avoid conflicts
4. **Cleanup**: Each test should clean up its data
5. **Isolation**: Tests should be independent and able to run in parallel
6. **Assertions**: Use meaningful assertions with clear error messages