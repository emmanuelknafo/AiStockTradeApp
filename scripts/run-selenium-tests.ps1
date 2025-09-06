<#!
.SYNOPSIS
  Helper to run Selenium UI tests locally (starts app, sets env vars, optional unskip of [Fact(Skip=...)] tests).

.DESCRIPTION
  1. Optionally patches test files to remove Skip attributes (unless you already removed them).
  2. Starts the application via existing start.ps1 in Local/Docker mode (API, UI, MCP) unless -SkipStart is supplied.
  3. Waits for the UI to become responsive (always performed, even with -SkipStart to verify availability).
  4. Exports SELENIUM_* environment variables used by the Selenium test harness.
  5. Executes dotnet test with optional category / ADO ID filtering.
  6. Restores original test files unless -KeepPatched is specified.

.PARAMETER Category
  Run only tests with matching Trait Category (Authenticated | Anonymous | CrossCutting).

.PARAMETER AdoId
  One or more ADO Test Case IDs separated by commas or pipe (e.g. "1403,1404").

.PARAMETER BaseUrl
  Override base UI URL (default https://localhost:7043 for Local mode).

.PARAMETER Username
  Username for authenticated tests (sets SELENIUM_USERNAME).

.PARAMETER Password
  Password for authenticated tests (sets SELENIUM_PASSWORD).

.PARAMETER Culture
  Culture to test (en or fr). Default en.

.PARAMETER Headless
  Run browser headless (default: $true). Set -Headless:$false for headed mode.

.PARAMETER EnableTests
  Remove Skip attributes temporarily so tests actually run.

.PARAMETER KeepPatched
  If supplied with -EnableTests, do not restore original files after run.

.PARAMETER Mode
  Startup mode for underlying app: Local or Docker (default Local). Docker implies base URL http://localhost:8080 unless overridden.

.PARAMETER SkipStart
  If provided, do NOT invoke start.ps1; instead reuse an already running instance at -BaseUrl.

.PARAMETER Configuration
  Build configuration (Debug or Release). Default: Release.

.PARAMETER NoBuild
  If set, attempts to run tests with --no-build (will fail if binaries not present or outdated).

.EXAMPLE
  ./scripts/run-selenium-tests.ps1 -EnableTests -Category Anonymous

.EXAMPLE
  ./scripts/run-selenium-tests.ps1 -EnableTests -AdoId 1403,1408 -Username devuser -Password P@ssw0rd!

#>
[CmdletBinding(PositionalBinding = $false)]
param(
  [ValidateSet('Authenticated', 'Anonymous', 'CrossCutting')]
  [string]$Category,

  [string]$AdoId,

  [string]$BaseUrl,

  [string]$Username,
  # Plain string password acceptable for ephemeral test user in CI; suppressing analyzer.
  [string]$Password,

  [ValidateSet('en', 'fr')]
  [string]$Culture = 'en',

  [bool]$Headless = $true,

  [switch]$EnableTests,
  [switch]$KeepPatched,

  [ValidateSet('Local', 'Docker')]
  [string]$Mode = 'Local'
  ,
  [switch]$SkipStart,
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Release',
  [switch]$NoBuild,
  [switch]$CI
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Auto-detect CI environment if switch not explicitly passed (GitHub Actions, generic CI, Azure DevOps TF_BUILD)
if (-not $CI -and ($env:GITHUB_ACTIONS -eq 'true' -or $env:CI -eq 'true' -or $env:TF_BUILD -eq 'True')) {
  $CI = $true
  Write-Host '[INFO ] CI environment detected via env vars (auto-enabling CI mode).' -ForegroundColor Cyan
}

function Write-Info([string]$msg) { Write-Host "[INFO ] $msg" -ForegroundColor Cyan }
function Write-Warn([string]$msg) { Write-Host "[WARN ] $msg" -ForegroundColor Yellow }
function Write-Err ([string]$msg) { Write-Host "[ERROR] $msg" -ForegroundColor Red }
function Write-Detail([string]$msg) { Write-Host "         $msg" -ForegroundColor DarkGray }

# Cross-platform termination of existing dotnet processes for a clean slate
function Stop-DotnetAppProcesses {
  param([string]$Reason = 'Pre-run cleanup')
  Write-Info "Killing existing dotnet processes ($Reason) ..."
  # Use a custom variable name to avoid conflict with automatic $IsLinux in newer PowerShell versions
  $platformIsLinux = $false
  try {
    if ($env:RUNNER_OS -eq 'Linux' -or [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) { $platformIsLinux = $true }
  }
  catch { $platformIsLinux = $false }
  if ($platformIsLinux) {
    try { & bash -c "pkill -f AiStockTradeApp || true" | Out-Null; Write-Detail 'pkill issued (Linux).' } catch { Write-Warn "pkill failed: $($_.Exception.Message)" }
  }
  else {
    try {
      $procs = Get-Process -Name dotnet -ErrorAction SilentlyContinue
      foreach ($p in $procs) {
        try {
          $cl = (Get-CimInstance Win32_Process -Filter "ProcessId=$($p.Id)" | Select-Object -ExpandProperty CommandLine 2>$null)
          if ($cl -and ($cl -match 'AiStockTradeApp')) {
            Write-Detail "Stopping dotnet PID=$($p.Id)"; Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
          }
        }
        catch { }
      }
    }
    catch { Write-Warn "Windows process enumeration failed: $($_.Exception.Message)" }
  }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot '..') | ForEach-Object { $_.Path }
$testsProj = Join-Path $repoRoot 'AiStockTradeApp.SeleniumTests/AiStockTradeApp.SeleniumTests.csproj'
$testsDir = Join-Path $repoRoot 'AiStockTradeApp.SeleniumTests/Tests'
$startScript = Join-Path $scriptRoot 'start.ps1'

if (-not (Test-Path $testsProj)) { Write-Err "Selenium test project not found: $testsProj"; exit 1 }
if (-not $SkipStart -and -not (Test-Path $startScript)) { Write-Err "start.ps1 not found at: $startScript"; exit 1 }

if ($Category -and $AdoId) { Write-Err 'Provide either -Category or -AdoId, not both.'; exit 1 }

# Determine default BaseUrl if not provided
if (-not $BaseUrl) {
  $BaseUrl = if ($Mode -eq 'Local') { 'https://localhost:7043' } else { 'http://localhost:8080' }
}

# In CI prefer HTTP endpoint to avoid dev cert trust issues (GitHub runners cannot trust cert easily)
if ($CI -and (-not $PSBoundParameters.ContainsKey('BaseUrl'))) {
  # UI launchSettings exposes HTTP on 5259 alongside HTTPS 7043
  $BaseUrl = 'http://localhost:5259'
  Write-Info "CI mode: overriding BaseUrl to $BaseUrl (HTTP to avoid dev cert trust warnings)."
}

Write-Info "Using BaseUrl: $BaseUrl"

$patchedFiles = @()
$backupSuffix = '.bak_selenium'

function Enable-Tests {
  Write-Info 'Patching test files to remove [Fact(Skip=...)] attributes...'
  Get-ChildItem -Path $testsDir -Filter *.cs -Recurse | ForEach-Object {
    $content = Get-Content -Raw -LiteralPath $_.FullName
    if ($content -match '\[Fact\(Skip\s*=') {
      $backup = "$($_.FullName)$backupSuffix"
      if (-not (Test-Path $backup)) { $content | Set-Content -LiteralPath $backup }
      $newContent = $content -replace '\[Fact\(Skip\s*=\s*"[^"]*"\)\]', '[Fact]'
      if ($newContent -ne $content) {
        $newContent | Set-Content -LiteralPath $_.FullName
        $patchedFiles += $_.FullName
        Write-Info "Unskipped: $($_.Name)"
      }
    }
  }
  if (-not $patchedFiles) { Write-Warn 'No files required patching (maybe already unskipped).' }
}

function Restore-PatchedFiles {
  if (-not $patchedFiles) { return }
  if ($KeepPatched) { Write-Warn 'Keeping patched test files (no restore).'; return }
  Write-Info 'Restoring original test files...'
  foreach ($f in $patchedFiles) {
    $backup = "$f$backupSuffix"
    if (Test-Path $backup) {
      Get-Content -Raw -LiteralPath $backup | Set-Content -LiteralPath $f
      Remove-Item $backup -Force
      Write-Info "Restored: $(Split-Path -Leaf $f)"
    }
  }
}

try {
  if ($CI) {
    Write-Info 'CI mode enabled: forcing headless, simulating autostart behavior.'
    $env:CI = 'true'
    if (-not $Headless) { $Headless = $true }
    $env:USE_INMEMORY_DB = 'true'
    Write-Info 'CI mode: USE_INMEMORY_DB=true set for API/UI.'
    # Simulate autostart: ensure orphaned processes are killed but do NOT disable MCP/selenium autostart flags (leave features logically enabled)
  Stop-DotnetAppProcesses -Reason 'CI autostart simulation pre-start'
    $env:CI_INLINE_LAUNCH = '1'
    Write-Info 'CI mode: CI_INLINE_LAUNCH=1 (inline processes; PIDs tracked).'
    $env:SELENIUM_AUTOSTART_MODE = 'enabled'
  }
  if ($EnableTests) { Enable-Tests }

  if (-not $SkipStart) {
    # Always enforce a clean slate before any start attempt (even outside CI)
    Stop-DotnetAppProcesses -Reason 'Pre-start enforcement'
    Write-Info "Starting application via start.ps1 (-Mode $Mode -NoBrowser) ..."
    if ($CI) {
      # Force API to pure HTTP profile, but use UI 'https' profile (with UseHttps disabled) so it also exposes HTTP on 5259
      # Launch settings: UI 'https' profile => https://7043;http://5259, API 'http' profile => http://5256
      # Avoid -NoExit in spawned shells to prevent hanging pipeline shells (consumed by Azure DevOps/CI)
      $env:NO_PWSH_NOEXIT = '1'
      & $startScript -Mode $Mode -NoBrowser -UseHttps:$false -ApiProfile http -UiProfile https | Out-Null
    }
    else {
      & $startScript -Mode $Mode -NoBrowser | Out-Null
    }
  }
  else {
    Write-Info "Skipping application startup (reusing existing instance at $BaseUrl)."
  }

  # UI readiness probe
  $timeoutSec = 120
  $pollSec = 3
  $deadline = (Get-Date).AddSeconds($timeoutSec)
  $uiReady = $false
  $attempt = 0
  $lastError = $null
  Write-Info "Waiting for UI to respond at $BaseUrl (timeout ${timeoutSec}s)..."
  while ((Get-Date) -lt $deadline) {
    $attempt++
    try {
      $resp = Invoke-WebRequest -Uri $BaseUrl -Method Get -TimeoutSec 5 -SkipCertificateCheck:$true -ErrorAction Stop
      if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 500) { $uiReady = $true; break }
      $lastError = "Unexpected status: $($resp.StatusCode)"
    }
    catch {
      $lastError = $_.Exception.Message
    }
    if ($attempt % 10 -eq 0) {
      Write-Warn "Still waiting (attempt $attempt). Last error/status: $lastError"
      # Fallback: if port is listening, proceed anyway (Azure DevOps sometimes blocks HTTP call inside readiness loop)
      try {
        $client = [System.Net.Sockets.TcpClient]::new()
        $ar = $client.BeginConnect('localhost', [int]([uri]$BaseUrl).Port, $null, $null)
        $success = $ar.AsyncWaitHandle.WaitOne(500)
        if ($success -and $client.Connected) {
          $client.EndConnect($ar)
          $client.Dispose()
          Write-Warn 'Port is open; proceeding despite HTTP readiness not confirmed.'
          $uiReady = $true; break
        }
        $client.Dispose()
      }
      catch { }
    }
    Start-Sleep -Seconds $pollSec
  }
  if (-not $uiReady) {
    Write-Err "UI not reachable within ${timeoutSec}s at $BaseUrl. Last error: $lastError"
    # Emit process list for diagnostics before exiting
    try { Get-Process -Name dotnet | Select-Object Id, StartTime, MainWindowTitle | Format-Table | Out-String | Write-Host } catch { }
    exit 1
  }
  Write-Info "UI is responsive after $attempt attempt(s)."

  # Export env vars for test process
  $env:SELENIUM_BASE_URL = $BaseUrl
  if ($Username) { $env:SELENIUM_USERNAME = $Username }
  if ($Password) { $env:SELENIUM_PASSWORD = $Password }
  $env:SELENIUM_CULTURE = $Culture
  $env:SELENIUM_HEADLESS = if ($Headless) { 'true' } else { 'false' }

  # -------------------------------------------------------------
  # Automatic Identity User + Watchlist Seeding (zero intervention)
  # -------------------------------------------------------------
  $userId = $null
  if ($env:USE_INMEMORY_DB -eq 'true') {
    Write-Info 'In-memory DB mode detected: skipping direct SQL seeding (UI will seed user/watchlist at startup).'
    # The UI startup code (Program.cs) seeds selenium@test.local when using InMemory.
    $seedUserEmail = if ($Username) { $Username } else { 'selenium@test.local' }
    $seedPassword = if ($Password) { $Password } else { 'P@ssw0rd1!' }
    if (-not $Username) { $env:SELENIUM_USERNAME = $seedUserEmail }
    if (-not $Password) { $env:SELENIUM_PASSWORD = $seedPassword }
  }
  else {
    Write-Info 'Ensuring test user + baseline watchlist (AAPL, MSFT)...'
    $conn = $env:ConnectionStrings__DefaultConnection
    if (-not $conn) { $conn = 'Server=.;Database=StockTraderDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true' }
    $seedUserEmail = if ($Username) { $Username } else { 'selenium@test.local' }
    $seedPassword = if ($Password) { $Password } else { 'P@ssw0rd1!' }
    if (-not $Username) { $env:SELENIUM_USERNAME = $seedUserEmail }
    if (-not $Password) { $env:SELENIUM_PASSWORD = $seedPassword }
    $pwdHash = 'AQAAAAEAACcQAAAAEOgCjU3VtWnHn9Q4xg7R97Q6ZC1mZp4TgBBXHytDg4S8z4uZc1QGJvBfQ7y7Q9vNzQ=='
  try {
      $sqlConn = [System.Data.SqlClient.SqlConnection]::new($conn)
      $sqlConn.Open()
      $norm = $seedUserEmail.ToUpperInvariant()
      $find = $sqlConn.CreateCommand(); $find.CommandText = 'SELECT TOP 1 Id FROM [Users] WHERE NormalizedEmail=@e'
      $p = $find.Parameters.Add('@e', [System.Data.SqlDbType]::NVarChar, 256); $p.Value = $norm
      $existing = $find.ExecuteScalar()
      if ($existing) {
        $userId = [string]$existing
        Write-Detail 'User already exists.'
      }
      else {
        $userId = [guid]::NewGuid().ToString()
        Write-Detail "Creating user $seedUserEmail"
        $ins = $sqlConn.CreateCommand();
        $ins.CommandText = @'
INSERT INTO [Users]
(Id,UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,PasswordHash,SecurityStamp,ConcurrencyStamp,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount,CreatedAt,PreferredCulture,EnablePriceAlerts)
VALUES (@Id,@User,@NormUser,@Email,@NormEmail,1,@PwdHash,@Sec,@Conc,0,0,1,0,GETUTCDATE(),'en',1)
'@
        $sec = [guid]::NewGuid().ToString('N'); $conc = [guid]::NewGuid().ToString('N')
        $params = @{'@Id' = $userId; '@User' = $seedUserEmail; '@NormUser' = $norm; '@Email' = $seedUserEmail; '@NormEmail' = $norm; '@PwdHash' = $pwdHash; '@Sec' = $sec; '@Conc' = $conc }
        foreach ($k in $params.Keys) {
          $p2 = $ins.Parameters.Add($k, [System.Data.SqlDbType]::NVarChar, -1); $p2.Value = $params[$k]
        }
        $ins.ExecuteNonQuery() | Out-Null
        Write-Detail 'User created.'
      }
      if ($userId) {
        $countCmd = $sqlConn.CreateCommand();
        $countCmd.CommandText = 'SELECT COUNT(1) FROM UserWatchlistItems WHERE UserId=@u';
        $pu = $countCmd.Parameters.Add('@u', [System.Data.SqlDbType]::NVarChar, 450); $pu.Value = $userId
        $count = [int]$countCmd.ExecuteScalar()
        if ($count -eq 0) {
          Write-Detail 'Seeding watchlist symbols (AAPL, MSFT)'
          foreach ($sym in 'AAPL','MSFT') {
            $w = $sqlConn.CreateCommand();
            $w.CommandText = 'INSERT INTO UserWatchlistItems (UserId, Symbol, AddedAt, SortOrder, EnableAlerts) VALUES (@u,@s,GETUTCDATE(),0,1)'
            $wu = $w.Parameters.Add('@u', [System.Data.SqlDbType]::NVarChar, 450); $wu.Value = $userId
            $ws = $w.Parameters.Add('@s', [System.Data.SqlDbType]::NVarChar, 10); $ws.Value = $sym
            $w.ExecuteNonQuery() | Out-Null
          }
        } else {
          Write-Detail "Watchlist already has $count item(s)."
        }
      }
    }
    catch {
      Write-Warn "Seeding failed: $($_.Exception.Message)"
    }
    finally { if ($sqlConn) { $sqlConn.Dispose() } }
    if ($userId) { $env:SELENIUM_SEED_USERID = $userId } else { Write-Warn 'User ID unresolved; authenticated tests may not login successfully.' }
  }

Write-Info 'Environment variables set:'
Write-Host "  SELENIUM_BASE_URL=$env:SELENIUM_BASE_URL" -ForegroundColor DarkCyan
if ($env:SELENIUM_USERNAME) { Write-Host '  SELENIUM_USERNAME=(set)' -ForegroundColor DarkCyan }
if ($env:SELENIUM_PASSWORD) { Write-Host '  SELENIUM_PASSWORD=(set)' -ForegroundColor DarkCyan }
if ($env:SELENIUM_SEED_USERID) { Write-Host "  SELENIUM_SEED_USERID=$env:SELENIUM_SEED_USERID" -ForegroundColor DarkCyan }
Write-Host "  SELENIUM_CULTURE=$env:SELENIUM_CULTURE" -ForegroundColor DarkCyan
Write-Host "  SELENIUM_HEADLESS=$env:SELENIUM_HEADLESS" -ForegroundColor DarkCyan

# Build filter
$filter = $null
if ($Category) { $filter = "Category=$Category" }
elseif ($AdoId) {
  $ids = ($AdoId -split '[,|;\s]+' | Where-Object { $_ }) -join '|'
  $filter = "AdoId=$ids"
}
# Decide whether build is required
$assemblyPath = Join-Path (Split-Path $testsProj) "bin/$Configuration/net9.0/AiStockTradeApp.SeleniumTests.dll"
$needBuild = $true
if ($NoBuild -and (Test-Path $assemblyPath)) { $needBuild = $false }
elseif (-not $NoBuild) { $needBuild = $true }

if ($needBuild) {
  Write-Info "Building test project ($Configuration)..."
  dotnet build $testsProj -c $Configuration | Out-Host
} else {
  Write-Info 'Skipping build (NoBuild specified and assembly exists).'
}

$logName = if ($CI) { 'ci-selenium.trx' } else { 'local-selenium.trx' }
$testArgs = @('test', $testsProj, '-c', $Configuration, '--logger', "trx;LogFileName=$logName")
if ($NoBuild -and -not $needBuild) { $testArgs += '--no-build' }
if ($filter) { $testArgs += @('--filter', $filter) }

Write-Info ("Running tests: dotnet " + ($testArgs -join ' '))
& dotnet @testArgs
$exit = $LASTEXITCODE
if ($exit -ne 0) { Write-Err "Test run failed with exit code $exit"; exit $exit }
Write-Info 'Test run completed.'
}
finally {
  Restore-PatchedFiles
  # CI cleanup: terminate locally started API/UI processes to avoid lingering STDIO handles on runners
  if ($CI -and -not $SkipStart) {
    Write-Info 'CI cleanup: attempting to stop spawned API/UI processes.'
    $pidFile = Join-Path $repoRoot '.ci-dotnet-pids'
    if (Test-Path $pidFile) {
      Write-Info 'Found PID file (.ci-dotnet-pids); attempting targeted termination.'
      $lines = Get-Content -LiteralPath $pidFile | Where-Object { $_ -match '^[A-Z]+:\d+$' }
      foreach ($l in $lines) {
        $parts = $l.Split(':', 2)
        $role = $parts[0]; $procPid = [int]$parts[1]
        try {
          if (Get-Process -Id $procPid -ErrorAction SilentlyContinue) {
            Write-Info "Stopping $role process PID=$procPid"
            Stop-Process -Id $procPid -Force -ErrorAction SilentlyContinue
            try { Wait-Process -Id $procPid -Timeout 5 -ErrorAction SilentlyContinue } catch {}
          }
        } catch { Write-Warn "Failed to stop $role PID=$procPid : $($_.Exception.Message)" }
      }
      try { Remove-Item $pidFile -Force -ErrorAction SilentlyContinue } catch {}
    }
    try {
      $cleanupIsLinux = $false
      if (Get-Variable -Name IsLinux -Scope Global -ErrorAction SilentlyContinue) { $cleanupIsLinux = $IsLinux }
      elseif ($env:RUNNER_OS -eq 'Linux') { $cleanupIsLinux = $true }
      elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) { $cleanupIsLinux = $true }
    } catch { $cleanupIsLinux = $false }

    if ($cleanupIsLinux) {
      try {
        & bash -c "pkill -f AiStockTradeApp.Api || true; pkill -f AiStockTradeApp.dll || true" | Out-Null
        Write-Info 'Issued pkill commands for Api/UI (Linux).'
      } catch { Write-Warn "pkill cleanup failed: $($_.Exception.Message)" }
    } else {
      # Windows / others: try narrowing to processes whose command line includes project dll names if possible
      try {
        $dotnetProcs = Get-Process -Name dotnet -ErrorAction SilentlyContinue
        foreach ($p in $dotnetProcs) {
          try {
            # Attempt to read MainWindowTitle / Id to heuristically decide termination if started recently
            if ($p.StartTime -gt (Get-Date).AddHours(-1)) {
              # Fallback: terminate if command line contains our projects (requires .NET 6+ / Powershell 7 with Get-CimInstance)
              $cmdLine = (Get-CimInstance Win32_Process -Filter "ProcessId=$($p.Id)" | Select-Object -ExpandProperty CommandLine 2>$null)
              if ($cmdLine -and ($cmdLine -match 'AiStockTradeApp.Api' -or $cmdLine -match 'AiStockTradeApp.csproj')) {
                Write-Info "Stopping process Id=$($p.Id) (API/UI)"
                Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
                try { Wait-Process -Id $p.Id -Timeout 5 -ErrorAction SilentlyContinue } catch {}
              }
            }
          } catch { }
        }
      } catch { Write-Warn "Windows cleanup failed: $($_.Exception.Message)" }
    }

    # Final defensive sweep: ensure no lingering dotnet processes with our assemblies remain (prevents STDIO hang warnings in CI agents)
    try {
      $sweep = Get-Process -Name dotnet -ErrorAction SilentlyContinue | ForEach-Object {
        $cl = (Get-CimInstance Win32_Process -Filter "ProcessId=$($_.Id)" | Select-Object -ExpandProperty CommandLine 2>$null)
        if ($cl -and ($cl -match 'AiStockTradeApp.Api' -or $cl -match 'AiStockTradeApp.McpServer' -or $cl -match 'AiStockTradeApp.csproj' -or $cl -match 'AiStockTradeApp.dll')) { $_ } }
      if ($sweep) {
        Write-Warn "Final sweep found lingering processes: $($sweep.Id -join ','). Forcing termination."
        foreach ($p in $sweep) {
          try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; Wait-Process -Id $p.Id -Timeout 5 -ErrorAction SilentlyContinue } catch {}
        }
      }
    } catch { Write-Warn "Final sweep failed: $($_.Exception.Message)" }

    # Attempt to close chromedriver / chrome if still attached (can also hold STDIO handles in some runners)
    try {
      $chromeProcs = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match 'chrome|chromedriver' }
      foreach ($cp in $chromeProcs) {
        try { Stop-Process -Id $cp.Id -Force -ErrorAction SilentlyContinue } catch {}
      }
    } catch {}
  }
}

Write-Host "\nDone." -ForegroundColor Green
