# AiStockTradeApp.SeleniumTests

Browser UI automation for the Watchlist scenarios using Selenium + xUnit.

## What’s included

- Page Objects: `DashboardPage`, `AuthPage`
- Test infrastructure: `TestBase` (WebDriver + config), `WebDriverFactory`, `TestSettings.json`
- 13 test skeletons matching ADO suites:
  - Authenticated (5)
  - Anonymous (4)
  - CrossCutting (4)

All tests are currently [Skipped] until you configure the app URL and data; enable them incrementally as setup is available.

## Configure

Edit `TestSettings.json`:

- `BaseUrl`: UI base URL (e.g., <https://localhost:5001>)
- `Headless`: true/false for Chrome headless mode
- `Credentials`: seeded test user credentials
- `Culture`: default culture to test (en|fr)

TestSettings.json is copied to output automatically.

## Run

From the repo root:

```powershell
# Run all Selenium tests (many will be skipped until enabled)
dotnet test .\AiStockTradeApp.SeleniumTests\AiStockTradeApp.SeleniumTests.csproj -v minimal

# Run by category (matches ADO suites)
dotnet test .\AiStockTradeApp.SeleniumTests\AiStockTradeApp.SeleniumTests.csproj --filter "Category=Authenticated"
dotnet test .\AiStockTradeApp.SeleniumTests\AiStockTradeApp.SeleniumTests.csproj --filter "Category=Anonymous"
dotnet test .\AiStockTradeApp.SeleniumTests\AiStockTradeApp.SeleniumTests.csproj --filter "Category=CrossCutting"
```

## Enable tests

- Fill in `BaseUrl` and `Credentials`
- Start the app via `./scripts/start.ps1 -Mode Local`
- Remove the `Skip = "..."` from a test once the prerequisite is ready
- Prefer data-testid selectors as used in the Page Objects; add them in the UI if missing

## Notes

- Uses Selenium 4 with chromedriver auto-management (Chrome required on test host)
- Traits mirror ADO suite grouping for filtered runs and reporting
- For merge/sign-in and error handling tests, consider lightweight API endpoints or seed hooks to set up session/user state deterministically
