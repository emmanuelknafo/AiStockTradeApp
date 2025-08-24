# AiStockTradeApp.DataAccess

Project: AiStockTradeApp.DataAccess (class library)

Purpose: data access layer for the AiStockTradeApp solution. Contains EF Core DbContext, migrations, repository interfaces and implementations used to persist and query stock and historical price data.

Key folders:

- `Data/` — DbContext and example/seed data.
- `Migrations/` — EF Core migrations for schema versioning.
- `Interfaces/` — Repository and data-access interfaces consumed by services.
- `Repositories/` — Concrete repository implementations and query logic.

Build & tooling:

- Build as part of the solution: `dotnet build` from the repo root.
- Manage migrations with EF Core CLI from this project, e.g. `dotnet ef migrations add <Name> --project AiStockTradeApp.DataAccess`.
