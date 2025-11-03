#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FRONTEND_DIR="$ROOT_DIR/frontend"
ENV_FILE="$ROOT_DIR/.env"
ENV_EXAMPLE="$ROOT_DIR/.env.example"
COMPOSE_FILE="$ROOT_DIR/docker-compose.yml"
API_PORT=${API_PORT:-8080}
FRONTEND_PORT=${FRONTEND_PORT:-3000}

require() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required tool '$1' was not found in PATH." >&2
    exit 1
  fi
}

cleanup() {
  echo "\nShutting down Docker services..."
  docker compose --project-directory "$ROOT_DIR" -f "$COMPOSE_FILE" down --remove-orphans >/dev/null 2>&1 || true
}

trap cleanup EXIT

require docker
require npm
require curl

if [ ! -f "$ENV_FILE" ]; then
  if [ -f "$ENV_EXAMPLE" ]; then
    echo "Creating .env from .env.example..."
    cp "$ENV_EXAMPLE" "$ENV_FILE"
  else
    echo "Missing environment file (.env). Create one before starting the demo." >&2
    exit 1
  fi
fi

if [ ! -d "$FRONTEND_DIR/node_modules" ]; then
  echo "Installing frontend dependencies..."
  (cd "$FRONTEND_DIR" && npm install)
fi

echo "Starting PostgreSQL and API containers..."
docker compose --project-directory "$ROOT_DIR" -f "$COMPOSE_FILE" up --build -d

echo "Waiting for API to become reachable on port $API_PORT..."
for attempt in {1..30}; do
  if curl -fsS -o /dev/null "http://localhost:${API_PORT}/swagger/index.html"; then
    break
  fi
  sleep 2
  if [ "$attempt" -eq 30 ]; then
    echo "API did not become ready in time; check docker compose logs." >&2
  fi
done

cat <<EOF

API is ready at http://localhost:${API_PORT}
Launching React development server on port ${FRONTEND_PORT} (press Ctrl+C to stop).
EOF

cd "$FRONTEND_DIR"
BROWSER=none PORT="$FRONTEND_PORT" npm start
