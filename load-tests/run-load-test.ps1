# AI Stock Trading API Load Test Runner
# PowerShell script to execute various load testing scenarios

[CmdletBinding()]
param(
    [string]$TestType = "locust",           # locust, jmeter, or both
    [string]$Environment = "local",         # local, development, or production
    [int]$Users = 50,                        # Number of virtual users
    [int]$Duration = 300,                    # Test duration in seconds
    [string]$TargetHost = "localhost",       # Target host
    [int]$Port = 5256,                       # Default local HTTP port (align with API_URL in CI)
    [string]$Protocol = "http",             # default to http for local ease
    [switch]$Html,                           # Generate HTML report
    [switch]$NoWeb,                          # Run without web UI
    [switch]$AutoStart,                      # Auto-start API if not reachable (local only)
    [switch]$ForceKill,                      # Force kill any processes bound to target port (no prompt)
    [string]$OutputDir = "test-results"      # Output directory
)

# Enforce strict mode & error preference for safer execution
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Configuration
$LoadTestDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $LoadTestDir

Write-Host "=== AI Stock Trading API Load Test Runner ===" -ForegroundColor Green
Write-Host "Test Type: $TestType" -ForegroundColor Yellow
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "Target: ${Protocol}://${TargetHost}:${Port}" -ForegroundColor Yellow
Write-Host "Users: $Users, Duration: ${Duration}s" -ForegroundColor Yellow

# For local environment default AutoStart to true if not explicitly set
if ($Environment -eq 'local' -and -not $PSBoundParameters.ContainsKey('AutoStart')) { $AutoStart = $true }

# Proactive clean slate: kill ALL existing dotnet processes when targeting local environment
if ($Environment -eq 'local' -and ($TargetHost -eq 'localhost' -or $TargetHost -eq '127.0.0.1')) {
    Write-Host "Ensuring no stale dotnet processes are running (global clean slate)..." -ForegroundColor DarkCyan
    try {
        $dotnet = Get-Process -Name dotnet -ErrorAction SilentlyContinue
        if ($dotnet) {
            $pids = @()
            foreach ($p in $dotnet) {
                try {
                    Write-Host "Stopping dotnet PID=$($p.Id)" -ForegroundColor DarkYellow
                    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
                    try { Wait-Process -Id $p.Id -Timeout 5 -ErrorAction SilentlyContinue } catch {}
                    $pids += $p.Id
                } catch {}
            }
            # Secondary sweep (just in case)
            Start-Sleep -Milliseconds 500
            $residual = Get-Process -Name dotnet -ErrorAction SilentlyContinue
            if ($residual) {
                Write-Warning "Residual dotnet processes still detected: $($residual.Id -join ', '). Forcing another termination pass."
                foreach ($r in $residual) { try { Stop-Process -Id $r.Id -Force -ErrorAction SilentlyContinue } catch {} }
                Start-Sleep -Milliseconds 300
            }
            $final = Get-Process -Name dotnet -ErrorAction SilentlyContinue
            if ($final) { Write-Warning "Some dotnet processes remain (may be protected/system). Proceeding anyway." } else { Write-Host "Terminated dotnet processes: $($pids -join ', ')" -ForegroundColor Green }
        } else {
            Write-Host "No dotnet processes detected." -ForegroundColor DarkGray
        }
    } catch { Write-Warning "Global dotnet cleanup error: $($_.Exception.Message)" }
}

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


function Remove-ProcessesOnPort {
    param(
        [Parameter(Mandatory)][int]$Port,
        [switch]$Quiet
    , [switch]$Force
    )
    try {
        $pids = @()

        # Prefer modern cmdlet if available
        $getNet = Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue
        if ($getNet) {
            try {
                $pids = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue |
                Select-Object -ExpandProperty OwningProcess -Unique
            }
            catch {}
        }

        # Fallback to netstat parsing if needed
        if (-not $pids -or $pids.Count -eq 0) {
            $netstatOutput = netstat -ano | Select-String ":$Port\s" -ErrorAction SilentlyContinue
            foreach ($line in $netstatOutput) {
                $parts = ($line -split '\s+') | Where-Object { $_ }
                if ($parts.Count -gt 0) {
                    $candidate = $parts[-1]
                    if ($candidate -match '^[0-9]+$') { $pids += [int]$candidate }
                }
            }
            $pids = $pids | Sort-Object -Unique
        }

        if (-not $pids -or $pids.Count -eq 0) {
            if (-not $Quiet) { Write-Host "No processes found listening on port $Port" -ForegroundColor DarkGray }
            return
        }

        # Gather process details (best effort)
        $procDetails = @()
        foreach ($pp in $pids) {
            try {
                $gp = Get-Process -Id $pp -ErrorAction Stop
                $path = $null
                try { $path = ($gp.Path) } catch { }
                if (-not $path) { try { $path = (Get-Process -Id $pp -FileVersionInfo -ErrorAction SilentlyContinue).FileName } catch {} }
                $procDetails += [pscustomobject]@{ PID = $pp; Name = $gp.ProcessName; Path = $path }
            } catch {}
        }

        if (-not $Force -and -not $Quiet) {
            Write-Host "The following process(es) are using port ${Port}:" -ForegroundColor Yellow
            $procDetails | Format-Table -AutoSize | Out-String | Write-Host
            $confirm = Read-Host "Terminate these process(es)? (y/N)"
            if ($confirm -notin @('y','Y')) {
                Write-Host "Skipping termination of processes on port $Port" -ForegroundColor Yellow
                return
            }
        }

        foreach ($procPid in ($pids | Where-Object { $_ -and $_ -ne $PID })) {
            if (-not $Quiet) { Write-Host "Killing process PID $procPid using port $Port" -ForegroundColor DarkGray }
            try { taskkill /PID $procPid /F /T | Out-Null } catch {
                if (-not $Quiet) { Write-Warning "Failed to kill PID ${procPid}: $($_.Exception.Message)" }
            }
        }
    }
    catch {
        if (-not $Quiet) { Write-Warning "Failed to enumerate or terminate processes on port ${Port}: $($_.Exception.Message)" }
    }
}

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

    $healthUrl = "$BaseUrl/health"
    # Probe current API health
    try {
        Write-Host "Testing API health endpoint: $healthUrl" -ForegroundColor Gray
        $null = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 10
        Write-Host "✓ API is reachable" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Warning "Cannot reach API health endpoint: $healthUrl"
        Write-Warning "Error: $($_.Exception.Message)"

        $isLocalLoopback = ($Environment -eq "local" -and ($TargetHost -in @('localhost','127.0.0.1')))
        if ($isLocalLoopback -and $AutoStart) {
            Write-Host "AutoStart active: launching API automatically on port $Port..." -ForegroundColor Yellow
            $apiProjectPath = Find-ApiProject
            if (-not $apiProjectPath) { Write-Warning "AutoStart failed: API project not found."; return $false }

            $started = Start-ApiInNewWindow -ProjectPath $apiProjectPath -Port $Port -Protocol $Protocol -KillExisting
            if (-not $started) { Write-Warning "AutoStart could not start API."; return $false }

            # Wait for requested port health
            $maxAttempts = 40
            for ($i=0; $i -lt $maxAttempts; $i++) {
                Start-Sleep -Seconds 2
                try { $null = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 5 -ErrorAction Stop; Write-Host "✓ API is now running (requested port)." -ForegroundColor Green; return $true } catch {}
            }
            Write-Warning "Requested port $Port not healthy yet; attempting dynamic port detection."            
            try {
                $apiProc = Get-Process -Name dotnet -ErrorAction SilentlyContinue | Sort-Object StartTime -Descending | Where-Object {
                    try { (Get-CimInstance Win32_Process -Filter "ProcessId=$($_.Id)" | Select-Object -ExpandProperty CommandLine 2>$null) -match 'AiStockTradeApp.Api' } catch { $false }
                } | Select-Object -First 1
                if ($apiProc) {
                    $candidatePorts = @()
                    if (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue) {
                        $candidatePorts = Get-NetTCPConnection -OwningProcess $apiProc.Id -ErrorAction SilentlyContinue | Where-Object { $_.State -eq 'Listen' } | Select-Object -ExpandProperty LocalPort -Unique
                    }
                    if (-not $candidatePorts -or $candidatePorts.Count -eq 0) {
                        $netstat = netstat -ano | Select-String $apiProc.Id 2>$null
                        foreach ($l in $netstat) { if ($l -match ':(\d+)') { $candidatePorts += [int]$matches[1] } }
                        $candidatePorts = $candidatePorts | Sort-Object -Unique
                    }
                    $altPort = ($candidatePorts | Where-Object { $_ -ne $Port } | Select-Object -First 1)
                    if ($altPort) {
                        Write-Host "Detected API listening on alternate port $altPort; updating target." -ForegroundColor Yellow
                        $script:Port = $altPort; $script:BaseUrl = "${Protocol}://localhost:${altPort}"; $env:PORT = $altPort
                        $altHealth = "$($script:BaseUrl)/health"
                        for ($j=0; $j -lt 15; $j++) {
                            Start-Sleep -Seconds 2
                            try { $null = Invoke-RestMethod -Uri $altHealth -Method Get -TimeoutSec 5 -ErrorAction Stop; Write-Host "✓ API healthy on detected port $altPort" -ForegroundColor Green; return $true } catch {}
                        }
                        Write-Warning "Detected port $altPort didn't pass health in allotted time."; return $false
                    } else { Write-Warning "Dynamic detection found no alternative listening port." }
                } else { Write-Warning "Could not find API process for dynamic port detection." }
            } catch { Write-Warning "Dynamic port detection error: $($_.Exception.Message)" }
            return $false
        }
        elseif ($isLocalLoopback -and -not $AutoStart) {
            Write-Host "Local API not reachable and AutoStart disabled. Start API manually or enable -AutoStart." -ForegroundColor Yellow
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
    param(
        [string]$ProjectPath,
        [int]$Port = 0,
        [string]$Protocol = 'http',
        [switch]$KillExisting
    )

    try {
        Write-Host "Starting API in new window..." -ForegroundColor Green

        # Resolve port precedence: explicit param > script scope > env:PORT > default 5256
        if (-not $Port -or $Port -le 0) {
            if ($script:Port -and $script:Port -gt 0) { $Port = $script:Port }
            elseif ($env:PORT -and [int]::TryParse($env:PORT, [ref]([int]$null))) { try { $Port = [int]$env:PORT } catch { $Port = 5256 } }
            else { $Port = 5256 }
        }

        $apiPort = $Port
        $apiUrl = "${Protocol}://localhost:${apiPort}"
        if ($KillExisting) {
            Remove-ProcessesOnPort -Port $apiPort -Quiet:(!$ForceKill) -Force:$ForceKill
        }

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
            FilePath     = $pwshPath
            ArgumentList = @("-NoExit", "-Command", $apiCommand)
            PassThru     = $true
        }
        
        $apiProcess = Start-Process @processArgs

        Write-Host "API started in process ID: $($apiProcess.Id) on port $apiPort" -ForegroundColor Green
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
