<#
.SYNOPSIS
  Seed historical prices by importing all CSV files in the Historical data folder via the CLI.

.DESCRIPTION
  Finds files like HistoricalData_*_SYMBOL.csv in data\nasdaq.com\Historical, derives SYMBOL from the filename,
  and calls the CLI: aistock-cli import-historical --symbol SYMBOL --file <path> --api <base> [--watch].

.PARAMETER Folder
  Folder containing CSV files (default: <repo>/data/nasdaq.com/Historical).

.PARAMETER ApiBase
  API base URL (default: http://localhost:5256 to avoid TLS prompts).

.PARAMETER Pattern
  File name pattern (default: HistoricalData_*_*.csv).

.PARAMETER Watch
  If set, pass --watch to the CLI to track each background job until completion.

.PARAMETER DryRun
  If set, list planned imports without invoking the CLI.

.EXAMPLE
  ./scripts/seed-historical.ps1

.EXAMPLE
  ./scripts/seed-historical.ps1 -ApiBase https://localhost:7032 -Watch
#>
[CmdletBinding(PositionalBinding = $false)]
param(
  [string]$Folder,
  [string]$ApiBase = 'http://localhost:5256',
  [string]$Pattern = 'HistoricalData_*_*.csv',
  [switch]$Watch,
  [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-CommandExists {
  param([Parameter(Mandatory)][string]$Name)
  return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

# Resolve repo paths
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..') | ForEach-Object { $_.Path }
$defaultDataDir = Join-Path $repoRoot 'data/nasdaq.com/Historical'
if (-not $Folder -or [string]::IsNullOrWhiteSpace($Folder)) { $Folder = $defaultDataDir }
$Folder = (Resolve-Path $Folder).Path

if (-not (Test-Path $Folder)) { throw "Folder not found: $Folder" }
if (-not (Test-CommandExists -Name 'dotnet')) { throw 'dotnet SDK not found on PATH' }

$cliProj = Join-Path $repoRoot 'AiStockTradeApp.Cli/AiStockTradeApp.Cli.csproj'
if (-not (Test-Path $cliProj)) { throw "CLI project not found: $cliProj" }

Write-Host "Scanning for CSV files in: $Folder" -ForegroundColor Cyan
$files = Get-ChildItem -Path $Folder -File -Filter $Pattern | Sort-Object Name
if (-not $files -or $files.Count -eq 0) {
  Write-Warning "No files found matching pattern '$Pattern' in $Folder"
  return
}

# Build the CLI once to avoid rebuilds on every import
Write-Host 'Building CLI (Debug)...' -ForegroundColor Cyan
& dotnet build $cliProj -c Debug | Write-Host

function Get-SymbolFromFilename {
  param([Parameter(Mandatory)][System.IO.FileInfo]$File)
  $nameNoExt = [System.IO.Path]::GetFileNameWithoutExtension($File.Name)
  $idx = $nameNoExt.LastIndexOf('_')
  if ($idx -lt 0 -or $idx -ge $nameNoExt.Length - 1) { return $null }
  return $nameNoExt.Substring($idx + 1).Trim().ToUpperInvariant()
}

$total = $files.Count
$n = 0
foreach ($f in $files) {
  $n++
  $symbol = Get-SymbolFromFilename -File $f
  if ([string]::IsNullOrWhiteSpace($symbol)) {
    Write-Warning "[$n/$total] Skip (cannot parse symbol): $($f.Name)"
    continue
  }
  Write-Host "[$n/$total] Importing $symbol from '$($f.Name)'" -ForegroundColor Green
  if ($DryRun) { continue }

  $args = @(
    'run','--no-build','--project',"$cliProj",'--',
    'import-historical','--symbol',"$symbol",'--file',"$($f.FullName)",'--api',"$ApiBase"
  )
  if ($Watch.IsPresent) { $args += '--watch' }

  try {
    & dotnet @args
  }
  catch {
    Write-Warning "Import failed for ${symbol}: $($_.Exception.Message)"
  }
}

Write-Host 'Seed historical import completed.' -ForegroundColor Cyan
