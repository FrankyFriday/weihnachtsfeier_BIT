#!/usr/bin/env bash
set -u

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/.." && pwd)"
validation_script="$script_dir/validate.sh"
compose_path="$repo_root/WeihnachtsfeierBIT/docker-compose.yml"

printf '==> Lightweight validation\n'
if ! bash "$validation_script"; then
    printf 'Validation failed; Compose services were not updated.\n' >&2
    exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
    printf "FAIL: 'docker' was not found.\n" >&2
    exit 1
fi

expected_services="$(docker compose -f "$compose_path" config --services)" || {
    printf 'FAIL: Could not read Compose services.\n' >&2
    exit 1
}

printf '\n==> Build and start all Compose services\n'
docker compose -f "$compose_path" up --build --detach || {
    printf 'FAIL: Compose could not build or start the services.\n' >&2
    exit 1
}

running_services="$(docker compose -f "$compose_path" ps --services --status running)" || {
    printf 'FAIL: Could not read running Compose services.\n' >&2
    exit 1
}

missing_services=()
while IFS= read -r service; do
    [ -n "$service" ] || continue
    if ! printf '%s\n' "$running_services" | grep -Fqx "$service"; then
        missing_services+=("$service")
    fi
done <<< "$expected_services"

if (( ${#missing_services[@]} > 0 )); then
    printf 'FAIL: Services are not running: %s\n' "${missing_services[*]}" >&2
    exit 1
fi

printf '\nAll Compose services are running. The developer can now perform final functional tests.\n'