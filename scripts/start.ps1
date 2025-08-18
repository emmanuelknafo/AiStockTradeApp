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
  [switch]$UseHttps = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve paths
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..') | ForEach-Object { $_.Path }
$composeFile = Join-Path $repoRoot 'docker-compose.yml'
$apiProj = Join-Path $repoRoot 'AiStockTradeApp.Api/AiStockTradeApp.Api.csproj'
$uiProj  = Join-Path $repoRoot 'AiStockTradeApp/AiStockTradeApp.csproj'

function Test-CommandExists {
  param([Parameter(Mandatory)][string]$Name)
  return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Invoke-DockerCleanUpAndUp {
  if (-not (Test-CommandExists -Name 'docker')) {
    throw 'Docker CLI not found. Please install Docker Desktop and ensure "docker" is on PATH.'
  }
  if (-not (Test-Path $composeFile)) {
    throw "Compose file not found at $composeFile"
  }

  Write-Host 'Stopping and removing existing containers, networks, and volumes...' -ForegroundColor Cyan
  & docker compose -f $composeFile down --volumes --remove-orphans | Write-Host

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
}

function Ensure-DevHttpsCert {
  if (-not $UseHttps) { return }
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

  Ensure-DevHttpsCert

  $cs = "Server=$SqlServer;Database=$SqlDatabase;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true"
  Write-Host "Using connection string: $cs" -ForegroundColor DarkCyan

  # Pre-restore to avoid concurrent NuGet operations causing file contention
  Write-Host 'Restoring solution packages...' -ForegroundColor Cyan
  & dotnet restore (Join-Path $repoRoot 'AiStockTradeApp.sln') | Write-Host

  # Launch API with environment passed safely via Start-Process -Environment
  $apiEnv = @{
    'ConnectionStrings__DefaultConnection' = $cs;
    'USE_INMEMORY_DB' = 'false';
    'ASPNETCORE_ENVIRONMENT' = 'Development'
  }
  $apiArgs = @('-NoExit','-Command',"dotnet run --project `"$apiProj`" --launch-profile $ApiProfile")
  Write-Host 'Launching API in a new PowerShell window...' -ForegroundColor Cyan
  Start-Process -FilePath 'pwsh' -ArgumentList $apiArgs -WorkingDirectory $repoRoot -Environment $apiEnv | Out-Null

  # Stagger UI start to minimize simultaneous builds
  Start-Sleep -Seconds 6

  # Launch UI with API endpoint pointing to the locally running API
  $uiEnv = @{
    'ASPNETCORE_ENVIRONMENT' = 'Development';
    'StockApi__BaseUrl' = 'https://localhost:7032';
    'StockApi__HttpBaseUrl' = 'http://localhost:5256'
  }
  $uiArgs = @('-NoExit','-Command',"dotnet run --project `"$uiProj`" --launch-profile $UiProfile")
  Write-Host 'Launching UI in a new PowerShell window...' -ForegroundColor Cyan
  Start-Process -FilePath 'pwsh' -ArgumentList $uiArgs -WorkingDirectory $repoRoot -Environment $uiEnv | Out-Null

  Write-Host 'Local processes started. API (7032/5256), UI (7043/5259) per launchSettings.' -ForegroundColor Green
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
