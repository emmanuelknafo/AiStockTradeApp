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

.PARAMETER RemoveVolumes
  If set in Docker mode, forces removal of Docker volumes during cleanup for a completely fresh start. Default: $false (volumes are preserved for data persistence).

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
  
  # Docker mode options
  [switch]$RemoveVolumes,
  
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
    Local   - Start API, UI, and MCP Server in separate PowerShell windows with local SQL Server

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

    # Force removal of volumes for completely fresh start
    ./scripts/start.ps1 -Mode Docker -RemoveVolumes

DOCKER MODE:
    - Performs complete cleanup (removes containers, images, networks)
    - By default, preserves volumes to maintain data persistence and test data protection
    - Use -RemoveVolumes to force removal of volumes and start completely fresh
    - Rebuilds all images from scratch (no cache)
    - Starts services and waits for SQL Server to be healthy
  - Services available at: UI (http://localhost:8080), API (http://localhost:8082), MCP Server (http://localhost:5500) (Docker uses 5500; Local uses 5000)
    - SQL Server available at: localhost:14330 (mapped to avoid conflict with host SQL Server on 1433)
    - Automatically opens browser windows to both UI and API

LOCAL MODE:
    - Builds solution and starts API/UI/MCP Server in separate PowerShell windows
    - Uses local SQL Server instance (default: "." for default instance)
    - API available at: https://localhost:7032 (HTTP: 5256)
    - UI available at: https://localhost:7043 (HTTP: 5259)
  - MCP Server available at: http://localhost:5000/mcp (HTTP mode for testing; Docker uses 5500)
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
$mcpProj = Join-Path $repoRoot 'AiStockTradeApp.McpServer/AiStockTradeApp.McpServer.csproj'

function Test-CommandExists {
  param([Parameter(Mandatory)][string]$Name)
  return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Open-BrowserWindows {
  param(
    [string]$UiUrl,
    [string]$ApiUrl,
    [string]$McpUrl = "",
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
    
    # Open MCP Server if URL provided (for local mode)
    if ($McpUrl) {
      Start-Sleep -Seconds 2
      Write-Host "Opening MCP Server at: $McpUrl" -ForegroundColor Green  
      Start-Process $McpUrl
    }
    
    Write-Host "`n🎉 Browser windows opened successfully!" -ForegroundColor Green
    Write-Host "UI Application: $UiUrl" -ForegroundColor Cyan
    Write-Host "API Endpoint:   $ApiUrl" -ForegroundColor Cyan
    if ($McpUrl) {
      Write-Host "MCP Server:     $McpUrl" -ForegroundColor Cyan
    }
    
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

  # Ensure any local dotnet processes are terminated to avoid file/port locks or file contention
  Write-Host 'Performing clean shutdown of existing dotnet processes on host to avoid conflicts with Docker...' -ForegroundColor Yellow
  try {
    $dotnetProcesses = Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue
    if ($dotnetProcesses) {
      Write-Host "Found $($dotnetProcesses.Count) dotnet process(es), terminating them to avoid conflicts..." -ForegroundColor Yellow
      & taskkill /f /im dotnet.exe | Out-Null
      Start-Sleep -Seconds 2
      Write-Host 'Existing dotnet processes terminated.' -ForegroundColor Green
    } else {
      Write-Host 'No existing dotnet processes found.' -ForegroundColor Green
    }
  } catch {
    Write-Warning "Could not clean up existing dotnet processes: $($_.Exception.Message)"
  }

  Write-Host 'Stopping containers and removing images and networks...' -ForegroundColor Cyan
  if ($RemoveVolumes) {
    Write-Host 'Also removing volumes (forced via -RemoveVolumes)...' -ForegroundColor Yellow
    & docker compose -f $composeFile down --rmi all --volumes --remove-orphans | Write-Host
  } else {
    Write-Host 'Preserving volumes for data persistence...' -ForegroundColor Green
    & docker compose -f $composeFile down --rmi all --remove-orphans | Write-Host
  }

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
    Open-BrowserWindows -UiUrl "http://localhost:8080" -ApiUrl "http://localhost:8082" -McpUrl "http://localhost:5500/mcp" -Mode "Docker"
  } else {
    Write-Host "`n✅ Services started successfully!" -ForegroundColor Green
    Write-Host "UI Application: http://localhost:8080" -ForegroundColor Cyan
    Write-Host "API Endpoint:   http://localhost:8082" -ForegroundColor Cyan
    Write-Host "MCP Server:     http://localhost:5500/mcp" -ForegroundColor Cyan
  }

  # Run a smoke test against the MCP server to validate it's responding to JSON-RPC requests
  if (Test-McpEndpoint -Url 'http://localhost:5500/mcp' -TimeoutSec 60 -PollIntervalSec 2) {
    Write-Host 'MCP server smoke test: PASSED' -ForegroundColor Green
  } else {
    Write-Warning 'MCP server smoke test: FAILED (endpoint did not respond as expected)'
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
  if (-not (Test-Path $mcpProj)) { throw "MCP Server project not found: $mcpProj" }

  # Clean shutdown of any existing dotnet processes to avoid file locks
  Write-Host 'Performing clean shutdown of existing dotnet processes...' -ForegroundColor Yellow
  try {
    $dotnetProcesses = Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue
    if ($dotnetProcesses) {
      Write-Host "Found $($dotnetProcesses.Count) dotnet process(es), terminating..." -ForegroundColor Yellow
      & taskkill /f /im dotnet.exe | Out-Null
      Start-Sleep -Seconds 2  # Wait for processes to fully terminate
      Write-Host 'Existing dotnet processes terminated.' -ForegroundColor Green
    } else {
      Write-Host 'No existing dotnet processes found.' -ForegroundColor Green
    }
  } catch {
    Write-Warning "Could not clean up existing dotnet processes: $($_.Exception.Message)"
  }

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

  # Launch MCP Server in HTTP mode
  $mcpEnv = @{
    'ASPNETCORE_ENVIRONMENT' = 'Development';
    'STOCK_API_BASE_URL' = 'https://localhost:7032'
  }
  $mcpArgs = @('-NoExit','-Command',"dotnet run --no-build --project `"$mcpProj`" -- --http")
  Write-Host 'Launching MCP Server in HTTP mode in a new PowerShell window...' -ForegroundColor Cyan
  Start-Process -FilePath 'pwsh' -ArgumentList $mcpArgs -WorkingDirectory $repoRoot -Environment $mcpEnv | Out-Null

  Write-Host 'Local processes started. API (7032/5256), UI (7043/5259), MCP Server (5000/mcp) per launchSettings.' -ForegroundColor Green
  
  # Wait a moment for UI to start before opening browsers
  if (-not $NoBrowser) {
    Write-Host 'Waiting for UI to initialize...' -ForegroundColor Cyan
    Start-Sleep -Seconds 5
    
    # Determine URLs based on profile settings
    $uiUrl = if ($UiProfile -eq 'https' -and $UseHttpsEffective) { "https://localhost:7043" } else { "http://localhost:5259" }
    $apiUrl = if ($ApiProfile -eq 'https' -and $UseHttpsEffective) { "https://localhost:7032" } else { "http://localhost:5256" }
    
    # Open browser windows for Local mode
    Open-BrowserWindows -UiUrl $uiUrl -ApiUrl $apiUrl -McpUrl "http://localhost:5000/mcp" -Mode "Local"
  } else {
    # Determine URLs based on profile settings for display
    $uiUrl = if ($UiProfile -eq 'https' -and $UseHttpsEffective) { "https://localhost:7043" } else { "http://localhost:5259" }
    $apiUrl = if ($ApiProfile -eq 'https' -and $UseHttpsEffective) { "https://localhost:7032" } else { "http://localhost:5256" }
    
    Write-Host "`n✅ Services started successfully!" -ForegroundColor Green
    Write-Host "UI Application: $uiUrl" -ForegroundColor Cyan
    Write-Host "API Endpoint:   $apiUrl" -ForegroundColor Cyan
    Write-Host "MCP Server:     http://localhost:5000/mcp" -ForegroundColor Cyan
    Write-Host "`n💡 Tip: Keep the PowerShell windows open to maintain the services running." -ForegroundColor Yellow
  }

  # Run a smoke test against the MCP server to validate it's responding to JSON-RPC requests
  if (Test-McpEndpoint -Url 'http://localhost:5000/mcp' -TimeoutSec 60 -PollIntervalSec 2) {
    Write-Host 'MCP server smoke test: PASSED' -ForegroundColor Green
  } else {
    Write-Warning 'MCP server smoke test: FAILED (endpoint did not respond as expected)'
  }
}


function Test-McpEndpoint {
  param(
    [string]$Url = 'http://localhost:5000/mcp',
    [int]$TimeoutSec = 60,
    [int]$PollIntervalSec = 2
  )

  $deadline = (Get-Date).AddSeconds($TimeoutSec)
  $payload = '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'

  Write-Host "Running MCP smoke test against: $Url (timeout: ${TimeoutSec}s)" -ForegroundColor Cyan

  while ((Get-Date) -lt $deadline) {
    try {
  # Send request and read raw content so we can handle either plain JSON or SSE-style responses
  $headers = @{ 'Accept' = 'application/json, text/event-stream' }
  $wr = Invoke-WebRequest -Uri $Url -Method Post -ContentType 'application/json' -Body $payload -Headers $headers -TimeoutSec 10 -ErrorAction Stop
      $content = $wr.Content

      # SSE responses often prefix JSON with 'data: ' lines. Try to extract JSON after the last 'data:' if present.
      $json = $null
      if ($content -match '(?s)data:\s*(\{.*\})') {
        $json = $matches[1]
      } else {
        # No SSE prefix, assume entire body is JSON
        $json = $content
      }

      try {
        $obj = ConvertFrom-Json $json -ErrorAction Stop
        $respJson = $obj | ConvertTo-Json -Depth 5
        Write-Host "MCP response:\n$respJson" -ForegroundColor Green
        return $true
      } catch {
        # Couldn't parse JSON yet; fallthrough to retry
        Write-Host "Received non-JSON response while probing MCP endpoint, will retry..." -ForegroundColor DarkYellow
      }
    } catch {
      # Swallow and retry until timeout
      Start-Sleep -Seconds $PollIntervalSec
    }
  }

  return $false
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
