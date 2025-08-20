#!/usr/bin/env bash
# Dev helper to run the app either via Docker (clean rebuild + up) or locally for debugging.
# Converted from scripts/start.ps1 for WSL2 / Ubuntu.

set -euo pipefail
IFS=$'\n\t'

# Defaults (match PowerShell defaults where reasonable)
MODE="Local"
SQL_SERVER='.'
SQL_DATABASE='StockTraderDb'
API_PROFILE='https'
UI_PROFILE='https'
USE_HTTPS=true

print_usage() {
  cat <<EOF
Usage: $0 [--mode Docker|Local] [--sql-server HOST] [--sql-database NAME] [--api-profile NAME] [--ui-profile NAME] [--no-https]

Examples:
  $0 --mode Docker
  $0 --mode Local --sql-server . --sql-database StockTraderDb
EOF
}

# Parse args (simple)
while [[ $# -gt 0 ]]; do
  case "$1" in
    --mode)
      MODE="$2"; shift 2;;
    --sql-server)
      SQL_SERVER="$2"; shift 2;;
    --sql-database)
      SQL_DATABASE="$2"; shift 2;;
    --api-profile)
      API_PROFILE="$2"; shift 2;;
    --ui-profile)
      UI_PROFILE="$2"; shift 2;;
    --no-https)
      USE_HTTPS=false; shift 1;;
    -h|--help)
      print_usage; exit 0;;
    *)
      echo "Unknown arg: $1"; print_usage; exit 2;;
  esac
done

# Resolve paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
COMPOSE_FILE="$REPO_ROOT/docker-compose.yml"
API_PROJ="$REPO_ROOT/AiStockTradeApp.Api/AiStockTradeApp.Api.csproj"
UI_PROJ="$REPO_ROOT/AiStockTradeApp/AiStockTradeApp.csproj"
SLN_PATH="$REPO_ROOT/AiStockTradeApp.sln"
LOG_DIR="$REPO_ROOT/logs"
mkdir -p "$LOG_DIR"

command_exists() {
  command -v "$1" >/dev/null 2>&1
}

docker_clean_up_and_up() {
  if ! command_exists docker; then
    echo "Docker CLI not found. Please install Docker and ensure \"docker\" is on PATH." >&2
    return 1
  fi
  if [[ ! -f "$COMPOSE_FILE" ]]; then
    echo "Compose file not found at $COMPOSE_FILE" >&2
    return 1
  fi

  echo "Stopping containers and removing images, networks, and volumes..."
  docker compose -f "$COMPOSE_FILE" down --rmi all --volumes --remove-orphans || true

  echo "Rebuilding images with no cache..."
  docker compose -f "$COMPOSE_FILE" build --no-cache

  echo "Starting containers in detached mode..."
  docker compose -f "$COMPOSE_FILE" up -d --force-recreate

  echo "Waiting for SQL Server container to become healthy..."
  timeout_sec=180
  poll_interval_sec=5
  deadline=$((SECONDS + timeout_sec))
  status=""

  while [[ $SECONDS -lt $deadline ]]; do
    sleep $poll_interval_sec
    id=$(docker compose -f "$COMPOSE_FILE" ps -q sqlserver || true)
    id=$(echo "$id" | tr -d '\r\n' )
    if [[ -z "$id" ]]; then
      continue
    fi
    status=$(docker inspect -f '{{.State.Health.Status}}' "$id" 2>/dev/null || true)
    status=$(echo "$status" | tr -d '\r\n')
    if [[ "$status" == "healthy" ]]; then
      echo "SQL Server is healthy."
      break
    else
      echo "Current SQL health: ${status:-unknown}"
    fi
  done

  if [[ "$status" != "healthy" ]]; then
    echo "Warning: SQL Server did not report healthy within the timeout. The API may retry migrations until ready." >&2
  fi

  echo "Compose status:"
  docker compose -f "$COMPOSE_FILE" ps
}

enable_dev_https_cert() {
  if [[ "$USE_HTTPS" != true ]]; then
    return 0
  fi
  if ! command_exists dotnet; then
    return 0
  fi

  if dotnet dev-certs https --check >/dev/null 2>&1; then
    return 0
  fi

  echo "Trusting .NET developer HTTPS certificate..."
  # On Linux this may require user interaction or be a no-op depending on distro
  dotnet dev-certs https --trust || true
}

start_local_processes() {
  if ! command_exists dotnet; then
    echo "dotnet SDK not found. Please install .NET SDK and ensure \"dotnet\" is on PATH." >&2
    return 1
  fi
  if [[ ! -f "$API_PROJ" ]]; then
    echo "API project not found: $API_PROJ" >&2
    return 1
  fi
  if [[ ! -f "$UI_PROJ" ]]; then
    echo "UI project not found: $UI_PROJ" >&2
    return 1
  fi

  enable_dev_https_cert

  cs="Server=$SQL_SERVER;Database=$SQL_DATABASE;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true"
  echo "Using connection string: $cs"

  echo "Restoring solution packages..."
  dotnet restore "$SLN_PATH"
  echo "Building solution (Debug)..."
  dotnet build "$SLN_PATH" -c Debug

  # Launch API in background and log output
  api_log="$LOG_DIR/api.log"
  echo "Launching API (background). Logs: $api_log"
  # Environment for API
  (
    export ConnectionStrings__DefaultConnection="$cs"
    export USE_INMEMORY_DB="false"
    export ASPNETCORE_ENVIRONMENT="Development"
    cd "$REPO_ROOT"
    nohup dotnet run --no-build --project "$API_PROJ" --launch-profile "$API_PROFILE" >"$api_log" 2>&1 &
  )

  # Wait for API to be responsive before starting UI
  api_health_url_http='http://localhost:5256/health'
  api_health_url_https='https://localhost:7032/health'
  timeout_sec=120
  poll_interval_sec=2
  deadline=$((SECONDS + timeout_sec))
  api_ready=false

  echo "Waiting for API to be ready at $api_health_url_http (fallback: $api_health_url_https)..."
  while [[ $SECONDS -lt $deadline ]]; do
    if command_exists curl; then
      if curl --silent --show-error --fail --max-time 5 "$api_health_url_http" >/dev/null 2>&1; then
        api_ready=true; break
      fi
      if curl --silent --show-error --insecure --fail --max-time 5 "$api_health_url_https" >/dev/null 2>&1; then
        api_ready=true; break
      fi
    else
      # Fallback using wget if curl isn't available
      if command_exists wget; then
        if wget -q --tries=1 --timeout=5 -O - "$api_health_url_http" >/dev/null 2>&1; then
          api_ready=true; break
        fi
        if wget --no-check-certificate -q --tries=1 --timeout=5 -O - "$api_health_url_https" >/dev/null 2>&1; then
          api_ready=true; break
        fi
      else
        echo "No http client (curl/wget) found to perform health checks." >&2
        break
      fi
    fi
    sleep $poll_interval_sec
  done

  if [[ "$api_ready" != true ]]; then
    echo "Warning: API did not respond healthy within ${timeout_sec}s. UI will not be started to avoid failures. Check $api_log for API output." >&2
    return 0
  fi

  echo "API is healthy. Starting UI..."
  ui_log="$LOG_DIR/ui.log"
  echo "Launching UI (background). Logs: $ui_log"
  (
    export ASPNETCORE_ENVIRONMENT="Development"
    export StockApi__BaseUrl='https://localhost:7032'
    export StockApi__HttpBaseUrl='http://localhost:5256'
    cd "$REPO_ROOT"
    nohup dotnet run --no-build --project "$UI_PROJ" --launch-profile "$UI_PROFILE" >"$ui_log" 2>&1 &
  )

  echo "Local processes started. API logs: $api_log, UI logs: $ui_log"
}

case "$MODE" in
  Docker|docker)
    docker_clean_up_and_up
    ;;
  Local|local)
    start_local_processes
    ;;
  *)
    echo "Unknown mode: $MODE" >&2
    exit 2
    ;;
esac

exit 0
