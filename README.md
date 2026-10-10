# weihnachtsfeier_BIT

## Entwicklung mit Docker Desktop

Voraussetzungen: Docker Desktop mit Linux-Containern und .NET 10 SDK. Alle folgenden Befehle werden im Solution-Verzeichnis ausgeführt:

```powershell
cd WeihnachtsfeierBIT
```

### Container starten

```powershell
docker compose up --build -d
```

Danach ist die Weboberfläche unter <http://localhost:8080> erreichbar. Port `8081` stellt die API bereit. Beim Start der API werden ausstehende EF-Core-Migrationen automatisch angewendet. PostgreSQL speichert die Daten in einem Docker-Volume namens `postgres-data`.

Containerstatus und API-Startprotokoll anzeigen:

```powershell
docker compose ps
docker compose logs -f apiservice
```

Die Protokollansicht mit `Ctrl+C` beenden. Die Container laufen dabei weiter.

Container stoppen:

```powershell
docker compose down
```

Das Datenbank-Volume und die gespeicherten Daten bleiben erhalten. Nur für eine vollständig leere Datenbank:

```powershell
docker compose down --volumes
```

### EF-Core-Migrationen

Lokale EF-CLI-Version wiederherstellen:

```powershell
dotnet tool restore
```

Neue Migration aus dem aktuellen Datenmodell erzeugen:

```powershell
dotnet ef migrations add AddProjektUndTeamTabellen --project WeihnachtsfeierBit.Data --startup-project WeihnachtsfeierBit.ApiService --output-dir Migrations
```

Die Migration wird zunächst als C#-Datei unter `WeihnachtsfeierBit.Data/Migrations` angelegt. Anschließend API-Image neu bauen und starten, damit die API die Migration auf die Docker-Datenbank anwendet:

```powershell
docker compose up --build -d apiservice
docker compose logs --tail 100 apiservice
```

Die Anwendung geschieht automatisch beim API-Start über `Database.MigrateAsync()`. Ein separates `dotnet ef database update` ist für die Docker-Datenbank nicht nötig.

### Datenbankzugriff

DataGrip-Verbindung zu PostgreSQL:

```text
Host:      localhost
Port:      5432
Database:  weihnachtsfeierdb
User:      postgres
Password:  devpassword
```

Das Passwort entspricht `POSTGRES_PASSWORD`, falls diese Umgebungsvariable gesetzt wurde.

Auf der Startseite kannst du Testeinträge anlegen und die gespeicherten Zeilen anzeigen. Alternativ sind sie über `GET http://localhost:8081/test-entries` abrufbar und über `POST http://localhost:8081/test-entries` mit JSON `{ "name": "Mein Test" }` anlegbar.

### Aspire ohne Docker für API und Web

Für die lokale Entwicklung ohne Container für API und Web kannst du weiterhin den Aspire-AppHost starten:

```powershell
dotnet run --project WeihnachtsfeierBit.AppHost
```