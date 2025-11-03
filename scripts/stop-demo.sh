#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/docker-compose.yml"
REMOVE_VOLUMES=${1:-}

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required to stop the services." >&2
  exit 1
fi

ARGS=(down --remove-orphans)
if [[ "$REMOVE_VOLUMES" == "--purge" ]]; then
  echo "Including volume removal."
  ARGS+=(--volumes)
fi

docker compose --project-directory "$ROOT_DIR" -f "$COMPOSE_FILE" "${ARGS[@]}"

echo "Services have been stopped."
