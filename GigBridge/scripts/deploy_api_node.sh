#!/usr/bin/env bash
set -Eeuo pipefail

mode="${1:-deploy}"
app_dir="${APP_DIR:-$HOME/app}"
override_name="docker-compose.production.yml"
override_path="$app_dir/$override_name"
rollback_file="${ROLLBACK_FILE:?ROLLBACK_FILE must be configured}"
health_url="${NODE_HEALTH_URL:-http://127.0.0.1:8080/health}"

compose() {
  docker compose \
    -f "$app_dir/docker-compose.yml" \
    -f "$override_path" \
    "$@"
}

wait_for_health() {
  local attempt
  for attempt in $(seq 1 20); do
    if curl --fail --silent --show-error --max-time 5 "$health_url" > /dev/null; then
      return 0
    fi
    sleep 3
  done

  echo "API node did not become healthy at $health_url." >&2
  return 1
}

container_environment_value() {
  local key="$1"
  docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' gigbridge-api |
    awk -F= -v expected_key="$key" '$1 == expected_key { sub(/^[^=]*=/, ""); print; exit }'
}

assert_container_environment() {
  local key="$1"
  local expected_value="$2"
  local actual_value
  actual_value="$(container_environment_value "$key")"

  if [ "$actual_value" != "$expected_value" ]; then
    echo "Container environment mismatch for $key." >&2
    return 1
  fi
}

verify_runtime() {
  local actual_image
  actual_image="$(docker inspect --format '{{.Config.Image}}' gigbridge-api)"
  if [ "$actual_image" != "$WEB_API_IMAGE" ]; then
    echo "Expected image $WEB_API_IMAGE but container uses $actual_image." >&2
    return 1
  fi

  assert_container_environment "Jwt__Key" "$JWT_KEY"
  assert_container_environment "Jwt__Issuer" "$JWT_ISSUER"
  assert_container_environment "Jwt__Audience" "$JWT_AUDIENCE"
  assert_container_environment "Jwt__AccessTokenMinutes" "$JWT_ACCESS_TOKEN_MINUTES"
  assert_container_environment "Jwt__RefreshTokenMinutes" "$JWT_REFRESH_TOKEN_MINUTES"
  assert_container_environment "Sentry__Release" "$SENTRY_RELEASE"
}

rollback_node() {
  if [ ! -f "$rollback_file" ]; then
    return 0
  fi

  local previous_image
  previous_image="$(tr -d '\r\n' < "$rollback_file")"
  if [ -z "$previous_image" ]; then
    echo "No previous image was recorded; automatic rollback is unavailable." >&2
    return 1
  fi

  echo "Rolling API node back to its previously running image."
  WEB_API_IMAGE="$previous_image" compose up -d --no-deps web-api
  wait_for_health
}

deploy_node() {
  local required_name
  for required_name in \
    WEB_API_IMAGE JWT_KEY JWT_ISSUER JWT_AUDIENCE \
    JWT_ACCESS_TOKEN_MINUTES JWT_REFRESH_TOKEN_MINUTES SENTRY_RELEASE \
    GHCR_USERNAME GHCR_TOKEN DEPLOY_STAGE_DIR; do
    if [ -z "${!required_name:-}" ]; then
      echo "$required_name must be configured." >&2
      return 1
    fi
  done

  local staged_override="$DEPLOY_STAGE_DIR/$override_name"
  if [ ! -f "$staged_override" ]; then
    echo "Staged Compose override was not found at $staged_override." >&2
    return 1
  fi
  if [ ! -f "$app_dir/docker-compose.yml" ]; then
    echo "Base Compose file was not found in $app_dir." >&2
    return 1
  fi

  install -m 0600 "$staged_override" "$override_path"

  local previous_image
  previous_image="$(docker inspect --format '{{.Config.Image}}' gigbridge-api 2>/dev/null || true)"
  printf '%s' "$previous_image" > "$rollback_file"

  local deploy_completed=false
  on_exit() {
    local exit_code=$?
    trap - EXIT
    if [ "$deploy_completed" != true ]; then
      set +e
      rollback_node
    fi
    exit "$exit_code"
  }
  trap on_exit EXIT

  printf '%s' "$GHCR_TOKEN" |
    docker login ghcr.io -u "$GHCR_USERNAME" --password-stdin > /dev/null
  compose config --quiet
  docker pull "$WEB_API_IMAGE"
  compose up -d --no-deps web-api
  wait_for_health
  verify_runtime

  deploy_completed=true
  trap - EXIT
}

case "$mode" in
  deploy)
    deploy_node
    ;;
  rollback)
    rollback_node
    rm -f "$rollback_file"
    ;;
  cleanup)
    rm -f "$rollback_file"
    docker image prune -f
    if [[ "${DEPLOY_STAGE_DIR:-}" == /tmp/gigbridge-deploy-* ]]; then
      rm -f \
        "$DEPLOY_STAGE_DIR/docker-compose.production.yml" \
        "$DEPLOY_STAGE_DIR/nginx.conf" \
        "$DEPLOY_STAGE_DIR/scripts/deploy_api_node.sh"
      rmdir "$DEPLOY_STAGE_DIR/scripts" "$DEPLOY_STAGE_DIR" 2>/dev/null || true
    fi
    ;;
  *)
    echo "Unsupported deployment mode: $mode" >&2
    exit 2
    ;;
esac
