# Quick UI Test Runner - Simplified version for local development
# Uses LocalDB by default, minimal setup required

param(
    [switch]$CleanDatabase = $false,
    [int]$TimeoutMinutes = 10
)

$ErrorActionPreference = "Stop"

# Get script directory and project paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$AppDir = Join-Path $ProjectRoot "ai-stock-trade-app"

Write-Host "=== Quick UI Test Runner ===" -ForegroundColor Green
Write-Host "Using LocalDB for testing" -ForegroundColor Yellow
Write-Host ""

# Variables for cleanup
$appProcess = $null

function Cleanup {
    if ($appProcess -and -not $appProcess.HasExited) {
        Write-Host "Stopping application..." -ForegroundColor Yellow
        Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue
    }
    
    # Kill any remaining dotnet processes for this app
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | 
        Where-Object { $_.MainModule.FileName -like "*ai-stock-trade-app*" } |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

try {
    # Set environment variables for LocalDB
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://localhost:5000"
    $env:ConnectionStrings__DefaultConnection = "Server=(localdb)\mssqllocaldb;Database=StockTrackerTestUI;Trusted_Connection=true;MultipleActiveResultSets=true"
    
    # Navigate to app directory
    Push-Location $AppDir
    
    try {
        # Quick build
        Write-Host "Building application..." -ForegroundColor Cyan
        dotnet build --configuration Release --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
        
        # Handle database
        if ($CleanDatabase) {
            Write-Host "Cleaning database..." -ForegroundColor Cyan
            dotnet ef database drop --force 2>$null
        }
        
        Write-Host "Updating database..." -ForegroundColor Cyan
        dotnet ef database update
        if ($LASTEXITCODE -ne 0) { throw "Database update failed" }
        
        # Start app
        Write-Host "Starting application..." -ForegroundColor Cyan
        $appProcess = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--no-build", "--configuration", "Release", "--no-launch-profile") -PassThru -WorkingDirectory $AppDir
        
        # Wait for app to be ready
        Write-Host "Waiting for application..." -ForegroundColor Cyan
        $timeout = 30
        for ($i = 0; $i -lt $timeout; $i++) {
            Start-Sleep -Seconds 1
            try {
                $response = Invoke-WebRequest -Uri "http://localhost:5000" -UseBasicParsing -TimeoutSec 3 -ErrorAction SilentlyContinue
                if ($response.StatusCode -eq 200) {
                    Write-Host "Application ready!" -ForegroundColor Green
                    break
                }
            } catch { }
            
            if ($appProcess.HasExited) { throw "Application exited unexpectedly" }
            if ($i -eq ($timeout - 1)) { throw "Application startup timeout" }
            Write-Host "." -NoNewline -ForegroundColor Yellow
        }
        
        # Run UI tests
        Pop-Location
        Write-Host "`nRunning UI tests..." -ForegroundColor Green
        
        $env:PLAYWRIGHT_BASE_URL = "http://localhost:5000"
        
        dotnet test ai-stock-trade-app.UITests --no-build --configuration Release --verbosity normal --filter "FullyQualifiedName~UITests" --settings test.runsettings
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "`nAll tests passed!" -ForegroundColor Green
        } else {
            throw "Tests failed with exit code: $LASTEXITCODE"
        }
        
    } finally {
        if (Get-Location | Where-Object { $_.Path -eq $AppDir }) {
            Pop-Location
        }
    }
}
catch {
    Write-Host "`nError: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    Cleanup
}

Write-Host "`n=== UI Tests Completed ===" -ForegroundColor Green
