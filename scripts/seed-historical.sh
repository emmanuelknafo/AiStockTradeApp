#!/usr/bin/env bash
# Seed historical prices by importing CSVs via the .NET CLI tool.
# Converted from scripts/seed-historical.ps1 for WSL2/Ubuntu-friendly usage.

set -euo pipefail
IFS=$'\n\t'

# Defaults (mirroring the PowerShell script)
FOLDER=""
APIBASE="http://localhost:5256"
PATTERN='HistoricalData_*_*.csv'
WATCH=false
DRYRUN=false
LISTED_FILE=""
SKIP_LISTED=false

print_usage() {
  cat <<EOF
Usage: $0 [--folder PATH] [--api-base URL] [--pattern GLOB] [--watch] [--dry-run] [--listed-file PATH] [--skip-listed]

Defaults:
  --folder        <repo>/data/nasdaq.com/Historical
  --api-base      http://localhost:5256
  --pattern       HistoricalData_*_*.csv

Examples:
  $0
  $0 --api-base http://localhost:5256 --watch
  $0 --folder ./data/nasdaq.com/Historical --pattern 'HistoricalData_*_AAPL.csv' --dry-run
  $0 --listed-file ./data/nasdaq.com/nasdaq_screener_2025.csv
EOF
}

# Arg parsing
while [[ $# -gt 0 ]]; do
  case "$1" in
    --folder)
      FOLDER="$2"; shift 2;;
    --api-base)
      APIBASE="$2"; shift 2;;
    --pattern)
      PATTERN="$2"; shift 2;;
    --watch)
      WATCH=true; shift 1;;
    --dry-run)
      DRYRUN=true; shift 1;;
    --listed-file)
      LISTED_FILE="$2"; shift 2;;
    --skip-listed)
      SKIP_LISTED=true; shift 1;;
    -h|--help)
      print_usage; exit 0;;
    *)
      echo "Unknown arg: $1" >&2; print_usage; exit 2;;
  esac
done

# Resolve paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DEFAULT_DATA_DIR="$REPO_ROOT/data/nasdaq.com/Historical"
if [[ -z "$FOLDER" ]]; then FOLDER="$DEFAULT_DATA_DIR"; fi
# best-effort absolute path
if command -v realpath >/dev/null 2>&1; then
  FOLDER="$(realpath "$FOLDER")"
else
  FOLDER="$(cd "$FOLDER" 2>/dev/null && pwd || echo "$FOLDER")"
fi

if [[ ! -d "$FOLDER" ]]; then echo "Folder not found: $FOLDER" >&2; exit 1; fi
if ! command -v dotnet >/dev/null 2>&1; then echo "dotnet SDK not found on PATH" >&2; exit 1; fi

CLIPROJ="$REPO_ROOT/AiStockTradeApp.Cli/AiStockTradeApp.Cli.csproj"
if [[ ! -f "$CLIPROJ" ]]; then echo "CLI project not found: $CLIPROJ" >&2; exit 1; fi

# Gather files by pattern, sorted by name, null-safe
mapfile -d '' -t FILES < <(find "$FOLDER" -maxdepth 1 -type f -name "$PATTERN" -printf '%f\0' | sort -z)
if [[ ${#FILES[@]} -eq 0 ]]; then
  echo "No files found matching pattern '$PATTERN' in $FOLDER" >&2
  exit 0
fi

# Build the CLI once (Debug)
echo "Building CLI (Debug)..."
dotnet build "$CLIPROJ" -c Debug >/dev/null

# Optional pre-step: import listed stocks
if [[ "$SKIP_LISTED" != true ]]; then
  listed_path=""
  if [[ -n "$LISTED_FILE" ]]; then
    if command -v realpath >/dev/null 2>&1; then
      listed_path="$(realpath "$LISTED_FILE")"
    else
      listed_path="$(cd "$(dirname "$LISTED_FILE")" 2>/dev/null && pwd)/$(basename "$LISTED_FILE")"
    fi
    if [[ ! -f "$listed_path" ]]; then echo "Listed file not found: $LISTED_FILE" >&2; listed_path=""; fi
  else
    datadir="$REPO_ROOT/data/nasdaq.com"
    if [[ -d "$datadir" ]]; then
      # pick newest by mtime
      listed_path=$(find "$datadir" -maxdepth 1 -type f -name 'nasdaq_screener_*.csv' -printf '%T@ %p\n' | sort -nr | awk 'NR==1{ $1=""; sub(/^ /, ""); print }')
    fi
  fi
  if [[ -n "$listed_path" ]]; then
    echo "Importing listed stocks from: $listed_path"
    # Warn about HTTPS dev certs on Linux/WSL
    if [[ "$APIBASE" =~ ^https:// ]]; then
      echo "Note: Using HTTPS API base ($APIBASE). On WSL/Linux, dev certificates may not be trusted by HttpClient." >&2
      echo "      If you see SSL errors, rerun with --api-base http://localhost:5256" >&2
    fi
    if [[ "$DRYRUN" == true ]]; then
      echo "(DryRun) Would run: dotnet run --no-build --project '$CLIPROJ' -- import-listed --file '$listed_path' --api '$APIBASE'"
    else
      set +e
      dotnet run --no-build --project "$CLIPROJ" -- import-listed --file "$listed_path" --api "$APIBASE" || echo "Warning: listed stocks import step failed"
      set -e
    fi
  else
    echo "Warning: No listed stocks CSV found. Skipping listed import." >&2
  fi
else
  echo "SkipListed set; skipping listed stocks import step."
fi

# Helper: extract symbol from filename (last underscore segment)
get_symbol_from_filename() {
  local fname="$1"
  local base="${fname%.*}"
  if [[ "$base" != *"_"* ]]; then
    echo ""; return 0
  fi
  local sym="${base##*_}"
  sym="${sym^^}" # upper
  echo "$sym"
}

# Process each CSV
TOTAL=${#FILES[@]}
N=0
for name in "${FILES[@]}"; do
  ((N++))
  fullpath="$FOLDER/$name"
  symbol="$(get_symbol_from_filename "$name")"
  if [[ -z "$symbol" ]]; then
    echo "[$N/$TOTAL] Skip (cannot parse symbol): $name" >&2
    continue
  fi
  echo "[$N/$TOTAL] Importing $symbol from '$name'"
  if [[ "$APIBASE" =~ ^https:// ]]; then
    echo "Note: Using HTTPS API base ($APIBASE). On WSL/Linux, dev certificates may not be trusted by HttpClient." >&2
    echo "      If you see SSL errors, rerun with --api-base http://localhost:5256" >&2
  fi
  if [[ "$DRYRUN" == true ]]; then
    continue
  fi
  args=( run --no-build --project "$CLIPROJ" -- import-historical --symbol "$symbol" --file "$fullpath" --api "$APIBASE" )
  if [[ "$WATCH" == true ]]; then args+=( --watch ); fi
  set +e
  dotnet "${args[@]}"
  rc=$?
  set -e
  if [[ $rc -ne 0 ]]; then
    echo "Warning: Import failed for $symbol (exit $rc)" >&2
  fi

done

echo "Seed historical import completed."
