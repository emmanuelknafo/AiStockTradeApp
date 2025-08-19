# AiStockTradeApp.Cli

A simple CLI for utility tasks.

Current command:

- download-historical: Automates nasdaq.com to download historical CSV for a symbol.
- import-historical: Import a CSV into the API's HistoricalPrices for a given symbol.

Usage:

- Install Playwright browsers once (only needed the first time):
  - dotnet tool install --global Microsoft.Playwright.CLI
  - playwright install --with-deps chromium

- Run:
  - dotnet run --project AiStockTradeApp.Cli -- download-historical -s GOOG -d ./goog.csv
  - dotnet run --project AiStockTradeApp.Cli -- import-historical -s AAPL --file ./data/nasdaq.com/HistoricalData_1755343611881_AAPL.csv --api <https://localhost:7043> --watch

Flags:

- --headful to see the browser for troubleshooting.
- For import-historical: --watch to poll job status; --api to set API base URL.
