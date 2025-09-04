# Docker SQL Server Port Configuration - Summary

## Problem Solved
The original Docker configuration used port 1433 for the SQL Server container, which conflicted with existing SQL Server installations on the host machine that use the default port 1433.

## Solution Implemented
Modified the Docker configuration to use port **14330** on the host instead of 1433, while keeping the internal container port as 1433.

## Changes Made

### 1. Updated Docker Compose Files
- **`docker-compose.yml`**: Changed SQL Server port mapping from `"1433:1433"` to `"14330:1433"`
- **`docker-compose.override.yml`**: Applied the same port change for consistency

### 2. Updated Documentation
- **`README.md`**: Added comprehensive Docker port configuration table and database access section
- **`scripts/start.ps1`**: Updated help text to mention the SQL Server port configuration

### 3. Created Helper Script
- **`scripts/connect-to-docker-sql.ps1`**: New PowerShell script to provide connection information and tools

## Current Port Configuration

| Service | Container Port | Host Port | Access URL |
|---------|---------------|-----------|------------|
| Web UI | 8080 | 8080 | http://localhost:8080 |
| REST API | 8080 | 8082 | http://localhost:8082 |
| MCP Server | 8080 | 5500 | http://localhost:5500/mcp |
| **SQL Server** | **1433** | **14330** | **localhost,14330** |

## Database Connection Details

When running in Docker mode, applications and external tools can connect to the SQL Server using:

- **Server**: `localhost,14330` (note the comma, not colon)
- **Database**: `StockTraderDb`
- **Username**: `sa`
- **Password**: `YourStrong@Passw0rd`
- **Connection String**: `Server=localhost,14330;Database=StockTraderDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;MultipleActiveResultSets=true`

## Using the Helper Script

The new helper script provides several useful functions:

```powershell
# Display connection information
.\scripts\connect-to-docker-sql.ps1

# Display connection information with password
.\scripts\connect-to-docker-sql.ps1 -ShowPassword

# Launch SQL Server Management Studio with connection details
.\scripts\connect-to-docker-sql.ps1 -Tool SSMS

# Launch Azure Data Studio with connection details
.\scripts\connect-to-docker-sql.ps1 -Tool ADS
```

## Verification

The implementation has been tested and verified:
- ✅ Docker containers start successfully with new port configuration
- ✅ SQL Server is accessible from host on port 14330
- ✅ No conflicts with existing SQL Server installations on port 1433
- ✅ All application services connect properly to the containerized database
- ✅ Helper script correctly identifies container status and tests connectivity
- ✅ MCP server integration continues to work properly

## Usage

To start the application with the new configuration:

```powershell
# Start in Docker mode (recommended)
.\scripts\start.ps1 -Mode Docker

# Get database connection details
.\scripts\connect-to-docker-sql.ps1
```

The SQL Server container will be available at `localhost,14330` and can be used by SQL Server Management Studio, Azure Data Studio, or any other SQL Server client tools without interfering with existing SQL Server installations.
