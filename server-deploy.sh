#!/bin/bash
# ============================================================================
# Inflan backend — server-side deploy script.
#
# Run this ON the EC2 instance (via AWS Console → EC2 → instance → Connect →
# EC2 Instance Connect, while public SSH is still blocked). Pulls latest code,
# builds Release, applies EF migrations, kills the old API, launches the new
# binary, and verifies. Safe to re-run.
#
# Bootstrap (only if this script isn't on disk yet):
#     cd /home/ec2-user/inflat-api-server && git pull origin master && \
#       chmod +x server-deploy.sh && ./server-deploy.sh
#
# Everyday use (after the first successful run):
#     ./server-deploy.sh
# ============================================================================

set -euo pipefail

APP_DIR="/home/ec2-user/inflat-api-server"
APP_PORT="${PORT:-8080}"
DB_CONN="${ConnectionStrings__DefaultConnection:-Host=localhost;Database=inflan_db;Username=postgres;Password=postgres123}"
BRANCH="${BRANCH:-master}"

cd "$APP_DIR"

# --- 0. Pull latest, then re-exec so we always run the newest deploy logic ---
if [ -z "${INFLAN_DEPLOY_REEXECED:-}" ]; then
  echo "=== Pulling origin/${BRANCH} ==="
  git pull origin "$BRANCH"
  chmod +x "$0"
  INFLAN_DEPLOY_REEXECED=1 exec "$0" "$@"
fi

# --- 1. Stop the existing API -----------------------------------------------
echo "=== Stopping existing API ==="
sudo pkill -9 -f "dotnet.*inflan_api" 2>/dev/null || true
sudo fuser -k "${APP_PORT}/tcp" 2>/dev/null || true
sleep 3
if ss -ltn | grep -q ":${APP_PORT} "; then
  echo "!!! Port ${APP_PORT} is still held after kill. Aborting."
  sudo ss -ltnp | grep ":${APP_PORT} " || true
  exit 1
fi
echo "Port ${APP_PORT} is free."

# --- 2. Build ---------------------------------------------------------------
echo "=== Building (Release) ==="
dotnet build -c Release --nologo

# --- 3. Apply EF migrations -------------------------------------------------
echo "=== Applying EF migrations ==="
ConnectionStrings__DefaultConnection="$DB_CONN" dotnet ef database update

# --- 4. Launch the API ------------------------------------------------------
echo "=== Launching API on :${APP_PORT} ==="
: > app.log
nohup env \
  ASPNETCORE_ENVIRONMENT=Production \
  PORT="$APP_PORT" \
  ConnectionStrings__DefaultConnection="$DB_CONN" \
  dotnet bin/Release/net8.0/inflan_api.dll --urls="http://0.0.0.0:${APP_PORT}" \
  > app.log 2>&1 &
disown

# --- 5. Wait for startup ----------------------------------------------------
echo "=== Waiting for startup ==="
STARTED=0
for i in $(seq 1 30); do
  sleep 2
  if grep -qE "Failed to bind|Unhandled exception|Address already in use" app.log 2>/dev/null; then
    echo "!!! Startup failed — tail of app.log:"
    tail -60 app.log
    exit 1
  fi
  if ss -ltn | grep -q ":${APP_PORT} " && grep -qE "Application started|Now listening" app.log 2>/dev/null; then
    STARTED=1
    echo "Started after $((i*2))s."
    break
  fi
done
if [ "$STARTED" -ne 1 ]; then
  echo "!!! Timed out waiting for app to start. Tail of app.log:"
  tail -60 app.log
  exit 1
fi

# --- 6. Verify --------------------------------------------------------------
echo ""
echo "=== Verification ==="
echo "--- Process ---"
pgrep -af "dotnet.*inflan_api"
echo "--- Port ${APP_PORT} ---"
ss -ltn | grep ":${APP_PORT} "
echo "--- Swagger paths (Auth + PostSchedule) ---"
curl -fsS "http://localhost:${APP_PORT}/swagger/v1/swagger.json" \
  | grep -oE '"/api/(Auth|PostSchedule)[^"]*"' | sort -u || echo "(none found)"
echo "--- Migrations mentioned in app.log ---"
grep -oE "20[0-9]{12}_[A-Za-z]+" app.log | sort -u | tail -5 || true
echo ""
echo "✅ Deploy complete."
