# Architektur

## Solution-Projekte

| Projekt | Verantwortung |
| --- | --- |
| `WeihnachtsfeierBit.ApiService` | HTTP-API, Login, Admin-Sharing & Rechte, SignalR Hubs (`GameStateHub`, `CanvasHub`) und automatische EF-Core-Migrationen beim Start |
| `WeihnachtsfeierBit.AdminClient` | Server-seitiger Blazor-Client mit Admin-Login, Projektverwaltung, gekürzter Icon-Workspace-Sidebar, globaler Show-Bibliothek und Live-Session-Steuerung |
| `WeihnachtsfeierBit.ParticipantClient` | Server-seitiger Blazor-Client für Teilnehmer unter `/play/{projectId}` und Beameransicht unter `/play/beamer/{sessionCode}`, mit SignalR Live-Anbindung und Verbindungs-Latenzanzeige |
| `WeihnachtsfeierBit.Framework` | Rollenübergreifender Razor-Class-Library-Baustein mit generischen UI-Primitiven, API-Client, `FrameworkConnectionStatus` und gemeinsam genutzten Verträgen |
| `WeihnachtsfeierBit.Web` | Reverse-Proxy; proxyt `/admin` per YARP an den AdminClient und `/play` an den ParticipantClient |
| `WeihnachtsfeierBit.Data` | EF-Core-Entities, PostgreSQL-Mappings und Migrationen |
| `WeihnachtsfeierBit.ServiceDefaults` | Aspire Service Discovery, Health Checks, Resilience und Telemetrie |
| `WeihnachtsfeierBit.AppHost` | Lokale Aspire-Orchestrierung |

```mermaid
flowchart LR
    Browser -->|/| ReverseProxy
    Browser -->|/admin/*| ReverseProxy
    Browser -->|/play/*| ReverseProxy
    ReverseProxy -->|YARP| AdminClient
    ReverseProxy -->|YARP| ParticipantClient
    AdminClient --> API
    ParticipantClient --> API
    API --> PostgreSQL
```

## Laufzeit

`WeihnachtsfeierBIT/docker-compose.yml` startet PostgreSQL, API, Reverse-Proxy, AdminClient und ParticipantClient als Container. Am Host veröffentlicht sind Proxy `8080`, API `8081` und PostgreSQL auf `127.0.0.1:5432`. Der Adminclient-Port bleibt intern; `/admin` wird vom Proxy weitergeleitet. Aspire beschreibt denselben Dienstgraphen für lokale Entwicklung.

## Codepfade

- API-Komposition & SignalR Hubs: `WeihnachtsfeierBit.ApiService/Program.cs` (`GameStateHub` unter `/hubs/gamestate`, `CanvasHub` unter `/hubs/canvas`), Hintergrund-Worker `ScheduledEventsBackgroundService.cs` für automatische Event-Starts.
- Endpunkte: `WeihnachtsfeierBit.ApiService/Endpoints/`
- Admin Sharing & Permissions: `WeihnachtsfeierBit.ApiService/Endpoints/AdminSharingEndpoints.cs`
- Authentifizierung und Projektfilter: `WeihnachtsfeierBit.ApiService/Auth/`
- Admin-Routen, Fachansichten, Icon-Sidebar und Adminformulare: `WeihnachtsfeierBit.AdminClient/`
- Teilnehmer-Client (`/play/{projectId}`): `WeihnachtsfeierBit.ParticipantClient/`
- Generische UI-Primitiven und gemeinsame API-Adapter: `WeihnachtsfeierBit.Framework/`

Fachlogik gehört in Endpoint-/Serviceklassen. SignalR-Verbindungen werden bei Abbruch automatisch mit wachsendem Retry-Abstand wieder aufgebaut; nach Wiederverbindung synchronisieren sie ihren Gruppenbeitritt und den aktuellen Serverzustand.
