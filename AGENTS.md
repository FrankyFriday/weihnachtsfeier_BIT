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

Run `dotnet build WeihnachtsfeierBIT/WeihnachtsfeierBit.slnx` after code changes. For container/runtime changes, validate with `docker compose -f WeihnachtsfeierBIT/docker-compose.yml config` and, when possible, rebuild and exercise the affected service. See [development and operations](docs/development-and-operations.md).