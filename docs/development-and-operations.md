# Entwicklung und Betrieb

## Vollständiges Update und Validierung

Wenn Docker verfügbar ist und der lokale Stack gestartet werden soll, bevorzugt dieses Skript vom Repository-Root aus:

```powershell
.\scripts\update-and-validate.ps1
```

Unter macOS und Linux:

```bash
bash scripts/update-and-validate.sh
```

Das Skript führt zuerst die Lightweight-Validierung aus. Nur wenn sie erfolgreich ist, baut es alle Images neu, startet den vollständigen Compose-Stack und prüft, dass alle konfigurierten Services laufen. Es sammelt keine Runtime-Logs und ersetzt keine abschließenden Browser- oder Funktionstests; diese führt der Entwickler aus. Der API-Start kann ausstehende EF-Migrationen auf die vorhandene Datenbank anwenden. Das Skript löscht das PostgreSQL-Volume nicht.

## Leichtgewichtige Validierung

Vom Repository-Root aus unter Windows:

```powershell
.\scripts\validate.ps1
```

Unter macOS und Linux:

```bash
bash scripts/validate.sh
```

Beide Skripte führen dieselben Prüfungen aus: den Build der gesamten Solution und die Validierung der Compose-Konfiguration. Sie führen beide Prüfungen auch dann aus, wenn eine fehlschlägt, und liefern bei mindestens einem Fehler einen Exitcode ungleich null. Voraussetzung sind das .NET 10 SDK und Docker Compose v2. Für die Standardvalidierung reichen diese Skripte; sie starten keine Container und sammeln keine Runtime-Logs. Abschließende Browser- und Funktionstests führt der Entwickler aus.

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

## Produktivbetrieb auf dem Server

Der Produktions-Compose-Stack läuft getrennt vom Entwicklungsstack und veröffentlicht weder PostgreSQL noch API oder App direkt auf Host-Ports. Die Web-App hängt sich an das vorhandene externe Docker-Netzwerk `proxy`; im Nginx Proxy Manager ist als Ziel `http://weihnachtsfeierbit:8080` einzutragen. TLS sollte am Reverse Proxy terminiert werden. Admin- und Teilnehmerclient sind über `/admin` und `/play` erreichbar.

Auf dem Server werden Docker Engine, Docker Compose v2, Git und das vorhandene externe Docker-Netzwerk `proxy` benötigt. Das .NET 10 SDK und PHP auf dem Host sind nicht erforderlich: Die Container-Images enthalten das .NET-Runtime-Image und werden aus den Dockerfiles gebaut. Das Repository enthält keine PHP-Anwendung; auf dem Server ist PHP nicht installiert, daher ist kein PHP-Update nötig. Die Dockerfiles verwenden `mcr.microsoft.com/dotnet/...:10.0`, sodass ein Image-Neubau den jeweils aktuellen verfügbaren .NET-10-Patchstand bezieht.

Für den initialen Start im Repository-Verzeichnis:

```bash
printf 'POSTGRES_PASSWORD=%s\n' "$(openssl rand -hex 32)" > .env
chmod 600 .env
docker compose -f WeihnachtsfeierBIT/docker-compose.production.yml -p weihnachtsfeierbit up -d --build
```

Das `.env` mit dem zufälligen Datenbankpasswort muss im Server-Checkout bleiben und darf nicht committet werden. Das PostgreSQL-Volume wird beim normalen Deployment beibehalten; `down --volumes` löscht die Produktionsdatenbank und ist zu vermeiden. Der API-Start wendet vorhandene EF-Migrationen automatisch an. Admin-Data-Protection-Keys sind derzeit nicht persistent; ein Container-Neubau kann daher Admin-Cookies ungültig machen.

### Automatisches Deployment

`.github/workflows/deploy-production.yml` deployt bei jedem Push auf `main`. Da der Server nur unter einer privaten Adresse erreichbar ist, benötigt GitHub einen persistenten, selbst gehosteten Runner auf diesem Server. Dies gibt Workflows auf `main` Zugriff auf den Docker-Daemon des Servers; Änderungen an `main` müssen daher wie privilegierter Servercode behandelt werden. Den Runner im Repository unter **Settings → Actions → Runners → New self-hosted runner** für Linux x64 einrichten und ihm zusätzlich das Label `weihnachtsfeierbit-deploy` geben. Die Deployment-Action setzt außerdem den Server-Checkout unter `/home/frankyserver/dev/weihnachtsfeier_BIT` und dessen `.env` voraus.

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