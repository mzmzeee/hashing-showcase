#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FRONTEND_DIR="$ROOT_DIR/frontend"
BACKEND_DIR="$ROOT_DIR/backend"
ANIMATION_DIR="$ROOT_DIR/animation_service"
ENV_FILE="$ROOT_DIR/.env"
ENV_EXAMPLE="$ROOT_DIR/.env.example"
COMPOSE_FILE="$ROOT_DIR/docker-compose.yml"

# Ports default values are re-applied after loading .env (see below).
API_PORT=${API_PORT:-8080}
FRONTEND_PORT=${FRONTEND_PORT:-3000}
ANIMATION_API_PORT=${ANIMATION_API_PORT:-5000}
USE_DOCKER=1
if [[ "${SKIP_DOCKER:-}" == "1" ]]; then
  USE_DOCKER=0
fi

LOCAL_API_PID=""
LOCAL_ANIMATION_PID=""
LOCAL_STACK_STARTED=0

require() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required tool '$1' was not found in PATH." >&2
    exit 1
  fi
}

wait_for_endpoint() {
  local name="$1"
  local url="$2"
  local method="${3:-GET}"
  local max_attempts=30

  echo "Waiting for ${name} to become reachable (${url})..."
  for ((attempt = 1; attempt <= max_attempts; attempt++)); do
    local status
    status=$(curl -sS -X "$method" -o /dev/null -w "%{http_code}" "$url" 2>/dev/null || printf '000')
    if [[ "$status" != "000" ]]; then
      return 0
    fi
    sleep 2
  done

  echo "${name} did not become ready in time; check docker compose logs." >&2
  exit 1
}

cleanup() {
  if [[ $USE_DOCKER -eq 1 ]]; then
    printf '\nShutting down Docker services...\n'
    docker compose --project-directory "$ROOT_DIR" -f "$COMPOSE_FILE" down --remove-orphans >/dev/null 2>&1 || true
  else
    if [[ $LOCAL_STACK_STARTED -eq 1 ]]; then
      if [[ -n "$LOCAL_API_PID" ]]; then
        kill "$LOCAL_API_PID" >/dev/null 2>&1 || true
        wait "$LOCAL_API_PID" >/dev/null 2>&1 || true
      fi
      if [[ -n "$LOCAL_ANIMATION_PID" ]]; then
        kill "$LOCAL_ANIMATION_PID" >/dev/null 2>&1 || true
        wait "$LOCAL_ANIMATION_PID" >/dev/null 2>&1 || true
      fi
    fi
  fi
}

trap cleanup EXIT

require npm
require curl

docker_supported() {
  if ! docker info >/dev/null 2>&1; then
    return 1
  fi

  if docker run --rm --pull=always --network bridge alpine:3.19 true >/dev/null 2>&1; then
    return 0
  fi

  return 1
}

wait_for_postgres() {
  local host="$POSTGRES_HOST"
  local port="$POSTGRES_PORT"
  local db="$POSTGRES_DB"
  local user="$POSTGRES_USER"
  local password="$POSTGRES_PASSWORD"
  local max_attempts=10

  echo "Waiting for PostgreSQL at ${host}:${port}/${db}..."
  for ((attempt = 1; attempt <= max_attempts; attempt++)); do
    if PGPASSWORD="$password" psql -h "$host" -p "$port" -U "$user" -d "$db" -c 'SELECT 1;' >/dev/null 2>&1; then
      return 0
    fi
    sleep 2
  done

  echo "Unable to connect to PostgreSQL using the credentials in .env." >&2
  exit 1
}

ensure_animation_env() {
  local venv_path="$ANIMATION_DIR/.venv"
  if [ ! -d "$venv_path" ]; then
    echo "Creating Python virtual environment for animation service..."
    python3 -m venv "$venv_path"
  fi

  "$venv_path/bin/pip" install --upgrade pip >/dev/null
  "$venv_path/bin/pip" install -r "$ANIMATION_DIR/requirements.txt" >/dev/null
}

start_local_animation_service() {
  ensure_animation_env
  echo "Starting animation service locally on port ${ANIMATION_API_PORT}..."
  (
    cd "$ANIMATION_DIR"
    "$ANIMATION_DIR/.venv/bin/python" app.py
  ) &
  LOCAL_ANIMATION_PID=$!
}

start_local_api() {
  local connection
  printf -v connection 'Host=%s;Port=%s;Database=%s;Username=%s;Password=%s' \
    "$POSTGRES_HOST" "$POSTGRES_PORT" "$POSTGRES_DB" "$POSTGRES_USER" "$POSTGRES_PASSWORD"

  echo "Starting API locally on port ${API_PORT}..."
  (
    cd "$BACKEND_DIR"
    ConnectionStrings__DefaultConnection="$connection" \
    PasswordHashing__Iterations="$PASSWORD_HASHING_ITERATIONS" \
    AnimationService__BaseUrl="http://127.0.0.1:${ANIMATION_API_PORT}/" \
    ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS="http://0.0.0.0:${API_PORT}" \
    dotnet run --project src/Api/Api.csproj --configuration Development --no-build
  ) &
  LOCAL_API_PID=$!
}

if [[ $USE_DOCKER -eq 1 ]]; then
  if ! docker_supported; then
    USE_DOCKER=0
    echo "Docker is available but cannot create network interfaces in this environment." >&2
    echo "Automatically skipping container startup. Set SKIP_DOCKER=0 to retry." >&2
  fi
fi

if [ ! -f "$ENV_FILE" ]; then
  if [ -f "$ENV_EXAMPLE" ]; then
    echo "Creating .env from .env.example..."
    cp "$ENV_EXAMPLE" "$ENV_FILE"
  else
    echo "Missing environment file (.env). Create one before starting the demo." >&2
    exit 1
  fi
fi

set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a

API_PORT=${API_PORT:-8080}
FRONTEND_PORT=${FRONTEND_PORT:-3000}
ANIMATION_API_PORT=${ANIMATION_API_PORT:-5000}
PASSWORD_HASHING_ITERATIONS=${PASSWORD_HASHING_ITERATIONS:-10000}
POSTGRES_HOST=${POSTGRES_HOST:-localhost}
POSTGRES_PORT=${POSTGRES_PORT:-5432}
POSTGRES_DB=${POSTGRES_DB:-hashing_demo}
POSTGRES_USER=${POSTGRES_USER:-user}
POSTGRES_PASSWORD=${POSTGRES_PASSWORD:-password}

if [ ! -d "$FRONTEND_DIR/node_modules" ]; then
  echo "Installing frontend dependencies..."
  if [ -f "$FRONTEND_DIR/package-lock.json" ]; then
    (cd "$FRONTEND_DIR" && npm ci)
  else
    (cd "$FRONTEND_DIR" && npm install)
  fi
else
  echo "Ensuring frontend dependencies are up to date..."
  (cd "$FRONTEND_DIR" && npm install)
fi

if [[ $USE_DOCKER -eq 1 ]]; then
  echo "Starting PostgreSQL, API, and animation services..."
  echo "Note: Shared volume 'animation_videos' is configured for API and animation service."
  if ! docker compose --project-directory "$ROOT_DIR" -f "$COMPOSE_FILE" up --build -d; then
    echo "\nFailed to start Docker services. Check that your environment supports Docker networking" >&2
    echo "or rerun with SKIP_DOCKER=1 if you plan to point at externally hosted services." >&2
    exit 1
  fi

  wait_for_endpoint "API" "http://localhost:${API_PORT}/swagger/index.html"
  wait_for_endpoint "Animation service" "http://localhost:${ANIMATION_API_PORT}/generate-animation" OPTIONS

  cat <<EOF

API ready at   http://localhost:${API_PORT}
Animation API: http://localhost:${ANIMATION_API_PORT}
Launching React development server on port ${FRONTEND_PORT} (press Ctrl+C to stop).
EOF
else
  if [[ "${SKIP_DOCKER:-}" == "1" ]]; then
    echo "SKIP_DOCKER=1 detected – skipping Docker services. Ensure your API and animation endpoints are reachable."
  fi

  start_local_animation_service
  start_local_api
  LOCAL_STACK_STARTED=1

  wait_for_endpoint "API" "http://127.0.0.1:${API_PORT}/swagger/index.html"
  wait_for_endpoint "Animation service" "http://127.0.0.1:${ANIMATION_API_PORT}/generate-animation" OPTIONS
fi

cd "$FRONTEND_DIR"
BROWSER=none PORT="$FRONTEND_PORT" npm start
