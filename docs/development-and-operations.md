# Entwicklung und Betrieb

## Docker Desktop

Voraussetzungen: .NET 10 SDK und Docker Desktop mit Linux-Containern. Aus dem Repository-Root:

```powershell
cd WeihnachtsfeierBIT
docker compose up --build -d
```

- Testclient: <http://localhost:8080>
- Adminclient: <http://localhost:8080/admin>
- API: <http://localhost:8081>
- OpenAPI (Development): <http://localhost:8081/openapi/v1.json>

```powershell
docker compose ps
docker compose logs -f apiservice
docker compose down
```

`docker compose down` behält das PostgreSQL-Volume. `docker compose down --volumes` löscht die Datenbank vollständig.

## Migrationen

Aus dem Solution-Verzeichnis `WeihnachtsfeierBIT`:

```powershell
dotnet tool restore
dotnet ef migrations add MeaningfulMigrationName --project WeihnachtsfeierBit.Data --startup-project WeihnachtsfeierBit.ApiService --output-dir Migrations
docker compose up --build -d apiservice
docker compose logs --tail 100 apiservice
```

Vor Anwendung generierte Migrationen prüfen, insbesondere bei Datenumformungen oder EF-Warnungen zu Datenverlust. Die API ruft `Database.MigrateAsync()` beim Start auf. Der Host-CLI-Befehl `dotnet ef database update` besitzt nicht automatisch die von Aspire/Compose injizierte Verbindung.

## DataGrip

Host `localhost`, Port `5432`, Datenbank `weihnachtsfeierdb`, Benutzer `postgres`, Passwort `devpassword` oder der Wert von `POSTGRES_PASSWORD`. PostgreSQL ist nur an `127.0.0.1` veröffentlicht.

## Aspire und Build

Lokale Aspire-Orchestrierung ohne Compose:

```powershell
dotnet run --project WeihnachtsfeierBit.AppHost
```

Gesamte Solution bauen:

```powershell
dotnet build WeihnachtsfeierBit.slnx
```

## Diagnose und Sicherheitsnotizen

- `docker compose logs --tail 100 apiservice` zeigt Datenbankverbindung und Migrationen.
- `GET /api/projects/{id}/teams` ohne Token sollte `401`, mit Teilnehmer-Token `200` liefern.
- Ein Schreibzugriff mit Teilnehmer- statt Admin-Token sollte `403` liefern.
- Die `/test-entries`-Routen existieren nur in Development.
- Data-Protection-Keys des Adminclients liegen derzeit nicht in einem persistenten Volume. Nach Container-Neubau können Admin-Cookies ungültig sein; vor dauerhaftem Mehrinstanzbetrieb Key-Ring persistieren und schützen.