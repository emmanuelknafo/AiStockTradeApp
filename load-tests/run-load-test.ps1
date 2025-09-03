# AI Stock Trading API Load Test Runner
# PowerShell script to execute various load testing scenarios

param(
    [string]$TestType = "locust",           # locust, jmeter, or both
    [string]$Environment = "local",         # local, development, or production
    [int]$Users = 50,                      # Number of virtual users
    [int]$Duration = 300,                  # Test duration in seconds
    [string]$TargetHost = "localhost",     # Target host
    [int]$Port = 5001,                     # Target port
    [string]$Protocol = "https",           # http or https
    [switch]$Html,                         # Generate HTML report
    [switch]$NoWeb,                        # Run without web UI
    [string]$OutputDir = "test-results"    # Output directory
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Configuration
$LoadTestDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $LoadTestDir

Write-Host "=== AI Stock Trading API Load Test Runner ===" -ForegroundColor Green
Write-Host "Test Type: $TestType" -ForegroundColor Yellow
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "Target: ${Protocol}://${TargetHost}:${Port}" -ForegroundColor Yellow
Write-Host "Users: $Users, Duration: ${Duration}s" -ForegroundColor Yellow

# Create output directory
$ResultsPath = Join-Path $LoadTestDir $OutputDir
if (-not (Test-Path $ResultsPath)) {
    New-Item -ItemType Directory -Path $ResultsPath -Force | Out-Null
}

# Set environment variables
$env:HOST = $TargetHost
$env:PORT = $Port
$env:PROTOCOL = $Protocol
$env:VIRTUAL_USERS = $Users
$env:DURATION = $Duration

# Environment-specific configurations
switch ($Environment) {
    "local" {
        $BaseUrl = "${Protocol}://${TargetHost}:${Port}"
        Write-Host "Using local environment: $BaseUrl" -ForegroundColor Cyan
        if ($Protocol -ieq 'https') {
            $env:VERIFY_SSL = 'false'
            Write-Host "Locust SSL verification disabled for local HTTPS (VERIFY_SSL=$env:VERIFY_SSL)" -ForegroundColor DarkGray
        }
    }
    "development" {
        $BaseUrl = "https://dev-stockapi.azurewebsites.net"
        Write-Host "Using development environment: $BaseUrl" -ForegroundColor Cyan
    }
    "production" {
        $BaseUrl = "https://stockapi.azurewebsites.net"
        Write-Host "Using production environment: $BaseUrl" -ForegroundColor Cyan
        Write-Warning "Running load tests against production! Proceed with caution."
        $confirmation = Read-Host "Are you sure you want to test production? (y/N)"
        if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
            Write-Host "Test cancelled." -ForegroundColor Red
            exit 1
        }
    }
}

function Test-Prerequisites {
    Write-Host "Checking prerequisites..." -ForegroundColor Cyan
    
    # Check if target API is reachable
    try {
        $healthUrl = "$BaseUrl/health"
        Write-Host "Testing API health endpoint: $healthUrl" -ForegroundColor Gray
        $response = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 10
        Write-Host "✓ API is reachable" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Warning "Cannot reach API health endpoint: $healthUrl"
        Write-Warning "Error: $($_.Exception.Message)"
        
        # If testing localhost, offer to start the API
        if ($Environment -eq "local" -and ($TargetHost -eq "localhost" -or $TargetHost -eq "127.0.0.1")) {
            Write-Host ""
            Write-Host "It looks like you're testing against localhost but the API isn't running." -ForegroundColor Yellow
            
            # Check if we can find the API project
            $apiProjectPath = Find-ApiProject
            if ($apiProjectPath) {
                Write-Host "Found API project at: $apiProjectPath" -ForegroundColor Cyan
                $startApi = Read-Host "Would you like me to start the API in a new window? (Y/n)"
                
                if ($startApi -eq '' -or $startApi -eq 'y' -or $startApi -eq 'Y') {
                    Start-ApiInNewWindow -ProjectPath $apiProjectPath
                    
                    # Wait for API to start
                    Write-Host "Waiting for API to start..." -ForegroundColor Yellow
                    $retries = 0
                    $maxRetries = 30
                    
                    do {
                        Start-Sleep -Seconds 2
                        $retries++
                        Write-Host "." -NoNewline -ForegroundColor Gray
                        
                        try {
                            $response = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 5 -ErrorAction Stop
                            Write-Host ""
                            Write-Host "✓ API is now running!" -ForegroundColor Green
                            return $true
                        }
                        catch {
                            # Continue waiting
                        }
                    } while ($retries -lt $maxRetries)
                    
                    Write-Host ""
                    Write-Warning "API didn't start within expected time. Please check the API window for errors."
                }
            }
            else {
                Write-Host "Could not locate the API project automatically." -ForegroundColor Yellow
                Write-Host "Please start the API manually:" -ForegroundColor Cyan
                Write-Host "  cd AiStockTradeApp.Api" -ForegroundColor Gray
                Write-Host "  dotnet run" -ForegroundColor Gray
            }
        }
        
        Write-Host ""
        $continue = Read-Host "Continue with load test anyway? (y/N)"
        if ($continue -ne 'y' -and $continue -ne 'Y') {
            Write-Host "Load test cancelled. Please ensure the API is running and try again." -ForegroundColor Red
            exit 1
        }
        return $false
    }
}

function Find-ApiProject {
    # Look for the API project in common locations
    $possiblePaths = @(
        (Join-Path $ProjectRoot "AiStockTradeApp.Api"),
        (Join-Path $ProjectRoot "..\AiStockTradeApp.Api"),
        (Join-Path $ProjectRoot "..\..\AiStockTradeApp.Api")
    )
    
    foreach ($path in $possiblePaths) {
        $csprojPath = Join-Path $path "AiStockTradeApp.Api.csproj"
        if (Test-Path $csprojPath) {
            return $path
        }
    }
    
    # Also try to find any .csproj file that looks like an API project
    $searchPaths = @($ProjectRoot, (Split-Path $ProjectRoot))
    foreach ($searchPath in $searchPaths) {
        $apiProjects = Get-ChildItem -Path $searchPath -Recurse -Name "*.Api.csproj" -ErrorAction SilentlyContinue
        if ($apiProjects) {
            $projectPath = Split-Path (Join-Path $searchPath $apiProjects[0])
            return $projectPath
        }
    }
    
    return $null
}

function Start-ApiInNewWindow {
    param([string]$ProjectPath)
    
    try {
        Write-Host "Starting API in new window..." -ForegroundColor Green
        
        # Determine the correct port for the API
        $apiPort = if ($Port -eq 5001) { 5001 } else { 5000 }
        $apiUrl = "${Protocol}://localhost:${apiPort}"
        
        # Check if we're in VS Code and can use a task instead
        if ($env:VSCODE_PID) {
            Write-Host "Detected VS Code environment. You can also use the built-in task:" -ForegroundColor Cyan
            Write-Host "  Ctrl+Shift+P → 'Tasks: Run Task' → 'Build and Run Stock Tracker'" -ForegroundColor Gray
            Write-Host ""
        }
        
        # Create a PowerShell command to run the API
    $apiCommand = @"
Write-Host "Starting AI Stock Trading API..." -ForegroundColor Green
Write-Host "Project Path: $ProjectPath" -ForegroundColor Cyan
Write-Host "API URL: $apiUrl" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Ctrl+C to stop the API when load testing is complete." -ForegroundColor Yellow
Write-Host ""
Set-Location "$ProjectPath"

# Ensure API uses in-memory DB for load tests
`$env:USE_INMEMORY_DB = 'true'
Write-Host "USE_INMEMORY_DB=`$env:USE_INMEMORY_DB" -ForegroundColor Gray

dotnet run --urls="$apiUrl"
Write-Host ""
Write-Host "API has stopped. Press any key to close this window..." -ForegroundColor Yellow
Read-Host
"@
        
        # Start new PowerShell window with the API
        # Try PowerShell Core first, fallback to Windows PowerShell
        $pwshPath = $null
        $pwshPaths = @("pwsh.exe", "powershell.exe")
        
        foreach ($path in $pwshPaths) {
            try {
                $null = Get-Command $path -ErrorAction Stop
                $pwshPath = $path
                break
            }
            catch {
                # Continue to next option
            }
        }
        
        if (-not $pwshPath) {
            Write-Warning "Neither PowerShell Core (pwsh) nor Windows PowerShell found in PATH"
            return $false
        }
        
        Write-Host "Using: $pwshPath" -ForegroundColor Gray
        
        $processArgs = @{
            FilePath = $pwshPath
            ArgumentList = @("-NoExit", "-Command", $apiCommand)
            PassThru = $true
        }
        
        $apiProcess = Start-Process @processArgs
        
        Write-Host "API started in process ID: $($apiProcess.Id)" -ForegroundColor Green
        Write-Host "You can close the API window when done testing." -ForegroundColor Yellow
        
        return $true
    }
    catch {
        Write-Warning "Failed to start API: $($_.Exception.Message)"
        return $false
    }
}

function Run-LocustTest {
    Write-Host "Running Locust load test..." -ForegroundColor Green
    
    # Check if Python and Locust are available
    try {
        $pythonVersion = python --version 2>&1
        Write-Host "Using Python: $pythonVersion" -ForegroundColor Gray
    }
    catch {
        Write-Error "Python is not installed or not in PATH"
        return
    }
    
    try {
        $locustVersion = locust --version 2>&1
        Write-Host "Using Locust: $locustVersion" -ForegroundColor Gray
    }
    catch {
        Write-Host "Installing Locust dependencies..." -ForegroundColor Yellow
        python -m pip install -r "$LoadTestDir\requirements.txt"
    }
    
    # Run Locust test
    $LocustArgs = @(
        "--host=$BaseUrl"
        "--users=$Users"
        "--spawn-rate=5"
        "--run-time=${Duration}s"
        "--headless"
        "--html=$ResultsPath\locust-report.html"
        "--csv=$ResultsPath\locust-stats"
        "--logfile=$ResultsPath\locust.log"
        "--loglevel=INFO"
    )
    
    if ($NoWeb) {
        $LocustArgs += "--headless"
    }
    
    $LocustFile = Join-Path $LoadTestDir "locust\api_load_test.py"
    if (-not (Test-Path $LocustFile)) {
        $LocustFile = Join-Path $LoadTestDir "locustfile.py"
    }
    
    Write-Host "Starting Locust with: locust -f $LocustFile $($LocustArgs -join ' ')" -ForegroundColor Gray
    
    Set-Location $LoadTestDir
    locust -f $LocustFile @LocustArgs
    
    Write-Host "✓ Locust test completed" -ForegroundColor Green
    Write-Host "Results saved to: $ResultsPath" -ForegroundColor Cyan
}

function Run-JMeterTest {
    Write-Host "Running JMeter load test..." -ForegroundColor Green
    
    # Check if JMeter is available
    try {
        $jmeterVersion = jmeter --version 2>&1
        Write-Host "Using JMeter: $jmeterVersion" -ForegroundColor Gray
    }
    catch {
        Write-Error "JMeter is not installed or not in PATH"
        Write-Host "Please install JMeter and add it to your PATH" -ForegroundColor Yellow
        Write-Host "Download from: https://jmeter.apache.org/download_jmeter.cgi" -ForegroundColor Yellow
        return
    }
    
    # JMeter test files
    $JMeterFile = Join-Path $LoadTestDir "jmeter\comprehensive-api-test.jmx"
    if (-not (Test-Path $JMeterFile)) {
        $JMeterFile = Join-Path $LoadTestDir "jmeter-script.jmx"
    }
    
    if (-not (Test-Path $JMeterFile)) {
        Write-Error "JMeter test file not found: $JMeterFile"
        return
    }
    
    # JMeter arguments
    $JMeterArgs = @(
        "-n"  # Non-GUI mode
        "-t", $JMeterFile
        "-l", "$ResultsPath\jmeter-results.jtl"
        "-e"  # Generate HTML dashboard
        "-o", "$ResultsPath\jmeter-dashboard"
        "-JHOST=$TargetHost"
        "-JPORT=$Port"
        "-JPROTOCOL=$Protocol"
        "-JTHREADS=$Users"
        "-JDURATION=$Duration"
        "-JRAMP_TIME=60"
    )
    
    Write-Host "Starting JMeter with: jmeter $($JMeterArgs -join ' ')" -ForegroundColor Gray
    
    # Remove existing dashboard directory
    $DashboardPath = "$ResultsPath\jmeter-dashboard"
    if (Test-Path $DashboardPath) {
        Remove-Item $DashboardPath -Recurse -Force
    }
    
    jmeter @JMeterArgs
    
    Write-Host "✓ JMeter test completed" -ForegroundColor Green
    Write-Host "Results saved to: $ResultsPath" -ForegroundColor Cyan
    Write-Host "Dashboard available at: $DashboardPath\index.html" -ForegroundColor Cyan
}

function Generate-Summary {
    Write-Host "Generating test summary..." -ForegroundColor Green
    
    $SummaryPath = Join-Path $ResultsPath "test-summary.txt"
    $Summary = @"
AI Stock Trading API Load Test Summary
=====================================

Test Configuration:
- Test Type: $TestType
- Environment: $Environment
- Target URL: $BaseUrl
- Virtual Users: $Users
- Duration: ${Duration} seconds
- Timestamp: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

Files Generated:
"@
    
    if ($TestType -eq "locust" -or $TestType -eq "both") {
        $Summary += @"

Locust Results:
- HTML Report: locust-report.html
- CSV Stats: locust-stats_*.csv
- Log File: locust.log
"@
    }
    
    if ($TestType -eq "jmeter" -or $TestType -eq "both") {
        $Summary += @"

JMeter Results:
- Results File: jmeter-results.jtl
- HTML Dashboard: jmeter-dashboard/index.html
"@
    }
    
    $Summary += @"

Next Steps:
1. Review the generated reports for performance metrics
2. Check error rates and response times
3. Analyze bottlenecks and optimization opportunities
4. Compare results across different test runs

For Azure Load Testing:
- Upload test files to Azure Load Testing service
- Configure monitoring with Application Insights
- Set up automated test runs in CI/CD pipeline
"@
    
    $Summary | Out-File -FilePath $SummaryPath -Encoding UTF8
    Write-Host "✓ Summary saved to: $SummaryPath" -ForegroundColor Green
}

# Main execution
try {
    $apiRunning = Test-Prerequisites
    
    if (-not $apiRunning -and $Environment -eq "local") {
        Write-Host ""
        Write-Host "💡 Tip: For best results, ensure your API is running before load testing." -ForegroundColor Cyan
        Write-Host "   You can also use VS Code tasks or run manually:" -ForegroundColor Gray
        Write-Host "   dotnet run --project AiStockTradeApp.Api --urls=https://localhost:5001" -ForegroundColor Gray
        Write-Host ""
    }
    
    switch ($TestType.ToLower()) {
        "locust" {
            Run-LocustTest
        }
        "jmeter" {
            Run-JMeterTest
        }
        "both" {
            Run-LocustTest
            Write-Host "`n" -ForegroundColor Gray
            Run-JMeterTest
        }
        default {
            Write-Error "Invalid test type: $TestType. Use 'locust', 'jmeter', or 'both'"
            exit 1
        }
    }
    
    Generate-Summary
    
    Write-Host "`n=== Load Test Completed Successfully ===" -ForegroundColor Green
    Write-Host "Results available in: $ResultsPath" -ForegroundColor Cyan
    
    if ($Html) {
        Write-Host "Opening HTML reports..." -ForegroundColor Yellow
        
        $LocustReport = Join-Path $ResultsPath "locust-report.html"
        if (Test-Path $LocustReport) {
            Start-Process $LocustReport
        }
        
        $JMeterDashboard = Join-Path $ResultsPath "jmeter-dashboard\index.html"
        if (Test-Path $JMeterDashboard) {
            Start-Process $JMeterDashboard
        }
    }
    
    # Show next steps
    Write-Host ""
    Write-Host "🎯 Next Steps:" -ForegroundColor Cyan
    Write-Host "• Review the generated reports for performance insights" -ForegroundColor Gray
    Write-Host "• Check error rates and response time distributions" -ForegroundColor Gray
    Write-Host "• Monitor resource usage during tests" -ForegroundColor Gray
    Write-Host "• Compare results with previous test runs" -ForegroundColor Gray
    if ($Environment -eq "local") {
        Write-Host "• Consider testing against staging/production environments" -ForegroundColor Gray
    }
}
catch {
    Write-Error "Load test failed: $($_.Exception.Message)"
    Write-Host ""
    Write-Host "🔧 Troubleshooting:" -ForegroundColor Yellow
    Write-Host "• Ensure the API is running and accessible" -ForegroundColor Gray
    Write-Host "• Check firewall and network connectivity" -ForegroundColor Gray
    Write-Host "• Verify SSL certificates for HTTPS endpoints" -ForegroundColor Gray
    Write-Host "• Review the error message above for specific issues" -ForegroundColor Gray
    exit 1
}
finally {
    # Cleanup
    if ($LoadTestDir -ne (Get-Location).Path) {
        Set-Location $ProjectRoot
    }
}
