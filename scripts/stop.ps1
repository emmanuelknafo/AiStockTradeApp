<#!
.SYNOPSIS
  Stops running AiStockTradeApp services and performs cleanup.

.DESCRIPTION
  Cross-platform stop script that:
    1. Terminates all running dotnet processes (forcefully) similar to start.ps1 pre-start cleanup.
    2. If docker-compose environment is present, brings it down removing containers, images, networks, and orphans while PRESERVING VOLUMES by default.
    3. Optional -RemoveVolumes flag allows full volume purge when explicitly requested.

.PARAMETER RemoveVolumes
  When specified, also removes docker volumes (data will be lost). Default preserves volumes.

.PARAMETER SkipDocker
  Skip docker cleanup (only kill dotnet processes).

.PARAMETER Force
  Suppresses confirmation prompts for destructive operations (volume removal).

.NOTES
  Mirrors logic style from scripts/start.ps1 (Invoke-DotnetProcessShutdown) for consistency.
  Safe to run multiple times.

.EXAMPLE
  ./scripts/stop.ps1
  Stops dotnet processes and docker compose stack (preserving volumes).

.EXAMPLE
  ./scripts/stop.ps1 -RemoveVolumes -Force
  Full cleanup including volumes without prompt.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
  [switch]$RemoveVolumes,
  [switch]$SkipDocker,
  [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Section($text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }
function Test-CommandExists { param([Parameter(Mandatory)][string]$Name) return [bool](Get-Command $Name -ErrorAction SilentlyContinue) }

# Determine repo root relative to this script path
$Script:ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Script:RepoRoot = Resolve-Path (Join-Path $Script:ScriptDir '..') | ForEach-Object { $_.Path }
$Script:ComposeFile = Join-Path $RepoRoot 'docker-compose.yml'

function Invoke-DotnetProcessShutdown {
  [CmdletBinding()]
  param([string]$Reason = 'stop')
  Write-Section "Dotnet Process Termination ($Reason)"
  try {
    $dotnetProcesses = Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue
    if (-not $dotnetProcesses) { Write-Host 'No dotnet processes found.' -ForegroundColor Green; return }
    Write-Host "Found $($dotnetProcesses.Count) dotnet process(es) – terminating..." -ForegroundColor Yellow

    $platformIsLinux = $false; $platformIsMac = $false
    try {
      if (Get-Variable -Name IsLinux -Scope Global -ErrorAction SilentlyContinue) { $platformIsLinux = $IsLinux }
      if (Get-Variable -Name IsMacOS -Scope Global -ErrorAction SilentlyContinue) { $platformIsMac = $IsMacOS }
      if (-not ($platformIsLinux -or $platformIsMac)) {
        if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) { $platformIsLinux = $true }
        elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) { $platformIsMac = $true }
      }
    } catch { }

    if ($platformIsLinux -or $platformIsMac) {
      foreach ($p in $dotnetProcesses) { try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch { } }
      if (Test-CommandExists -Name 'pkill') { & pkill -f 'dotnet' 2>$null | Out-Null }
    }
    else {
      if (Test-CommandExists -Name 'taskkill') { & taskkill /f /im dotnet.exe 2>$null | Out-Null }
      else { foreach ($p in $dotnetProcesses) { try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch { } } }
    }

    Start-Sleep -Milliseconds 800
    $remaining = Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue
    if ($remaining) {
      Write-Warning "Some dotnet processes still remain (forcing again)..."
      foreach ($p in $remaining) { try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch { } }
      Start-Sleep -Milliseconds 500
    }
    if (Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue) {
      Write-Warning 'Residual dotnet processes may persist (locked / system).'
    } else {
      Write-Host 'All dotnet processes terminated.' -ForegroundColor Green
    }
  } catch {
    Write-Warning "Dotnet shutdown encountered an error: $($_.Exception.Message)"
  }
}

function Invoke-DockerShutdown {
  [CmdletBinding()]
  param()
  if ($SkipDocker) { Write-Host 'Skipping Docker cleanup as requested.' -ForegroundColor Yellow; return }
  if (-not (Test-CommandExists -Name 'docker')) { Write-Warning 'Docker CLI not found; skipping container/image cleanup.'; return }
  if (-not (Test-Path $ComposeFile)) { Write-Warning "Compose file not found at $ComposeFile"; return }

  Write-Section 'Docker Compose Shutdown'
  if ($RemoveVolumes -and -not $Force) {
    $resp = Read-Host 'You requested volume removal. This deletes persistent data. Continue? (y/N)'
    if ($resp -notin @('y','Y')) { Write-Host 'Volume removal aborted by user; proceeding without volume deletion.' -ForegroundColor Yellow; $RemoveVolumes = $false }
  }

  $downArgs = @('compose','-f', $ComposeFile, 'down','--rmi','all','--remove-orphans')
  if ($RemoveVolumes) { $downArgs += '--volumes' }

  Write-Host ('Running: docker ' + ($downArgs -join ' ')) -ForegroundColor DarkGray
  try {
    & docker @downArgs | Write-Host
  } catch {
    Write-Warning "docker compose down failed: $($_.Exception.Message)"
  }

  Write-Host 'Pruning dangling images (safe)...' -ForegroundColor Cyan
  try { & docker image prune -f | Write-Host } catch { }

  Write-Host 'Listing remaining ai-stock related images (if any)...' -ForegroundColor Cyan
  try { & docker images | Where-Object { $_ -match 'aistock' } | Out-String | Write-Host } catch { }

  if (-not $RemoveVolumes) {
    Write-Host 'Volumes preserved (default behavior).' -ForegroundColor Green
  } else {
    Write-Host 'Volumes removed (explicit request).' -ForegroundColor Yellow
  }
}

Write-Host "AiStockTradeApp STOP Utility" -ForegroundColor Green
Write-Host "Working Directory: $RepoRoot" -ForegroundColor DarkGray

Invoke-DotnetProcessShutdown -Reason 'manual stop'
Invoke-DockerShutdown

Write-Host "\n✅ Stop procedure completed." -ForegroundColor Green
if (-not $RemoveVolumes) { Write-Host 'Persistent data volumes remain intact.' -ForegroundColor Cyan }
else { Write-Host 'All data including volumes removed.' -ForegroundColor Yellow }
