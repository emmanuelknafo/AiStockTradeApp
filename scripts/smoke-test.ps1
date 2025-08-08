#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Runs basic smoke tests for the AI Stock Trade application (minimal version to avoid Playwright serialization issues)

.DESCRIPTION
    This script performs minimal UI testing to verify the application works without triggering Playwright serialization stack overflows.
    It focuses on basic functionality and avoids complex page object operations.

.PARAMETER TimeoutMinutes
    Timeout for the test run in minutes (default: 10)

.PARAMETER SkipBuild
    Skip the build step and run tests immediately

.PARAMETER Verbose
    Enable verbose output

.EXAMPLE
    .\smoke-test.ps1
    .\smoke-test.ps1 -TimeoutMinutes 5 -Verbose
#>

param(
    [int]$TimeoutMinutes = 10,
    [switch]$SkipBuild,
    [switch]$Verbose
)

# Error handling
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Global variables
$script:appProcess = $null
$script:sqlServerProcess = $null
$script:appPort = 5000
$script:appUrl = "http://localhost:$appPort"
$script:testProject = "ai-stock-trade-app.UITests"

function Write-Status {
    param([string]$Message, [string]$Status = "Info")
    $timestamp = Get-Date -Format "HH:mm:ss"
    $color = switch ($Status) {
        "Success" { "Green" }
        "Warning" { "Yellow" }
        "Error" { "Red" }
        default { "Cyan" }
    }
    Write-Host "[$timestamp] $Message" -ForegroundColor $color
}

function Test-CommandExists {
    param([string]$Command)
    try {
        Get-Command $Command -ErrorAction Stop | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Start-LocalDatabase {
    Write-Status "Setting up LocalDB..."
    
    if (-not (Test-CommandExists "sqllocaldb")) {
        Write-Status "SqlLocalDB not found. Please install SQL Server Express LocalDB." "Error"
        throw "SqlLocalDB is required but not installed"
    }

    try {
        # Create or start LocalDB instance
        $output = sqllocaldb info MSSQLLocalDB 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Status "Creating LocalDB instance..."
            sqllocaldb create MSSQLLocalDB
        }

        Write-Status "Starting LocalDB instance..."
        sqllocaldb start MSSQLLocalDB

        # Set connection string
        $env:ConnectionStrings__DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=StockTrackerTestDb;Trusted_Connection=true;MultipleActiveResultSets=true"
        Write-Status "LocalDB started and connection string set" "Success"
    }
    catch {
        Write-Status "Failed to setup LocalDB: $_" "Error"
        throw
    }
}

function Start-Application {
    Write-Status "Starting application on port $appPort..."
    
    try {
        # Update launch settings for test port
        $launchSettingsPath = "ai-stock-trade-app\Properties\launchSettings.json"
        if (Test-Path $launchSettingsPath) {
            $launchSettings = Get-Content $launchSettingsPath | ConvertFrom-Json
            if ($launchSettings.profiles.http) {
                $launchSettings.profiles.http.applicationUrl = $appUrl
                $launchSettings | ConvertTo-Json -Depth 10 | Set-Content $launchSettingsPath
            }
        }

        # Set environment variables
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $env:ASPNETCORE_URLS = $appUrl

        # Start application in background
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = "dotnet"
        $startInfo.Arguments = "run --project ai-stock-trade-app\ai-stock-trade-app.csproj"
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.CreateNoWindow = $true

        $script:appProcess = [System.Diagnostics.Process]::Start($startInfo)
        
        Write-Status "Application started with PID: $($script:appProcess.Id)"

        # Wait for application to be ready
        $maxWaitTime = 60
        $waitTime = 0
        $ready = $false

        Write-Status "Waiting for application to be ready..."
        while ($waitTime -lt $maxWaitTime -and -not $ready) {
            try {
                $response = Invoke-WebRequest -Uri $appUrl -Method GET -TimeoutSec 5 -ErrorAction SilentlyContinue
                if ($response.StatusCode -eq 200) {
                    $ready = $true
                    Write-Status "Application is ready and responding" "Success"
                }
            }
            catch {
                Start-Sleep -Seconds 2
                $waitTime += 2
            }
        }

        if (-not $ready) {
            throw "Application did not become ready within $maxWaitTime seconds"
        }
    }
    catch {
        Write-Status "Failed to start application: $_" "Error"
        throw
    }
}

function Invoke-SmokeTests {
    Write-Status "Running basic smoke tests..."
    
    try {
        # Build test project if not skipping
        if (-not $SkipBuild) {
            Write-Status "Building test project..."
            dotnet build $testProject --configuration Release --verbosity minimal
            if ($LASTEXITCODE -ne 0) {
                throw "Build failed"
            }
        }

        # Run only the basic smoke tests
        $testFilter = "FullyQualifiedName~BasicSmokeTest"
        $testTimeout = $TimeoutMinutes * 60 * 1000  # Convert to milliseconds
        
        Write-Status "Running smoke tests with $TimeoutMinutes minute timeout..."
        
        $testArgs = @(
            "test", $testProject
            "--configuration", "Release"
            "--logger", "console;verbosity=normal"
            "--filter", $testFilter
            "--no-build"
        )
        
        if ($Verbose) {
            $testArgs += @("--verbosity", "detailed")
        }

        # Set test environment variables
        $env:BaseUrl = $appUrl
        $env:TestTimeout = $testTimeout

        $testProcess = Start-Process -FilePath "dotnet" -ArgumentList $testArgs -Wait -PassThru -NoNewWindow
        
        if ($testProcess.ExitCode -eq 0) {
            Write-Status "Smoke tests completed successfully" "Success"
            return $true
        } else {
            Write-Status "Smoke tests failed with exit code: $($testProcess.ExitCode)" "Error"
            return $false
        }
    }
    catch {
        Write-Status "Error running smoke tests: $_" "Error"
        throw
    }
}

function Stop-Application {
    if ($script:appProcess -and -not $script:appProcess.HasExited) {
        Write-Status "Stopping application..."
        try {
            $script:appProcess.Kill($true)
            $script:appProcess.WaitForExit(5000)
            Write-Status "Application stopped" "Success"
        }
        catch {
            Write-Status "Error stopping application: $_" "Warning"
        }
    }
}

function Stop-LocalDatabase {
    try {
        Write-Status "Stopping LocalDB..."
        sqllocaldb stop MSSQLLocalDB
        Write-Status "LocalDB stopped" "Success"
    }
    catch {
        Write-Status "Error stopping LocalDB: $_" "Warning"
    }
}

function Invoke-Cleanup {
    Write-Status "Performing cleanup..."
    Stop-Application
    Stop-LocalDatabase
    Write-Status "Cleanup completed"
}

# Main execution
try {
    Write-Status "Starting AI Stock Trade App smoke test run" "Success"
    Write-Status "Configuration: Timeout=$TimeoutMinutes min, SkipBuild=$SkipBuild, Verbose=$Verbose"

    # Setup cleanup trap
    trap { Invoke-Cleanup; break }

    # Prerequisites check
    Write-Status "Checking prerequisites..."
    $requiredCommands = @("dotnet", "sqllocaldb")
    foreach ($cmd in $requiredCommands) {
        if (-not (Test-CommandExists $cmd)) {
            throw "$cmd is required but not found in PATH"
        }
    }

    # Setup database
    Start-LocalDatabase

    # Start application
    Start-Application

    # Run smoke tests
    $testSuccess = Invoke-SmokeTests

    if ($testSuccess) {
        Write-Status "=== SMOKE TEST RUN COMPLETED SUCCESSFULLY ===" "Success"
        exit 0
    } else {
        Write-Status "=== SMOKE TEST RUN FAILED ===" "Error"
        exit 1
    }
}
catch {
    Write-Status "Fatal error: $_" "Error"
    exit 1
}
finally {
    Invoke-Cleanup
}
