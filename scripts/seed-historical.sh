#!/usr/bin/env bash
# Seed historical prices by importing CSVs via the .NET CLI tool.
# Converted from scripts/seed-historical.ps1 for WSL2/Ubuntu-friendly usage.

set -euo pipefail
IFS=$'\n\t'

# Defaults (mirroring the PowerShell script)
FOLDER="data/nasdaq.com/Historical"
APIBASE="https://localhost:7032"
PATTERN='HistoricalData_*_*.csv'
WATCH=false
DRYRUN=false
LISTED_FILE=""
SKIP_LISTED=false
WAIT_LISTED=true
DEBUG=false

print_usage() {
  cat <<EOF
Usage: $0 [--folder PATH] [--api-base URL] [--pattern GLOB] [--watch] [--dry-run] [--listed-file PATH] [--skip-listed] [--no-wait-listed] [--debug]

Defaults:
  --folder        <repo>/data/nasdaq.com/Historical
  --api-base      https://localhost:7032
  --pattern       HistoricalData_*_*.csv

Examples:
  $0
  $0 --api-base https://localhost:7032 --watch
  $0 --folder ./data/nasdaq.com/Historical --pattern 'HistoricalData_*_AAPL.csv' --dry-run
  $0 --listed-file ./data/nasdaq.com/nasdaq_screener_2025.csv
  # Skip waiting for listed job (not recommended)
  $0 --api-base http://localhost:5256 --skip-listed
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
    --no-wait-listed)
      WAIT_LISTED=false; shift 1;;
    --debug)
      DEBUG=true; shift 1;;
    -h|--help)
      print_usage; exit 0;;
    *)
      echo "Unknown arg: $1" >&2; print_usage; exit 2;;
  esac
done

if [[ "$DEBUG" == true ]]; then
  set -x
fi

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

# Preflight API base: try health check; if https fails on localhost, suggest or switch to http
preflight_api() {
  local base="$APIBASE"
  local health_url
  health_url="${base%%/}/health"
  if command -v curl >/dev/null 2>&1; then
    if curl --silent --fail --max-time 3 "$health_url" >/dev/null 2>&1; then
      return 0
    fi
    if [[ "$base" =~ ^https://localhost(:[0-9]+)?$ ]]; then
      # Try HTTP fallback on common dev port
      local http_base="http://localhost:5256"
      if curl --silent --fail --max-time 3 "${http_base}/health" >/dev/null 2>&1; then
        echo "HTTPS health failed; falling back to $http_base" >&2
        APIBASE="$http_base"
        return 0
      fi
    fi
  elif command -v wget >/dev/null 2>&1; then
    if wget -q --tries=1 --timeout=3 -O - "$health_url" >/dev/null 2>&1; then
      return 0
    fi
  fi
  # No change; continue with provided base
  return 0
}

CLIPROJ="$REPO_ROOT/AiStockTradeApp.Cli/AiStockTradeApp.Cli.csproj"
if [[ ! -f "$CLIPROJ" ]]; then echo "CLI project not found: $CLIPROJ" >&2; exit 1; fi

# Gather files using a simple glob to avoid delimiter quirks
shopt -s nullglob
GLOB_PATTERN="$FOLDER/${PATTERN}"
FILES=( $GLOB_PATTERN )
if [[ ${#FILES[@]} -eq 0 ]]; then
  FILES=( "$FOLDER"/HistoricalData_*.csv )
fi
if [[ ${#FILES[@]} -eq 0 ]]; then
  echo "No files found in $FOLDER matching '$PATTERN' or fallback 'HistoricalData_*.csv'" >&2
  echo "Tip: Check filenames or pass --pattern 'YourPattern.csv' and/or --folder PATH" >&2
  exit 0
fi
echo "Found ${#FILES[@]} file(s) to import in: $FOLDER"
# Print a short preview of files (up to 10)
preview_max=10
idx=0
for p in "${FILES[@]}"; do
  printf '  - %s\n' "$(basename "$p")"
  idx=$((idx+1))
  [[ $idx -ge $preview_max ]] && { echo "  ..."; break; }
done

# Build the CLI once (Debug)
echo "Building CLI (Debug)..."
dotnet build "$CLIPROJ" -c Debug >/dev/null

# Check API readiness and potentially adjust APIBASE
preflight_api

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
      # Capture output so we can extract JobId and wait for completion
      tmp_out="$(mktemp)" || tmp_out="/tmp/aistock-listed.out"
      dotnet run --no-build --project "$CLIPROJ" -- import-listed --file "$listed_path" --api "$APIBASE" | tee "$tmp_out"
      rc=$?
      set -e
      if [[ $rc -ne 0 ]]; then
        echo "Warning: listed stocks import step failed (exit $rc)" >&2
      else
        if [[ "$WAIT_LISTED" == true ]]; then
          jobId="$(grep -oE '[0-9a-fA-F-]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}' "$tmp_out" | head -n1)"
          if [[ -n "$jobId" ]]; then
            echo "Waiting for listed import job to complete: $jobId"
            set +e
            dotnet run --no-build --project "$CLIPROJ" -- check-job --job "$jobId" --api "$APIBASE" --watch
            rc2=$?
            set -e
            if [[ $rc2 -ne 0 ]]; then
              echo "Warning: listed job finished with non-success code ($rc2). Continuing with historicals." >&2
            fi
          else
            echo "Note: Could not extract JobId from output; proceeding without waiting."
          fi
        else
          echo "Skipping wait for listed job completion (--no-wait-listed)."
        fi
      fi
      rm -f "$tmp_out" 2>/dev/null || true
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
succ=0
fail=0
echo "Starting historical imports..."
set +e
for fullpath in "${FILES[@]}"; do
  ((N++))
  echo "Processing file $N/$TOTAL: $fullpath"
  name="$(basename "$fullpath")"
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
  # Show the command being executed for traceability
  echo "dotnet ${args[*]}"
  tmp_hist_out="$(mktemp)" || tmp_hist_out="/tmp/aistock-hist.out"
  set +e
  if command -v timeout >/dev/null 2>&1; then
    timeout 60s dotnet "${args[@]}" | tee "$tmp_hist_out"
  else
    dotnet "${args[@]}" | tee "$tmp_hist_out"
  fi
  rc=$?
  set -e
  if [[ $rc -ne 0 ]]; then
    echo "Warning: Import failed for $symbol (exit $rc)" >&2
    fail=$((fail+1))
  fi
  rm -f "$tmp_hist_out" 2>/dev/null || true
  echo "[$N/$TOTAL] Submitted import for $symbol (exit $rc)"
  # Gentle pacing to avoid hammering the API when not using --watch
  if [[ "$WATCH" != true ]]; then sleep 1; fi
  if [[ $rc -eq 0 ]]; then succ=$((succ+1)); fi

done
set -e

echo "Seed historical import completed. Success: $succ, Failed: $fail, Total: $TOTAL"
