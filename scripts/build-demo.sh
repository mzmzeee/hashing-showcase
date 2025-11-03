#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKEND_DIR="$ROOT_DIR/backend"
FRONTEND_DIR="$ROOT_DIR/frontend"
COMPOSE_FILE="$ROOT_DIR/docker-compose.yml"

require() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required tool '$1' was not found in PATH." >&2
    exit 1
  fi
}

require dotnet
require npm
require docker

cd "$BACKEND_DIR"
echo "Restoring backend dependencies..."
dotnet restore HashingDemo.sln

echo "Building backend (Release)..."
dotnet build HashingDemo.sln --configuration Release --no-restore

echo "Running backend tests..."
dotnet test HashingDemo.sln --configuration Release --no-build

cd "$FRONTEND_DIR"
if [ ! -d node_modules ]; then
  echo "Installing frontend dependencies..."
  npm install
else
  echo "Installing/updating frontend dependencies..."
  npm install
fi

echo "Running frontend tests..."
CI=true npm test -- --watch=false

echo "Building frontend production bundle..."
CI=true npm run build

cd "$ROOT_DIR"
echo "Building Docker images..."
docker compose --project-directory "$ROOT_DIR" -f "$COMPOSE_FILE" build

echo "Build completed successfully."
