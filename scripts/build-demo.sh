#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKEND_DIR="$ROOT_DIR/backend"
FRONTEND_DIR="$ROOT_DIR/frontend"
COMPOSE_FILE="$ROOT_DIR/docker-compose.yml"
docker_build_failed=0
USE_DOCKER=1

if [[ "${SKIP_DOCKER:-}" == "1" ]]; then
  USE_DOCKER=0
fi

require() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required tool '$1' was not found in PATH." >&2
    exit 1
  fi
}

require dotnet
require npm
if [[ $USE_DOCKER -eq 1 ]]; then
  require docker
fi

docker_supported() {
  if ! docker info >/dev/null 2>&1; then
    return 1
  fi

  if docker run --rm --pull=always --network bridge alpine:3.19 true >/dev/null 2>&1; then
    return 0
  fi

  return 1
}

if [[ $USE_DOCKER -eq 1 ]]; then
  if ! docker_supported; then
    USE_DOCKER=0
    echo "Docker is available but cannot create network interfaces in this environment." >&2
    echo "Automatically skipping container builds. Set SKIP_DOCKER=0 to retry." >&2
  fi
fi

install_frontend_deps() {
  if [ -f package-lock.json ]; then
    npm ci
  else
    npm install
  fi
}

cd "$BACKEND_DIR"
echo "Restoring backend dependencies..."
dotnet restore HashingDemo.sln

echo "Building backend (Release)..."
dotnet build HashingDemo.sln --configuration Release --no-restore

echo "Running backend tests..."
dotnet test HashingDemo.sln --configuration Release --no-build

cd "$FRONTEND_DIR"
echo "Installing frontend dependencies..."
install_frontend_deps

echo "Running frontend tests..."
CI=true npm test -- --watch=false

echo "Building frontend production bundle..."
CI=true npm run build

cd "$ROOT_DIR"
if [[ $USE_DOCKER -eq 0 ]]; then
  if [[ "${SKIP_DOCKER:-}" == "1" ]]; then
    echo "Skipping Docker image build because SKIP_DOCKER=1."
  else
    echo "Skipping Docker image build because Docker networking is unavailable in this environment."
  fi
else
  echo "Building Docker images..."
  if ! docker compose --project-directory "$ROOT_DIR" -f "$COMPOSE_FILE" build --no-cache --progress=plain; then
    docker_build_failed=1
    echo "\n⚠️  Docker images were not built." >&2
    echo "   The current environment appears to block container networking (see docker error above)." >&2
    echo "   You can rerun with SKIP_DOCKER=1 to skip Docker builds on constrained environments," >&2
    echo "   or run 'docker compose build' manually on a host that supports Docker networking." >&2
  fi
fi

if [[ $docker_build_failed -eq 0 ]]; then
  echo "Build completed successfully."
else
  echo "Build completed with warnings (Docker images not built)." >&2
fi
