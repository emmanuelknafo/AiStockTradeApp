#!/usr/bin/env bash
set -euo pipefail

# sql-conn-auth-dump.sh
# Dumps authentication mode for the DefaultConnection of a Web App and warns on blank SQL passwords.
# Usage: sql-conn-auth-dump.sh <resource-group> <webapp-name>

if [ $# -lt 2 ]; then
  echo "Usage: $0 <resource-group> <webapp-name>" >&2
  exit 1
fi

RG="$1"; APP="$2"
RAW_CONN=$(az webapp config connection-string list --name "$APP" --resource-group "$RG" -o json 2>/dev/null | jq -r '.[] | select(.name=="DefaultConnection") | .connectionString' || echo "")
if [ -z "$RAW_CONN" ]; then
  echo "[DIAG] No DefaultConnection retrieved (may be unset or insufficient permissions)"; exit 0
fi

if echo "$RAW_CONN" | grep -qi 'Authentication=Active Directory Default'; then
  echo "[DIAG] Connection string uses Azure AD authentication"
else
  MASKED=$(echo "$RAW_CONN" | sed -E 's/Password=([^;]{0,4})[^;]*/Password=***MASKED***/I')
  echo "[DIAG] Connection string (masked): $MASKED"
  if echo "$RAW_CONN" | grep -qi 'User ID=sqladmin' && echo "$RAW_CONN" | grep -qi 'Password=;' ; then
    echo "[DIAG][WARN] SQL admin password appears blank in connection string. Recommended: enforce Azure AD (Entra) only auth and remove SQL logins."
  fi
fi
