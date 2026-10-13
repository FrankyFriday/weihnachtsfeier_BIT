# Architektur

## Solution-Projekte

| Projekt | Verantwortung |
| --- | --- |
| `WeihnachtsfeierBit.ApiService` | HTTP-API, Login, Projektberechtigungen und EF-Core-Migrationen beim Start |
| `WeihnachtsfeierBit.AdminClient` | Server-seitiger Blazor-Client mit Admin-Login, Projektverwaltung, Workspace-Sidebar, globaler Show-Bibliothek und projektspezifischen Show-Verknüpfungen |
| `WeihnachtsfeierBit.Framework` | Rollenübergreifender Razor-Class-Library-Baustein mit generischen UI-Primitiven, API-Client und gemeinsam genutzten Verträgen; enthält keine Admin-/Projektseiten |
| `WeihnachtsfeierBit.Web` | Entwicklungs-/Testclient; proxyt `/admin` per YARP an den AdminClient |
| `WeihnachtsfeierBit.Data` | EF-Core-Entities, PostgreSQL-Mappings und Migrationen |
| `WeihnachtsfeierBit.ServiceDefaults` | Aspire Service Discovery, Health Checks, Resilience und Telemetrie |
| `WeihnachtsfeierBit.AppHost` | Lokale Aspire-Orchestrierung |

```mermaid
flowchart LR
    Browser -->|/| TestClient
    Browser -->|/admin/*| TestClient
    TestClient -->|YARP| AdminClient
    TestClient --> API
    AdminClient --> API
    API --> PostgreSQL
```

## Laufzeit

`WeihnachtsfeierBIT/docker-compose.yml` startet PostgreSQL, API, Testclient und Adminclient als Container. Am Host veröffentlicht sind Testclient `8080`, API `8081` und PostgreSQL ausschließlich auf `127.0.0.1:5432`. Der Adminclient-Port bleibt intern; `/admin` wird vom Testclient weitergeleitet. Aspire beschreibt denselben Dienstgraphen für lokale Entwicklung.

`WeihnachtsfeierBit.Data` und `WeihnachtsfeierBit.ServiceDefaults` sind Bibliotheken und laufen nicht als eigene Container. `Hitster/` gehört nicht zu diesem Dienstgraphen.

## Codepfade

- API-Komposition: `WeihnachtsfeierBit.ApiService/Program.cs`
- Routenmodule: `WeihnachtsfeierBit.ApiService/Endpoints/`
- Authentifizierung und Projektfilter: `WeihnachtsfeierBit.ApiService/Auth/`
- Request-/Response-Verträge: `WeihnachtsfeierBit.ApiService/Contracts/`
- Entities und Mapping: `WeihnachtsfeierBit.Data/`
- Admin-Routen, Fachansichten, Sidebar und Adminformulare: `WeihnachtsfeierBit.AdminClient/`
- Generische UI-Primitiven und gemeinsame API-Adapter: `WeihnachtsfeierBit.Framework/`
- Testclient und Proxy: `WeihnachtsfeierBit.Web/Program.cs`

Fachlogik gehört in Endpoint-/Serviceklassen, nicht in `Program.cs` oder Razor-Komponenten. Fachansichten und Navigation bleiben im jeweiligen Client; nur zweckneutrale UI-Primitiven und wiederverwendbare API-Verträge liegen im Framework. Weitere Oberflächen wie Teilnehmerclient und Leinwand-Dashboard sollen später eigenständige Clients sein und können dieselbe Framework-Bibliothek verwenden.

## Produktgrenzen

Datenmodell, Login und CRUD-Grundlagen existieren. Live-Showzustand, Antworten, Punktehistorie, Teilnehmerclient und Leinwand-Dashboard fehlen noch. Siehe [Produktvision und Rollen](product-and-roles.md).