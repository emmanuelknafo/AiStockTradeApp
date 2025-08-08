# UI Test Automation Scripts

This directory contains PowerShell scripts for running automated UI tests with proper database setup and application management.

## Scripts

### 1. `run-ui-tests.ps1` - Full-Featured Test Runner

A comprehensive script that provides complete control over the test environment.

#### Features:
- Automatic application startup and cleanup
- Database management (LocalDB or Docker SQL Server)
- Environment configuration
- Timeout management
- Process cleanup

#### Usage:

```powershell
# Basic usage with LocalDB
.\scripts\run-ui-tests.ps1

# Use Docker SQL Server
.\scripts\run-ui-tests.ps1 -UseDocker

# Clean database before tests
.\scripts\run-ui-tests.ps1 -CleanDatabase

# Custom configuration
.\scripts\run-ui-tests.ps1 -Port 8080 -Environment "Testing" -TimeoutMinutes 20

# Skip build (if already built)
.\scripts\run-ui-tests.ps1 -SkipBuild
```

#### Parameters:
- `Environment` - ASP.NET Core environment (default: "Development")
- `Port` - Application port (default: "5000")
- `DatabaseName` - Database name (default: "StockTrackerTestUI")
- `SqlConnectionString` - Custom connection string
- `UseDocker` - Use Docker SQL Server instead of LocalDB
- `CleanDatabase` - Drop and recreate database
- `SkipBuild` - Skip building the application
- `TimeoutMinutes` - Test execution timeout (default: 15)

### 2. `quick-ui-test.ps1` - Simplified Test Runner

A lightweight script for quick local testing.

#### Features:
- Uses LocalDB (no Docker required)
- Minimal configuration
- Fast startup
- Automatic cleanup

#### Usage:

```powershell
# Quick test run
.\scripts\quick-ui-test.ps1

# Clean database and test
.\scripts\quick-ui-test.ps1 -CleanDatabase

# Custom timeout
.\scripts\quick-ui-test.ps1 -TimeoutMinutes 15
```

#### Parameters:
- `CleanDatabase` - Drop and recreate database
- `TimeoutMinutes` - Test execution timeout (default: 10)

## Prerequisites

### For LocalDB (quick-ui-test.ps1 and default run-ui-tests.ps1):
1. SQL Server LocalDB installed
2. .NET 9.0 SDK
3. Entity Framework Core tools: `dotnet tool install --global dotnet-ef`
4. Node.js (for Playwright)

### For Docker SQL Server (run-ui-tests.ps1 -UseDocker):
1. Docker Desktop installed and running
2. .NET 9.0 SDK
3. Entity Framework Core tools: `dotnet tool install --global dotnet-ef`
4. Node.js (for Playwright)

## Setup Instructions

1. **Install Prerequisites**:
   ```powershell
   # Install EF Core tools
   dotnet tool install --global dotnet-ef
   
   # Install Playwright (run once)
   npx playwright install --with-deps
   ```

2. **First-time setup**:
   ```powershell
   # Build the application
   dotnet build --configuration Release
   
   # Run tests with clean database
   .\scripts\quick-ui-test.ps1 -CleanDatabase
   ```

3. **Regular testing**:
   ```powershell
   # Quick test run
   .\scripts\quick-ui-test.ps1
   ```

## Troubleshooting

### Common Issues:

1. **"Application failed to start"**:
   - Check if port 5000 is available
   - Verify database connection
   - Check application logs

2. **"SQL Server not available"**:
   - For LocalDB: Ensure SQL Server LocalDB is installed
   - For Docker: Ensure Docker Desktop is running

3. **"Playwright browser not found"**:
   ```powershell
   npx playwright install --with-deps
   ```

4. **"Database migration failed"**:
   ```powershell
   # Reset database
   .\scripts\quick-ui-test.ps1 -CleanDatabase
   ```

5. **"Process cleanup issues"**:
   - Scripts automatically handle cleanup
   - Manually kill processes if needed:
   ```powershell
   Get-Process -Name "dotnet" | Where-Object { $_.MainModule.FileName -like "*ai-stock-trade-app*" } | Stop-Process -Force
   ```

### Test Configuration:

The scripts use the `test.runsettings` file for test configuration:
- Single-threaded execution (required for Playwright)
- 2-minute individual test timeout
- 20-minute session timeout

### Environment Variables:

The scripts set these environment variables:
- `ASPNETCORE_ENVIRONMENT`: Development/Testing
- `ASPNETCORE_URLS`: http://localhost:5000 (or custom port)
- `ConnectionStrings__DefaultConnection`: Database connection
- `PLAYWRIGHT_BASE_URL`: http://localhost:5000 (or custom port)

## Integration with CI/CD

These scripts are designed to work locally and complement the GitHub Actions workflow:

- **Local Development**: Use `quick-ui-test.ps1` for fast iteration
- **Full Testing**: Use `run-ui-tests.ps1` for comprehensive testing
- **CI/CD**: GitHub Actions workflow handles automated testing in the cloud

## Script Architecture

Both scripts follow this pattern:
1. **Setup**: Configure environment variables and paths
2. **Database**: Setup SQL Server (LocalDB or Docker)
3. **Build**: Compile application (unless skipped)
4. **Migrate**: Run EF Core migrations
5. **Start**: Launch application in background
6. **Wait**: Ensure application is ready
7. **Test**: Execute Playwright UI tests
8. **Cleanup**: Stop processes and cleanup resources

The scripts are designed to be robust with proper error handling and cleanup even if tests fail.
