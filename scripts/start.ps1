<#
.SYNOPSIS
  Dev helper to run the app either via Docker (clean rebuild + up) or locally for debugging.

.DESCRIPTION
  -Mode Docker: Performs a from-scratch compose cycle (down -v --remove-orphans, build --no-cache, up -d --force-recreate) and waits for SQL to be healthy.
  -Mode Local: Starts API and UI in separate PowerShell windows. API uses SQL Server on the host (default instance ".").

.PARAMETER Mode
  "Docker" or "Local". Default: Docker.

.PARAMETER SqlServer
  SQL Server host/instance for Local mode. Default: .

.PARAMETER SqlDatabase
  Database name for Local mode. Default: StockTraderDb.

.PARAMETER ApiProfile
  Launch profile for the API when running locally. Default: https.

.PARAMETER UiProfile
  Launch profile for the UI when running locally. Default: https.

.PARAMETER UseHttps
  If set in Local mode, ensures dev HTTPS certs are trusted and uses HTTPS launch profiles. Default: $true.

.PARAMETER NoBrowser
  If set, prevents automatic opening of browser windows for UI and API endpoints.

.EXAMPLES
  # Clean rebuild and start containers
  ./scripts/start.ps1 -Mode Docker

  # Run locally (API+UI) using host SQL Server default instance
  ./scripts/start.ps1 -Mode Local -SqlServer . -SqlDatabase StockTraderDb

#>
[CmdletBinding(PositionalBinding = $false)]
param(
  [ValidateSet('Docker','Local')]
  [string]$Mode = 'Local',

  # Local mode options
  [string]$SqlServer = '.',
  [string]$SqlDatabase = 'StockTraderDb',
  [string]$ApiProfile = 'https',
  [string]$UiProfile = 'https',
  [switch]$UseHttps,
  [switch]$NoBrowser,
  
  # Help parameter
  [switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Show help if requested or if running with default parameters in an interactive manner
if ($Help) {
    Write-Host @'

AI Stock Trade App - Development Startup Script
===============================================

USAGE:
    ./scripts/start.ps1 [-Mode <Docker|Local>] [Options...]
    ./scripts/start.ps1 -Help

MODES:
    Docker  - Clean rebuild containers and start via docker-compose
    Local   - Start API and UI in separate PowerShell windows with local SQL Server

EXAMPLES:
    # Quick start with containers (recommended for most development)
    ./scripts/start.ps1 -Mode Docker

    # Run locally with default SQL Server instance
    ./scripts/start.ps1 -Mode Local

    # Run locally with custom SQL Server settings
    ./scripts/start.ps1 -Mode Local -SqlServer "localhost\SQLEXPRESS" -SqlDatabase "MyStockDb"

    # Run locally without HTTPS (useful for debugging)
    ./scripts/start.ps1 -Mode Local -UseHttps:$false

    # Run without opening browser windows automatically
    ./scripts/start.ps1 -Mode Docker -NoBrowser

DOCKER MODE:
    - Performs complete cleanup (removes containers, images, volumes)
    - Rebuilds all images from scratch (no cache)
    - Starts services and waits for SQL Server to be healthy
    - Services available at: UI (http://localhost:8080), API (http://localhost:8082)
    - Automatically opens browser windows to both UI and API

LOCAL MODE:
    - Builds solution and starts API/UI in separate PowerShell windows
    - Uses local SQL Server instance (default: "." for default instance)
    - API available at: https://localhost:7032 (HTTP: 5256)
    - UI available at: https://localhost:7043 (HTTP: 5259)
    - Requires SQL Server to be running and accessible
    - Automatically opens browser windows to both UI and API

PREREQUISITES:
    Docker Mode: Docker Desktop installed and running
    Local Mode:  .NET SDK 9.0+, SQL Server running locally

For detailed parameter descriptions, use: Get-Help ./scripts/start.ps1 -Detailed

'@ -ForegroundColor Cyan
    exit 0
}

# Display quick usage hint when running with all defaults (most common beginner scenario)
if ($PSCmdlet.MyInvocation.BoundParameters.Count -eq 0) {
    Write-Host "`nAI Stock Trade App Startup" -ForegroundColor Green
    Write-Host "Running in LOCAL mode with default settings..." -ForegroundColor Cyan
    Write-Host "For usage examples and Docker mode, run: ./scripts/start.ps1 -Help`n" -ForegroundColor Yellow
}

# Compute effective HTTPS setting (defaults to true unless explicitly disabled)
$UseHttpsEffective = $true
if ($PSBoundParameters.ContainsKey('UseHttps')) {
  $UseHttpsEffective = $UseHttps.IsPresent
}

# Resolve paths
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..') | ForEach-Object { $_.Path }
$composeFile = Join-Path $repoRoot 'docker-compose.yml'
$apiProj = Join-Path $repoRoot 'AiStockTradeApp.Api/AiStockTradeApp.Api.csproj'
$uiProj  = Join-Path $repoRoot 'AiStockTradeApp/AiStockTradeApp.csproj'

function Test-CommandExists {
  param([Parameter(Mandatory)][string]$Name)
  return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Open-BrowserWindows {
  param(
    [string]$UiUrl,
    [string]$ApiUrl,
    [string]$Mode
  )
  
  Write-Host "`nOpening browser windows..." -ForegroundColor Cyan
  
  try {
    # Open UI in default browser
    Write-Host "Opening UI at: $UiUrl" -ForegroundColor Green
    Start-Process $UiUrl
    
    # Wait a moment before opening the second window
    Start-Sleep -Seconds 2
    
    # Open API documentation/root endpoint in a new browser window
    Write-Host "Opening API at: $ApiUrl" -ForegroundColor Green
    Start-Process $ApiUrl
    
    Write-Host "`n🎉 Browser windows opened successfully!" -ForegroundColor Green
    Write-Host "UI Application: $UiUrl" -ForegroundColor Cyan
    Write-Host "API Endpoint:   $ApiUrl" -ForegroundColor Cyan
    
    if ($Mode -eq 'Local') {
      Write-Host "`n💡 Tip: Keep the PowerShell windows open to maintain the services running." -ForegroundColor Yellow
    }
    
  } catch {
    Write-Warning "Failed to open browser windows automatically. You can manually navigate to:"
    Write-Host "UI:  $UiUrl" -ForegroundColor Cyan
    Write-Host "API: $ApiUrl" -ForegroundColor Cyan
  }
}

function Invoke-DockerCleanUpAndUp {
  if (-not (Test-CommandExists -Name 'docker')) {
    throw 'Docker CLI not found. Please install Docker Desktop and ensure "docker" is on PATH.'
  }
  if (-not (Test-Path $composeFile)) {
    throw "Compose file not found at $composeFile"
  }

  Write-Host 'Stopping containers and removing images, networks, and volumes...' -ForegroundColor Cyan
  & docker compose -f $composeFile down --rmi all --volumes --remove-orphans | Write-Host

  Write-Host 'Rebuilding images with no cache...' -ForegroundColor Cyan
  & docker compose -f $composeFile build --no-cache | Write-Host

  Write-Host 'Starting containers in detached mode...' -ForegroundColor Cyan
  & docker compose -f $composeFile up -d --force-recreate | Write-Host

  # Wait for SQL Server health (service name: sqlserver)
  Write-Host 'Waiting for SQL Server container to become healthy...' -ForegroundColor Cyan
  $timeoutSec = 180
  $pollIntervalSec = 5
  $deadline = (Get-Date).AddSeconds($timeoutSec)

  do {
    Start-Sleep -Seconds $pollIntervalSec
    $id = (& docker compose -f $composeFile ps -q sqlserver).Trim()
    if (-not $id) { continue }
    $status = (& docker inspect -f '{{.State.Health.Status}}' $id).Trim()
    if ($status -eq 'healthy') {
      Write-Host 'SQL Server is healthy.' -ForegroundColor Green
      break
    } else {
      Write-Host "Current SQL health: $status" -ForegroundColor DarkGray
    }
  } while ((Get-Date) -lt $deadline)

  if ($status -ne 'healthy') {
    Write-Warning 'SQL Server did not report healthy within the timeout. The API may retry migrations until ready.'
  }

  Write-Host 'Compose status:' -ForegroundColor Cyan
  & docker compose -f $composeFile ps
  
  # Open browser windows for Docker mode
  if (-not $NoBrowser) {
    Open-BrowserWindows -UiUrl "http://localhost:8080" -ApiUrl "http://localhost:8082" -Mode "Docker"
  } else {
    Write-Host "`n✅ Services started successfully!" -ForegroundColor Green
    Write-Host "UI Application: http://localhost:8080" -ForegroundColor Cyan
    Write-Host "API Endpoint:   http://localhost:8082" -ForegroundColor Cyan
  }
}

function Enable-DevHttpsCert {
  if (-not $UseHttpsEffective) { return }
  if (-not (Test-CommandExists -Name 'dotnet')) { return }
  try {
    & dotnet dev-certs https --check | Out-Null
  } catch {
    Write-Host 'Trusting .NET developer HTTPS certificate...' -ForegroundColor Cyan
    & dotnet dev-certs https --trust | Write-Host
  }
}

function Start-LocalProcesses {
  if (-not (Test-CommandExists -Name 'dotnet')) {
    throw 'dotnet SDK not found. Please install .NET SDK and ensure "dotnet" is on PATH.'
  }
  if (-not (Test-Path $apiProj)) { throw "API project not found: $apiProj" }
  if (-not (Test-Path $uiProj))  { throw "UI project not found: $uiProj" }

  Enable-DevHttpsCert

  $cs = "Server=$SqlServer;Database=$SqlDatabase;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true"
  Write-Host "Using connection string: $cs" -ForegroundColor DarkCyan

  # Pre-restore and pre-build to avoid concurrent builds causing file contention (e.g., CS2012 on shared projects)
  $slnPath = Join-Path $repoRoot 'AiStockTradeApp.sln'
  Write-Host 'Restoring solution packages...' -ForegroundColor Cyan
  & dotnet restore $slnPath | Write-Host
  Write-Host 'Building solution (Debug)...' -ForegroundColor Cyan
  & dotnet build $slnPath -c Debug | Write-Host

  # Launch API with environment passed safely via Start-Process -Environment
  $apiEnv = @{
    'ConnectionStrings__DefaultConnection' = $cs;
    'USE_INMEMORY_DB' = 'false';
    'ASPNETCORE_ENVIRONMENT' = 'Development'
  }
  # Use --no-build to prevent a second concurrent build
  $apiArgs = @('-NoExit','-Command',"dotnet run --no-build --project `"$apiProj`" --launch-profile $ApiProfile")
  Write-Host 'Launching API in a new PowerShell window...' -ForegroundColor Cyan
  Start-Process -FilePath 'pwsh' -ArgumentList $apiArgs -WorkingDirectory $repoRoot -Environment $apiEnv | Out-Null

  # Wait for API to be responsive before starting UI to avoid race conditions
  $apiHealthUrlHttp = 'http://localhost:5256/health'
  $apiHealthUrlHttps = 'https://localhost:7032/health'
  $timeoutSec = 120
  $pollIntervalSec = 2
  $deadline = (Get-Date).AddSeconds($timeoutSec)
  $apiReady = $false
  Write-Host "Waiting for API to be ready at $apiHealthUrlHttp (fallback: $apiHealthUrlHttps)..." -ForegroundColor Cyan
  while ((Get-Date) -lt $deadline) {
    try {
      # Prefer HTTP to avoid cert trust issues
      $resp = Invoke-WebRequest -Uri $apiHealthUrlHttp -Method GET -TimeoutSec 5 -ErrorAction Stop
      if ($resp.StatusCode -eq 200) { $apiReady = $true; break }
    } catch { }
    try {
      $resp2 = Invoke-WebRequest -Uri $apiHealthUrlHttps -Method GET -TimeoutSec 5 -SkipCertificateCheck:$true -ErrorAction Stop
      if ($resp2.StatusCode -eq 200) { $apiReady = $true; break }
    } catch { }
    Start-Sleep -Seconds $pollIntervalSec
  }
  if (-not $apiReady) {
    Write-Warning "API did not respond healthy within ${timeoutSec}s. UI will not be started to avoid failures. Check the API window for errors."
    return
  }
  Write-Host 'API is healthy. Starting UI...' -ForegroundColor Green

  # Launch UI with API endpoint pointing to the locally running API
  $uiEnv = @{
    'ConnectionStrings__DefaultConnection' = $cs;
    'USE_INMEMORY_DB' = 'false';
    'ASPNETCORE_ENVIRONMENT' = 'Development';
    'StockApi__BaseUrl' = 'https://localhost:7032';
    'StockApi__HttpBaseUrl' = 'http://localhost:5256'
  }
  # Use --no-build to prevent a second concurrent build
  $uiArgs = @('-NoExit','-Command',"dotnet run --no-build --project `"$uiProj`" --launch-profile $UiProfile")
  Write-Host 'Launching UI in a new PowerShell window...' -ForegroundColor Cyan
  Start-Process -FilePath 'pwsh' -ArgumentList $uiArgs -WorkingDirectory $repoRoot -Environment $uiEnv | Out-Null

  Write-Host 'Local processes started. API (7032/5256), UI (7043/5259) per launchSettings.' -ForegroundColor Green
  
  # Wait a moment for UI to start before opening browsers
  if (-not $NoBrowser) {
    Write-Host 'Waiting for UI to initialize...' -ForegroundColor Cyan
    Start-Sleep -Seconds 5
    
    # Determine URLs based on profile settings
    $uiUrl = if ($UiProfile -eq 'https' -and $UseHttpsEffective) { "https://localhost:7043" } else { "http://localhost:5259" }
    $apiUrl = if ($ApiProfile -eq 'https' -and $UseHttpsEffective) { "https://localhost:7032" } else { "http://localhost:5256" }
    
    # Open browser windows for Local mode
    Open-BrowserWindows -UiUrl $uiUrl -ApiUrl $apiUrl -Mode "Local"
  } else {
    # Determine URLs based on profile settings for display
    $uiUrl = if ($UiProfile -eq 'https' -and $UseHttpsEffective) { "https://localhost:7043" } else { "http://localhost:5259" }
    $apiUrl = if ($ApiProfile -eq 'https' -and $UseHttpsEffective) { "https://localhost:7032" } else { "http://localhost:5256" }
    
    Write-Host "`n✅ Services started successfully!" -ForegroundColor Green
    Write-Host "UI Application: $uiUrl" -ForegroundColor Cyan
    Write-Host "API Endpoint:   $apiUrl" -ForegroundColor Cyan
    Write-Host "`n💡 Tip: Keep the PowerShell windows open to maintain the services running." -ForegroundColor Yellow
  }
}

switch ($Mode) {
  'Docker' {
    Invoke-DockerCleanUpAndUp
  }
  'Local' {
    Start-LocalProcesses
  }
  default {
    throw "Unknown mode: $Mode"
  }
}
