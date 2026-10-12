# Repository instructions

## Scope and context

- This repository contains `WeihnachtsfeierBIT` and the separate `Hitster` side project. Work on `WeihnachtsfeierBIT` unless the request explicitly names Hitster.
- Before changing product or technical behavior, read [docs/index.md](docs/index.md) and the relevant linked page. Verify implementation details in source; documentation describes current behavior but is not a substitute for code.
- Keep implemented behavior separate from the future product vision. Do not describe planned participant clients, live-show control, answer collection, or project-management UI as already implemented.

## Change discipline

- Keep API business logic in focused endpoint/service classes, not in `Program.cs` or Razor components.
- Keep project scoping in every project-owned query and validate that related IDs belong to the requested project.
- Admin access requires an active admin session and a `ProjektAdminProjekt` membership. Participant access requires an active participant session for the same project.
- Store only hashes of API session tokens. Never log, document, or persist raw tokens or passwords outside their intended response/cookie flow.
- When data, routes, authorization, container behavior, or product scope changes, update the relevant page under `docs/` and its links. Prefer one focused source-of-truth page over duplicated explanations.
- Review generated EF migrations for data preservation before applying them. The API applies pending migrations when it starts.

## Validation

By default after code changes, run the full update script when Docker is available and starting the local stack is appropriate: `scripts/update-and-validate.ps1` on Windows or `bash scripts/update-and-validate.sh` on macOS/Linux. It runs lightweight validation, rebuilds and starts all Compose services, then fails if any configured service is not running. API startup can apply pending database migrations; the script does not delete the database volume.

Use the lightweight script instead when the change does not affect build/runtime behavior (for example, documentation-only changes), Docker is unavailable, or the user asks not to start services: `scripts/validate.ps1` on Windows or `bash scripts/validate.sh` on macOS/Linux. It builds the full solution and validates the Compose configuration. Do not repeat these checks manually or inspect runtime logs unless a check fails or the user asks for more. Do not run final browser or functional tests by default; the user performs final functional testing after implementation. See [development and operations](docs/development-and-operations.md).