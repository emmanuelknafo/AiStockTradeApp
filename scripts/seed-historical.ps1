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

.PARAMETER ListedFile
  Optional path to the listed stocks screener CSV to import before historicals
  (e.g., data\nasdaq.com\nasdaq_screener_*.csv). If not provided, the most
  recent nasdaq_screener_*.csv in data\nasdaq.com will be used when available.

.PARAMETER SkipListed
  If set, skips the pre-step that imports listed stocks.

.EXAMPLE
  ./scripts/seed-historical.ps1

.EXAMPLE
  ./scripts/seed-historical.ps1 -ApiBase https://localhost:7032 -Watch
#>
[CmdletBinding(PositionalBinding = $false)]
param(
  [string]$Folder = 'data/nasdaq.com/Historical',
  [string]$ApiBase = 'http://localhost:8082',
  [string]$Pattern = 'HistoricalData_*_*.csv',
  [switch]$Watch,
  [switch]$DryRun,
  [string]$ListedFile,
  [switch]$SkipListed
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

# Pre-step: Import listed stocks (screener CSV) if not skipped
if (-not $SkipListed) {
  try {
    $listedPath = $null
    if ($ListedFile -and -not [string]::IsNullOrWhiteSpace($ListedFile)) {
      $resolved = Resolve-Path -LiteralPath $ListedFile -ErrorAction Stop
      $listedPath = $resolved.Path
    }
    else {
      $dataDir = Join-Path $repoRoot 'data/nasdaq.com'
      if (Test-Path $dataDir) {
        $latest = Get-ChildItem -Path $dataDir -File -Filter 'nasdaq_screener_*.csv' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($latest) { $listedPath = $latest.FullName }
      }
    }

    if ($listedPath) {
      Write-Host "Importing listed stocks from: $listedPath" -ForegroundColor Cyan
      if (-not $DryRun) {
        $listArgs = @('run', '--no-build', '--project', "$cliProj", '--', 'import-listed', '--file', "$listedPath", '--api', "$ApiBase")
        & dotnet @listArgs
      }
      else {
        Write-Host "(DryRun) Would run: aistock-cli import-listed --file `"$listedPath`" --api `"$ApiBase`"" -ForegroundColor DarkGray
      }
    }
    else {
      Write-Warning "No listed stocks CSV found. Searched parameter and data\\nasdaq.com\\nasdaq_screener_*.csv. Skipping listed import."
    }
  }
  catch {
    Write-Warning "Listed stocks import step failed: $($_.Exception.Message)"
  }
}
else {
  Write-Host 'SkipListed is set; skipping listed stocks import step.' -ForegroundColor DarkGray
}

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

  $cliArgs = @(
    'run', '--no-build', '--project', "$cliProj", '--',
    'import-historical', '--symbol', "$symbol", '--file', "$($f.FullName)", '--api', "$ApiBase"
  )
  if ($Watch.IsPresent) { $cliArgs += '--watch' }

  try {
    & dotnet @cliArgs
  }
  catch {
    Write-Warning "Import failed for ${symbol}: $($_.Exception.Message)"
  }
}

Write-Host 'Seed historical import completed.' -ForegroundColor Cyan
