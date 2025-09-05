# UI Test Automation & Azure DevOps Utilities

This directory contains PowerShell scripts for UI test automation and Azure DevOps test case maintenance.

---

## UI Test Runner Scripts

### run-ui-tests.ps1 (Full Featured)

Comprehensive runner managing application lifecycle, database setup, and Playwright UI tests.

#### Features (run-ui-tests)

- Automatic application startup & cleanup
- LocalDB or Docker SQL Server support
- Environment & timeout controls
- Optional database reset
- Build skip option

#### Usage (run-ui-tests)

```powershell
# LocalDB default
./scripts/run-ui-tests.ps1

# Use Docker SQL Server
./scripts/run-ui-tests.ps1 -UseDocker

# Clean database first
./scripts/run-ui-tests.ps1 -CleanDatabase

# Custom port & environment
./scripts/run-ui-tests.ps1 -Port 8080 -Environment Testing -TimeoutMinutes 20

# Skip build if already built
./scripts/run-ui-tests.ps1 -SkipBuild
```

#### Parameters (run-ui-tests)

- Environment (default: Development)
- Port (default: 5000)
- DatabaseName (default: StockTrackerTestUI)
- SqlConnectionString
- UseDocker
- CleanDatabase
- SkipBuild
- TimeoutMinutes (default: 15)

### quick-ui-test.ps1 (Lightweight)

Minimal fast iteration runner (LocalDB only).

#### Features (quick-ui-test)

- No Docker requirement
- Minimal setup
- Fast startup
- Auto cleanup

#### Usage (quick-ui-test)

```powershell
./scripts/quick-ui-test.ps1
./scripts/quick-ui-test.ps1 -CleanDatabase
./scripts/quick-ui-test.ps1 -TimeoutMinutes 15
```

#### Parameters (quick-ui-test)

- CleanDatabase
- TimeoutMinutes (default: 10)

---

## Azure DevOps Test Case Utilities

### Update-AdoTestCaseDescriptions.ps1

Adds or overwrites concise one-line HTML descriptions for every Test Case in a specified Azure DevOps Test Plan.

#### Key Features (update script)

- Enumerates all suites in the plan
- Collects distinct test case work item IDs
- Generates one-line description from title (normalized)
- Dry-run mode
- Overwrite or fill-only behavior
- Retry + API version header handling
- Diagnostic flags (-DumpFirstCase, -DumpOnUnknown, -ShowPatch)

#### Usage (update script)

```powershell
# Dry run (no changes)
./Update-AdoTestCaseDescriptions.ps1 -Organization myorg -Project aistocktradeapp -PlanId 1401 -DryRun

# Overwrite all existing descriptions
./Update-AdoTestCaseDescriptions.ps1 -Organization myorg -Project aistocktradeapp -PlanId 1401 -Overwrite

# Only fill blank descriptions
./Update-AdoTestCaseDescriptions.ps1 -Organization myorg -Project aistocktradeapp -PlanId 1401

# With diagnostics
./Update-AdoTestCaseDescriptions.ps1 -Organization myorg -Project aistocktradeapp -PlanId 1401 -Overwrite -DumpFirstCase -ShowPatch
```

#### Parameters (update script)

- Organization (ADO org name after <https://dev.azure.com/>)
- Project
- PlanId
- PatEnvVar (default: AZDO_PAT)
- Overwrite
- DryRun
- DumpFirstCase
- DumpOnUnknown
- ShowPatch

#### Required PAT Scopes

- Work Items (Read & Write)
- Test Plans (Read)

#### Description Generation Rules

- Trim & normalize title
- Remove prefixes (e.g., TC123:, [Tag])
- Replace hyphen separators with an en dash
- Truncate to 180 chars + ellipsis
- Ensure trailing period
- Wrap in paragraph tags (literal string `"<p>...</p>"`)

#### Exit Summary

- Updated = modified test cases
- Skipped = existing descriptions not overwritten (when -Overwrite not used)

#### Troubleshooting

- 401/403: Check PAT scopes / env var
- 404 suites: Invalid plan or license lacks Test Plans
- Patch errors: Use -ShowPatch
- Missing cases: Root-only plan (create suites)

#### Environment Variable Example

```powershell
$env:AZDO_PAT = 'xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx'
```

---

## Prerequisites

### LocalDB

1. SQL Server LocalDB
2. .NET 9 SDK
3. EF Core tools (`dotnet tool install --global dotnet-ef`)
4. Node.js (Playwright)

### Docker SQL (run-ui-tests.ps1 -UseDocker)

1. Docker Desktop running
2. .NET 9 SDK
3. EF Core tools
4. Node.js

---

## Setup

```powershell
# Install EF Core tools
dotnet tool install --global dotnet-ef

# Install Playwright browsers
npx playwright install --with-deps
```

## First-Time Run

```powershell
dotnet build --configuration Release
./scripts/quick-ui-test.ps1 -CleanDatabase
```

## Regular Run

```powershell
./scripts/quick-ui-test.ps1
```

---

## Troubleshooting (UI Tests)

1. App won't start: check port, logs
2. SQL not available: ensure LocalDB or Docker
3. Browser missing: `npx playwright install --with-deps`
4. Migration failed: re-run with -CleanDatabase
5. Cleanup issues: stop orphan dotnet processes

Process cleanup example:

```powershell
Get-Process -Name dotnet | Where-Object { $_.MainModule.FileName -like '*AiStockTradeApp*' } | Stop-Process -Force
```

---

## Test Configuration

From `test.runsettings`:

- Single-thread (Playwright requirement)
- 2m per-test timeout
- 20m session timeout

## Environment Variables Set By Scripts

- ASPNETCORE_ENVIRONMENT
- ASPNETCORE_URLS
- ConnectionStrings__DefaultConnection
- PLAYWRIGHT_BASE_URL

## CI/CD Integration

- Local fast path: quick-ui-test.ps1
- Full validation: run-ui-tests.ps1
- CI: GitHub Actions (scripts changes ignored for triggers)

## Script Flow (run-ui-tests)

1. Setup
2. Database (LocalDB/Docker)
3. Build (optional skip)
4. Migrate
5. Start app
6. Wait ready
7. Run tests
8. Cleanup

All scripts include robust error handling and guaranteed cleanup paths.
