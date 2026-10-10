# weihnachtsfeier_BIT

## Anwendung mit Docker Desktop

Die Container für Webfrontend, API und PostgreSQL startest du mit Docker Desktop aus dem Solution-Verzeichnis:

```powershell
cd WeihnachtsfeierBIT
docker compose up --build
```

Danach ist die Weboberfläche unter <http://localhost:8080> erreichbar. Port `8081` stellt die API bereit. Beim Start der API werden ausstehende EF-Core-Migrationen automatisch angewendet. PostgreSQL speichert die Daten in einem Docker-Volume namens `postgres-data`.

DataGrip kann sich direkt mit PostgreSQL verbinden: Host `localhost`, Port `5432`, Datenbank `weihnachtsfeierdb`, Benutzer `postgres`, Passwort `devpassword` (sofern `POSTGRES_PASSWORD` nicht überschrieben wurde).

Auf der Startseite kannst du Testeinträge anlegen und die gespeicherten Zeilen anzeigen. Alternativ sind sie über `GET http://localhost:8081/test-entries` abrufbar und über `POST http://localhost:8081/test-entries` mit JSON `{ "name": "Mein Test" }` anlegbar.

Zum Stoppen verwendest du `Ctrl+C`, danach:

```powershell
docker compose down
```

Das Volume und damit die Daten bleiben bestehen. Für einen vollständigen Neustart mit leerer Datenbank: `docker compose down --volumes`.

Für die lokale Entwicklung ohne Container für API und Web kannst du weiterhin den Aspire-AppHost starten:

```powershell
dotnet run --project WeihnachtsfeierBit.AppHost
```

Neue Migrationen werden aus dem Solution-Verzeichnis so angelegt:

```powershell
dotnet ef migrations add <Migrationsname> --project WeihnachtsfeierBit.Data --startup-project WeihnachtsfeierBit.ApiService --output-dir Migrations
```