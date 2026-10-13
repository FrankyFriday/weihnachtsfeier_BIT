# WeihnachtsfeierBIT

WeihnachtsfeierBIT ist die aktuelle Anwendung in diesem Repository: eine Grundlage für Event-Projekte mit Teams, Punkten, Shows und konfigurierbaren Slides. Die [Produktvision](docs/product-and-roles.md) beschreibt, was bereits existiert und was noch geplant ist.

## Schnellstart

Voraussetzung: Docker Desktop mit Linux-Containern. Im Repository-Root:

```powershell
cd WeihnachtsfeierBIT
docker compose up --build -d
```

- Testclient: <http://localhost:8080>
- Adminclient: <http://localhost:8080/admin>
- API: <http://localhost:8081>
- OpenAPI (Development): <http://localhost:8081/openapi/v1.json>

Zum Stoppen im Solution-Verzeichnis `docker compose down` ausführen. Das PostgreSQL-Volume bleibt dabei erhalten. Ausführliche Befehle stehen in [Entwicklung und Betrieb](docs/development-and-operations.md).

Im Admin-Panel werden zunächst Projekte angelegt. Shows sind globale, benannte Abläufe: Sie werden im Workspace-Menüpunkt „Shows“ erstellt und anschließend mit einem oder mehreren Projekten verknüpft. Das Entfernen eines Projektlinks löscht die Show nicht. Der Show-Editor ist aktuell ein leerer Platzhalter; Bearbeitung von Slides folgt separat. Details stehen in [Produktvision und Rollen](docs/product-and-roles.md), [Datenmodell](docs/data-model.md) und [API und Berechtigungen](docs/api-and-auth.md).

## Dokumentation

- [Dokumentationsübersicht](docs/index.md)
- [Produktvision und Rollen](docs/product-and-roles.md)
- [Architektur](docs/architecture.md)
- [Datenmodell](docs/data-model.md)
- [API und Berechtigungen](docs/api-and-auth.md)
- [Entwicklung und Betrieb](docs/development-and-operations.md)
- [Agent-Regeln](AGENTS.md)

`Hitster/` ist ein unabhängiges Randprojekt. Es ist aktuell nicht in WeihnachtsfeierBIT integriert und kein Fokus dieser Dokumentation.