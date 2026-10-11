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

Danach ist der Testclient unter <http://localhost:8080> erreichbar, der Adminclient unter <http://localhost:8080/admin> und die API auf Port `8081`. Der Adminclient läuft als eigener Container, wird aber über den Testclient auf `/admin` weitergeleitet. Beim Start der API werden ausstehende EF-Core-Migrationen automatisch angewendet. PostgreSQL speichert die Daten in einem Docker-Volume namens `postgres-data`.

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

Slides werden als EF-Core-TPH-Vererbung in einer gemeinsamen Tabelle `Slides` gespeichert. Gemeinsame Werte wie Show-Zuordnung und Position sind normale Spalten; typspezifische `Parameters` liegen in einer PostgreSQL-`jsonb`-Spalte. Neue Eigenschaften an einem Parametersatz erfordern dadurch keine Datenbankmigration. Für neue Slide-Typen muss zusätzlich der EF-Discriminator in `WeihnachtsfeierDbContext` registriert werden.

Teilnehmer können mehrere `Sitzungen` besitzen. Die Sitzungen speichern einen Hash des Anmeldetokens sowie Erstellungs-, Aktivitäts-, Ablauf- und optionalen Widerrufszeitpunkt. Eine Sitzung gilt als aktiv, wenn sie nicht widerrufen und noch nicht abgelaufen ist. Der Token selbst sollte nur im `HttpOnly`-Cookie des Browsers liegen.

Parameter lassen sich wie normale mutable Objekte ändern. `SaveChanges` serialisiert den aktuellen Zustand in die JSONB-Spalte:

```csharp
var slide = new PollSlide { ShowId = showId, Position = 0 };
slide.Parameters.Question = "Welche Farbe bevorzugst du?";
slide.Parameters.Options = ["Rot", "Blau", "Grün"];
dbContext.Slides.Add(slide);
await dbContext.SaveChangesAsync();

slide.Parameters.Question = "Welche Farbe magst du am liebsten?";
await dbContext.SaveChangesAsync();
```

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

### API und Berechtigungen

Die API-Dokumentation ist in Development unter <http://localhost:8081/openapi/v1.json> verfügbar. Geschützte Requests senden das ausgegebene Token als `Authorization: Bearer <AccessToken>`. Admin-Registrierung, Admin-Login und Teilnehmernummern-Login sind auf zehn Versuche pro IP und Minute begrenzt.

Ein globales Admin-Konto registrieren und anmelden:

```powershell
$registration = Invoke-RestMethod -Method Post -Uri http://localhost:8081/api/admin-accounts -ContentType 'application/json' -Body (@{
	username = 'admin'
	password = 'EinLangesLokalesPasswort!'
} | ConvertTo-Json)
$adminToken = $registration.session.accessToken
$login = Invoke-RestMethod -Method Post -Uri http://localhost:8081/api/admin-sessions -ContentType 'application/json' -Body (@{
	username = 'admin'
	password = 'EinLangesLokalesPasswort!'
} | ConvertTo-Json)
$adminToken = $login.accessToken
```

Alternativ kannst du das Konto unter <http://localhost:8080/admin/register> registrieren und dich anschließend unter <http://localhost:8080/admin/login> anmelden. Login und Registrierung benötigen keine Projekt-ID. Ein Admin-Konto kann später mehreren Projekten zugeordnet werden; bis zur Zuordnung besitzt es noch keinen Zugriff auf Projektendpunkte. Der Adminclient zeigt zunächst den angemeldeten Namen und bietet das Profilmenü zum Abmelden.

Ein bestehender Projekt-Admin kann ein bereits registriertes Admin-Konto einem Projekt zuordnen. Projektanlage und Projektverwaltung im Adminclient sind noch nicht umgesetzt:

```powershell
$projectId = 'UUID-EINES-BESTEHENDEN-PROJEKTS'
Invoke-RestMethod -Method Post -Uri "http://localhost:8081/api/projects/$projectId/admins" -Headers @{ Authorization = "Bearer $adminToken" } -ContentType 'application/json' -Body (@{ username = 'weiterer-admin' } | ConvertTo-Json)
```

Teilnehmernummern im Projekt registrieren und Teilnehmer anmelden:

```powershell
$participant = Invoke-RestMethod -Method Post -Uri "http://localhost:8081/api/projects/$projectId/participants" -Headers @{ Authorization = "Bearer $adminToken" } -ContentType 'application/json' -Body (@{
	number = 12
	name = 'Teilnehmer 12'
} | ConvertTo-Json)
$participantSession = Invoke-RestMethod -Method Post -Uri "http://localhost:8081/api/projects/$projectId/participant-sessions" -ContentType 'application/json' -Body (@{ number = 12 } | ConvertTo-Json)
$participantToken = $participantSession.accessToken
```

Teilnehmer können Projekt, Teams, Shows, Slides und das eigene Profil lesen. Projekt-, Teilnehmer-, Team-, Show- und Slide-Änderungen benötigen ein Projekt-Admin-Token. Admins können Teilnehmer-Sitzungen ansehen und widerrufen; Teilnehmer können ihre eigenen Sitzungen ansehen und beenden. Entfernte oder abgelaufene Tokens werden bei weiteren Requests mit `401` abgewiesen, fehlende Adminrechte mit `403`.

Beispiele für geschützte Lese- und Schreibzugriffe:

```powershell
Invoke-RestMethod -Uri "http://localhost:8081/api/projects/$projectId/teams" -Headers @{ Authorization = "Bearer $participantToken" }
Invoke-RestMethod -Method Post -Uri "http://localhost:8081/api/projects/$projectId/teams" -Headers @{ Authorization = "Bearer $adminToken" } -ContentType 'application/json' -Body (@{ displayName = 'Team Rot'; color = '#d32f2f' } | ConvertTo-Json)
```

Die Dev-Testendpunkte `/test-entries` stehen nur bei `ASPNETCORE_ENVIRONMENT=Development` zur Verfügung. In den Release-Containern sind sie deaktiviert.

### Aspire ohne Docker für API und Web

Für die lokale Entwicklung ohne Container für API und Web kannst du weiterhin den Aspire-AppHost starten:

```powershell
dotnet run --project WeihnachtsfeierBit.AppHost
```