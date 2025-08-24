# AiStockTradeApp.Services

This .NET class library contains service implementations, background services, and interfaces for business logic used by the application.

Contents

- `Interfaces/` - Service interface definitions (e.g. `IStockDataService`, `IWatchlistService`).
- `Implementations/` - Concrete service classes that implement business logic and integrate with external APIs.
- `BackgroundServices/` - Hosted/background workers (timed refresh, cache maintenance, etc.).

Usage

- Services are registered in the main web project via dependency injection and consumed by controllers and background workers.
