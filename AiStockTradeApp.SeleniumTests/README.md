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

Edit `TestSettings.json` (non‑secret values only):

- `BaseUrl`: UI base URL (default local HTTPS: <https://localhost:7043>)
- `Headless`: true/false for Chrome headless mode
- `Culture`: default culture to test (en|fr)

Credentials are supplied via environment variables (preferred for local + CI):

Environment variables consumed:

| Variable | Purpose | Example |
|----------|---------|---------|
| `SELENIUM_BASE_URL` | Override BaseUrl | <https://localhost:7043> |
| `SELENIUM_HEADLESS` | Force headless (true/false) | true |
| `SELENIUM_CULTURE` | Override culture | fr |
| `SELENIUM_USERNAME` | Auth username | <testuser@example.com> |
| `SELENIUM_PASSWORD` | Auth password | (secret) |

Set locally (PowerShell):

```powershell
$env:SELENIUM_USERNAME = 'testuser@example.com'
$env:SELENIUM_PASSWORD = 'P@ssw0rd!'
dotnet test .\AiStockTradeApp.SeleniumTests\AiStockTradeApp.SeleniumTests.csproj --filter "Category=Authenticated"
```

In GitHub Actions / Azure DevOps put them in secure secret variables.

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

- Ensure `BaseUrl` (or set `SELENIUM_BASE_URL`) and provide credentials via `SELENIUM_USERNAME` / `SELENIUM_PASSWORD`
- Start the app via `./scripts/start.ps1 -Mode Local`
- Remove the `Skip = "..."` from a test once the prerequisite is ready
- Prefer data-testid selectors as used in the Page Objects; add them in the UI if missing

## Notes

- Uses Selenium 4 with chromedriver auto-management (Chrome required on test host)
- Traits mirror ADO suite grouping for filtered runs and reporting
- For merge/sign-in and error handling tests, consider lightweight API endpoints or seed hooks to set up session/user state deterministically
