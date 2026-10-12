#!/usr/bin/env bash
set -u

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/.." && pwd)"
solution_path="$repo_root/WeihnachtsfeierBIT/WeihnachtsfeierBit.slnx"
compose_path="$repo_root/WeihnachtsfeierBIT/docker-compose.yml"
failed=0

run_check() {
    local name="$1"
    shift

    printf '\n==> %s\n' "$name"
    if "$@"; then
        printf 'PASS: %s\n' "$name"
    else
        local exit_code=$?
        printf 'FAIL: %s (exit code %s)\n' "$name" "$exit_code"
        failed=1
    fi
}

run_check "Solution build" dotnet build "$solution_path"
run_check "Compose configuration" docker compose -f "$compose_path" config --quiet

if (( failed != 0 )); then
    exit 1
fi

printf '\nAll lightweight validation checks passed.\n'