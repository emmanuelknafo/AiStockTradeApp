<#
.SYNOPSIS
  Helper script to provide connection information for the containerized SQL Server instance.

.DESCRIPTION
  When running in Docker mode, SQL Server runs in a container and is accessible from the host
  on port 14330 (instead of the default 1433 to avoid conflicts with host SQL Server).
  
  This script provides the connection details and can optionally launch SQL Server Management Studio
  or Azure Data Studio with the correct connection parameters.

.PARAMETER Tool
  Tool to launch: "SSMS" for SQL Server Management Studio, "ADS" for Azure Data Studio, or "None" to just display connection info.

.PARAMETER ShowPassword
  If set, displays the SQL Server password in the output.

.EXAMPLES
  # Display connection information
  ./scripts/connect-to-docker-sql.ps1

  # Launch SQL Server Management Studio with connection parameters
  ./scripts/connect-to-docker-sql.ps1 -Tool SSMS

  # Launch Azure Data Studio with connection parameters  
  ./scripts/connect-to-docker-sql.ps1 -Tool ADS

  # Show connection info including password
  ./scripts/connect-to-docker-sql.ps1 -ShowPassword
#>

[CmdletBinding()]
param(
    [ValidateSet('SSMS', 'ADS', 'None')]
    [string]$Tool = 'None',
    
    [switch]$ShowPassword
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Connection details for Docker SQL Server
$ServerName = "localhost,14330"
$DatabaseName = "StockTraderDb"
$Username = "sa"
$Password = "YourStrong@Passw0rd"

Write-Host "`n🐳 Docker SQL Server Connection Information" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "Server:   $ServerName" -ForegroundColor Green
Write-Host "Database: $DatabaseName" -ForegroundColor Green
Write-Host "Username: $Username" -ForegroundColor Green

if ($ShowPassword) {
    Write-Host "Password: $Password" -ForegroundColor Green
} else {
    Write-Host "Password: ******* (use -ShowPassword to display)" -ForegroundColor Yellow
}

Write-Host "`nConnection String (for applications):" -ForegroundColor Cyan
$connectionString = "Server=$ServerName;Database=$DatabaseName;User Id=$Username;Password=$Password;TrustServerCertificate=true;MultipleActiveResultSets=true"
Write-Host $connectionString -ForegroundColor Gray

# Check if Docker containers are running
Write-Host "`n🔍 Checking Docker container status..." -ForegroundColor Cyan
try {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..') | ForEach-Object { $_.Path }
    $composeFile = Join-Path $repoRoot 'docker-compose.yml'
    
    $containers = & docker compose -f $composeFile ps --format json 2>$null | ConvertFrom-Json
    $sqlContainer = $containers | Where-Object { $_.Service -eq 'sqlserver' }
    
    if ($sqlContainer) {
        $status = $sqlContainer.State
        if ($status -eq 'running') {
            Write-Host "✅ SQL Server container is running" -ForegroundColor Green
            
            # Test connectivity
            Write-Host "`n🔌 Testing connectivity..." -ForegroundColor Cyan
            try {
                $testCmd = "docker exec $($sqlContainer.Name) /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P `"$Password`" -Q `"SELECT @@VERSION`""
                Invoke-Expression $testCmd 2>$null | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "✅ Connection test successful" -ForegroundColor Green
                } else {
                    Write-Host "❌ Connection test failed" -ForegroundColor Red
                }
            } catch {
                Write-Host "❌ Connection test failed: $($_.Exception.Message)" -ForegroundColor Red
            }
        } else {
            Write-Host "⚠️ SQL Server container exists but is not running (status: $status)" -ForegroundColor Yellow
            Write-Host "Run: ./scripts/start.ps1 -Mode Docker" -ForegroundColor Cyan
        }
    } else {
        Write-Host "❌ SQL Server container not found" -ForegroundColor Red
        Write-Host "Run: ./scripts/start.ps1 -Mode Docker" -ForegroundColor Cyan
    }
} catch {
    Write-Host "❌ Could not check Docker status: $($_.Exception.Message)" -ForegroundColor Red
}

# Launch tool if requested
switch ($Tool) {
    'SSMS' {
        Write-Host "`n🚀 Launching SQL Server Management Studio..." -ForegroundColor Cyan
        try {
            # Try to find SSMS in common locations
            $ssmsPaths = @(
                "${env:ProgramFiles(x86)}\Microsoft SQL Server Management Studio 19\Common7\IDE\Ssms.exe",
                "${env:ProgramFiles(x86)}\Microsoft SQL Server Management Studio 18\Common7\IDE\Ssms.exe",
                "${env:ProgramFiles}\Microsoft SQL Server Management Studio 19\Common7\IDE\Ssms.exe",
                "${env:ProgramFiles}\Microsoft SQL Server Management Studio 18\Common7\IDE\Ssms.exe"
            )
            
            $ssmsPath = $ssmsPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
            
            if ($ssmsPath) {
                $ssmsArgs = "-S `"$ServerName`" -d `"$DatabaseName`" -U `"$Username`""
                Start-Process -FilePath $ssmsPath -ArgumentList $ssmsArgs
                Write-Host "✅ SSMS launched with connection parameters" -ForegroundColor Green
                Write-Host "You will need to enter the password manually: $Password" -ForegroundColor Yellow
            } else {
                Write-Host "❌ SQL Server Management Studio not found in common locations" -ForegroundColor Red
                Write-Host "Please install SSMS or launch it manually with the connection details above" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "❌ Failed to launch SSMS: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    'ADS' {
        Write-Host "`n🚀 Launching Azure Data Studio..." -ForegroundColor Cyan
        try {
            # Try to find Azure Data Studio
            $adsCommand = Get-Command "azuredatastudio" -ErrorAction SilentlyContinue
            
            if ($adsCommand) {
                Start-Process -FilePath "azuredatastudio" -ArgumentList "--new-connection"
                Write-Host "✅ Azure Data Studio launched" -ForegroundColor Green
                Write-Host "Use the connection details displayed above" -ForegroundColor Yellow
            } else {
                Write-Host "❌ Azure Data Studio not found in PATH" -ForegroundColor Red
                Write-Host "Please install Azure Data Studio or launch it manually with the connection details above" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "❌ Failed to launch Azure Data Studio: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host "`n💡 Tips:" -ForegroundColor Cyan
Write-Host "- The SQL Server container uses port 14330 to avoid conflicts with host SQL Server (1433)" -ForegroundColor Gray
Write-Host "- Use 'localhost,14330' as the server name (note the comma, not colon)" -ForegroundColor Gray
Write-Host "- TrustServerCertificate=true is required for container connections" -ForegroundColor Gray
Write-Host "- Container data persists in Docker volumes between restarts" -ForegroundColor Gray
