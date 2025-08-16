# AiStockTradeApp.Cli

A simple CLI for utility tasks.

Current command:

- download-historical: Automates nasdaq.com to download historical CSV for a symbol.

Usage:

- Install Playwright browsers once (only needed the first time):
  - dotnet tool install --global Microsoft.Playwright.CLI
  - playwright install --with-deps chromium

- Run:
  - dotnet run --project AiStockTradeApp.Cli -- download-historical -s GOOG -d ./goog.csv

Flags:

- --headful to see the browser for troubleshooting.
