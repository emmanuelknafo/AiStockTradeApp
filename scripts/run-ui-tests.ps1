# PowerShell script to start the application and run UI tests
# This script sets up a local SQL Server database and runs automated UI tests

param(
    [string]$Environment = "Development",
    [string]$Port = "5000",
    [string]$DatabaseName = "StockTrackerTestUI",
    [string]$SqlConnectionString = "Server=(localdb)\mssqllocaldb;Database=StockTrackerTestUI;Trusted_Connection=true;MultipleActiveResultSets=true",
    [switch]$UseDocker = $false,
    [switch]$CleanDatabase = $false,
    [switch]$SkipBuild = $false,
    [int]$TimeoutMinutes = 15
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$AppDir = Join-Path $ProjectRoot "ai-stock-trade-app"
$UITestDir = Join-Path $ProjectRoot "ai-stock-trade-app.UITests"

Write-Host "=== AI Stock Trade App - UI Test Runner ===" -ForegroundColor Green
Write-Host "Project Root: $ProjectRoot" -ForegroundColor Yellow
Write-Host "App Directory: $AppDir" -ForegroundColor Yellow
Write-Host "UI Test Directory: $UITestDir" -ForegroundColor Yellow
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "Port: $Port" -ForegroundColor Yellow
Write-Host "Database: $DatabaseName" -ForegroundColor Yellow
Write-Host ""

# Function to cleanup processes
function Stop-AppProcess {
    param([int]$ProcessId)
    
    if ($ProcessId -and (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) {
        Write-Host "Stopping application process (PID: $ProcessId)..." -ForegroundColor Yellow
        try {
            Stop-Process -Id $ProcessId -Force
            Start-Sleep -Seconds 2
        }
        catch {
            Write-Warning "Failed to stop process $ProcessId : $_"
        }
    }
}

# Function to setup SQL Server using Docker (if UseDocker switch is provided)
function Start-SqlServerDocker {
    Write-Host "Setting up SQL Server in Docker..." -ForegroundColor Cyan
    
    $containerName = "sqlserver-uitest"
    
    # Check if container exists and remove it
    $existingContainer = docker ps -a --filter "name=$containerName" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing SQL Server container..." -ForegroundColor Yellow
        docker rm -f $containerName 2>$null
    }
    
    # Start new SQL Server container
    Write-Host "Starting SQL Server container..." -ForegroundColor Cyan
    docker run -d `
        --name $containerName `
        -e "ACCEPT_EULA=Y" `
        -e "SA_PASSWORD=YourStrong@Passw0rd" `
        -p 1433:1433 `
        mcr.microsoft.com/mssql/server:2022-latest
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start SQL Server container"
    }
    
    # Wait for SQL Server to be ready
    Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Cyan
    $timeout = 60
    $elapsed = 0
    
    do {
        Start-Sleep -Seconds 2
        $elapsed += 2
        
        # Test connection
        $testResult = docker exec $containerName /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -Q "SELECT 1" 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "SQL Server is ready!" -ForegroundColor Green
            break
        }
        
        if ($elapsed -ge $timeout) {
            throw "SQL Server failed to start within $timeout seconds"
        }
        
        Write-Host "Waiting for SQL Server... ($elapsed/$timeout seconds)" -ForegroundColor Yellow
    } while ($true)
    
    # Update connection string for Docker SQL Server
    $script:SqlConnectionString = "Server=localhost,1433;Database=$DatabaseName;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;Encrypt=false"
    
    return $containerName
}

# Function to cleanup Docker SQL Server
function Stop-SqlServerDocker {
    param([string]$ContainerName)
    
    if ($ContainerName) {
        Write-Host "Stopping SQL Server container..." -ForegroundColor Yellow
        docker rm -f $ContainerName 2>$null
    }
}

# Cleanup function
function Cleanup {
    param(
        [int]$AppProcessId,
        [string]$SqlContainer
    )
    
    Write-Host "`nCleaning up..." -ForegroundColor Yellow
    
    # Stop application
    Stop-AppProcess -ProcessId $AppProcessId
    
    # Stop Docker SQL Server if used
    if ($UseDocker -and $SqlContainer) {
        Stop-SqlServerDocker -ContainerName $SqlContainer
    }
    
    # Kill any remaining dotnet processes for this app
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | 
        Where-Object { $_.MainModule.FileName -like "*ai-stock-trade-app*" } |
        ForEach-Object { 
            Write-Host "Killing remaining dotnet process: $($_.Id)" -ForegroundColor Yellow
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue 
        }
}

# Variables for cleanup
$appProcess = $null
$sqlContainer = $null

try {
    # Setup SQL Server if using Docker
    if ($UseDocker) {
        $sqlContainer = Start-SqlServerDocker
    }
    
    # Set environment variables
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    $env:ASPNETCORE_URLS = "http://localhost:$Port"
    $env:ConnectionStrings__DefaultConnection = $SqlConnectionString
    
    Write-Host "Environment Variables:" -ForegroundColor Cyan
    Write-Host "  ASPNETCORE_ENVIRONMENT: $env:ASPNETCORE_ENVIRONMENT" -ForegroundColor Gray
    Write-Host "  ASPNETCORE_URLS: $env:ASPNETCORE_URLS" -ForegroundColor Gray
    Write-Host "  ConnectionStrings__DefaultConnection: $($SqlConnectionString -replace 'Password=[^;]+', 'Password=***')" -ForegroundColor Gray
    Write-Host ""
    
    # Navigate to app directory
    Push-Location $AppDir
    
    try {
        # Build application (unless skipped)
        if (-not $SkipBuild) {
            Write-Host "Building application..." -ForegroundColor Cyan
            dotnet build --configuration Release
            if ($LASTEXITCODE -ne 0) {
                throw "Application build failed"
            }
        }
        else {
            Write-Host "Skipping build (--SkipBuild specified)" -ForegroundColor Yellow
        }
        
        # Install EF tools if needed
        Write-Host "Ensuring Entity Framework tools are available..." -ForegroundColor Cyan
        dotnet tool install --global dotnet-ef 2>$null
        
        # Clean database if requested
        if ($CleanDatabase) {
            Write-Host "Cleaning database..." -ForegroundColor Cyan
            try {
                dotnet ef database drop --force 2>$null
            }
            catch {
                Write-Host "Database drop failed (may not exist): $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }
        
        # Run database migrations
        Write-Host "Running database migrations..." -ForegroundColor Cyan
        dotnet ef database update --verbose
        if ($LASTEXITCODE -ne 0) {
            throw "Database migration failed"
        }
        
        # Start application in background
        Write-Host "Starting application..." -ForegroundColor Cyan
        $appProcess = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--no-build", "--configuration", "Release", "--no-launch-profile") -PassThru -WorkingDirectory $AppDir
        
        if (-not $appProcess) {
            throw "Failed to start application process"
        }
        
        Write-Host "Application started with PID: $($appProcess.Id)" -ForegroundColor Green
        
        # Wait for application to be ready
        Write-Host "Waiting for application to be ready..." -ForegroundColor Cyan
        $baseUrl = "http://localhost:$Port"
        $timeout = 60
        $elapsed = 0
        
        do {
            Start-Sleep -Seconds 2
            $elapsed += 2
            
            try {
                $response = Invoke-WebRequest -Uri $baseUrl -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
                if ($response.StatusCode -eq 200) {
                    Write-Host "Application is ready!" -ForegroundColor Green
                    break
                }
            }
            catch {
                # Continue waiting
            }
            
            # Check if process is still running
            if ($appProcess.HasExited) {
                throw "Application process exited unexpectedly (Exit Code: $($appProcess.ExitCode))"
            }
            
            if ($elapsed -ge $timeout) {
                throw "Application failed to start within $timeout seconds"
            }
            
            Write-Host "Waiting for application... ($elapsed/$timeout seconds)" -ForegroundColor Yellow
        } while ($true)
        
        # Navigate to UI test directory
        Pop-Location
        Push-Location $UITestDir
        
        # Install Playwright if needed
        Write-Host "Installing Playwright browsers..." -ForegroundColor Cyan
        npx playwright install --with-deps 2>$null
        
        # Set Playwright environment variables
        $env:PLAYWRIGHT_BASE_URL = $baseUrl
        
        Write-Host "`nPlaywright Environment:" -ForegroundColor Cyan
        Write-Host "  PLAYWRIGHT_BASE_URL: $env:PLAYWRIGHT_BASE_URL" -ForegroundColor Gray
        Write-Host ""
        
        # List tests that will be executed
        Write-Host "UI Tests to be executed:" -ForegroundColor Cyan
        dotnet test --list-tests --verbosity normal --filter "FullyQualifiedName~UITests"
        Write-Host ""
        
        # Run UI tests
        Write-Host "Starting UI test execution..." -ForegroundColor Green
        Write-Host "Test timeout: $TimeoutMinutes minutes" -ForegroundColor Yellow
        Write-Host ""
        
        $testArgs = @(
            "test",
            "--no-build",
            "--configuration", "Release",
            "--verbosity", "normal",
            "--filter", "FullyQualifiedName~UITests",
            "--logger", "console;verbosity=detailed",
            "--logger", "trx;LogFileName=ui-test-results.trx",
            "--settings", (Join-Path $ProjectRoot "test.runsettings")
        )
        
        # Start UI tests with timeout
        $testProcess = Start-Process -FilePath "dotnet" -ArgumentList $testArgs -PassThru -WorkingDirectory $UITestDir
        
        # Wait for tests to complete with timeout
        $testCompleted = $testProcess.WaitForExit($TimeoutMinutes * 60 * 1000)
        
        if (-not $testCompleted) {
            Write-Host "UI tests timed out after $TimeoutMinutes minutes" -ForegroundColor Red
            $testProcess.Kill()
            throw "UI tests exceeded timeout of $TimeoutMinutes minutes"
        }
        
        $testExitCode = $testProcess.ExitCode
        
        Write-Host ""
        Write-Host "UI test execution completed with exit code: $testExitCode" -ForegroundColor $(if ($testExitCode -eq 0) { "Green" } else { "Red" })
        
        # Display test results if available
        $testResultsFile = Join-Path $UITestDir "TestResults\ui-test-results.trx"
        if (Test-Path $testResultsFile) {
            Write-Host ""
            Write-Host "Test results saved to: $testResultsFile" -ForegroundColor Cyan
        }
        
        if ($testExitCode -ne 0) {
            throw "UI tests failed with exit code: $testExitCode"
        }
        
        Write-Host ""
        Write-Host "All UI tests passed successfully!" -ForegroundColor Green
        
    }
    finally {
        Pop-Location
    }
}
catch {
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    
    # Show application logs if available
    if ($appProcess -and -not $appProcess.HasExited) {
        Write-Host ""
        Write-Host "Application is still running. Check logs manually if needed." -ForegroundColor Yellow
    }
    
    exit 1
}
finally {
    # Cleanup
    Cleanup -AppProcessId $appProcess.Id -SqlContainer $sqlContainer
    
    Write-Host ""
    Write-Host "Cleanup completed." -ForegroundColor Green
}

Write-Host ""
Write-Host "=== UI Test Execution Completed Successfully ===" -ForegroundColor Green
