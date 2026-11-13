# Gesamtsystem aufsetzen und betreiben

Diese Anleitung beschreibt den **WeihnachtsfeierBIT-Stack**, seine Docker-Konfiguration und den Betrieb hinter Nginx Proxy Manager. Die Hitster-Slide ist Bestandteil von WeihnachtsfeierBIT; ihre Songliste aus `Hitster/data/songs.json` wird beim Build in die API eingebettet. Änderungen an der Playlist erfordern deshalb einen neuen API-Build. Der eigenständige Ordner `Hitster/` wird hier nicht eingerichtet oder verändert.

Es gibt keine mitgelieferte Nginx- oder Nginx-Proxy-Manager-Konfigurationsdatei. Die NPM-Schritte unten beschreiben die Werte, die in der NPM-Weboberfläche einzutragen sind; Domains und Zugangsdaten sind ausdrücklich Platzhalter.

## 1. Voraussetzungen

### Entwicklungsrechner

- Git
- Docker Desktop mit Linux-Containern und Docker Compose v2
- .NET 10 SDK für `scripts/validate.ps1` und Builds außerhalb von Docker

### Server

- Docker Engine und Docker Compose v2
- Git für manuelle Updates
- Eine erreichbare Nginx Proxy Manager-Instanz (NPM)
- DNS-Einträge für die gewünschten Domains, die auf die öffentliche IP-Adresse des NPM zeigen
- Für Spotify-Wiedergabe ein Spotify-Konto mit Premium. Bei einer Spotify-App im Entwicklungsmodus muss der verwendete Account dort freigeschaltet sein.

Auf dem Server ist kein .NET SDK erforderlich: Die Dockerfiles bauen die .NET-Dienste mit dem .NET-10-SDK-Image; die Laufzeit-Images enthalten ASP.NET Core.

## 2. Dienste und Datenhaltung

### WeihnachtsfeierBIT

`WeihnachtsfeierBIT/docker-compose.yml` startet lokal diese Dienste:

| Dienst | Aufgabe | Lokaler Zugriff |
| --- | --- | --- |
| `postgres` | PostgreSQL 17-Datenbank | `127.0.0.1:5432` |
| `apiservice` | API, Migrationen und serverseitige Hitster-Logik | `localhost:8081` |
| `adminclient` | Admin-UI | über `webfrontend` |
| `participantclient` | Teilnehmer-UI | über `webfrontend` |
| `webfrontend` | gemeinsamer Web-Einstieg und Weiterleitung | `localhost:8080` |

Die Produktionsdatei `WeihnachtsfeierBIT/docker-compose.production.yml` veröffentlicht keine App-, API- oder PostgreSQL-Ports auf dem Host. Alle fünf Container hängen im externen Docker-Netzwerk `proxy`; `webfrontend` hat dort den Netzwerknamen `weihnachtsfeierbit` für NPM. PostgreSQL und API sind damit für andere Container im `proxy`-Netzwerk erreichbar, aber nicht direkt über Host-Ports. Dieses Netzwerk nur vertrauenswürdigen Containern zugänglich machen.

Die Compose-Dateien definieren folgende persistente Volumes:

- `postgres-data`: PostgreSQL-Datenbank
- `admin-dataprotection-keys`, `participant-dataprotection-keys`, `web-dataprotection-keys`: getrennte ASP.NET-Data-Protection-Schlüsselringe. Nicht löschen; sie schützen unter anderem Cookies und Antiforgery-Tokens.

Die API wendet beim Start vorhandene EF-Migrationen an. Das erstmalige Anlegen der Data-Protection-Volumes kann dazu führen, dass bestehende Browser-Sitzungen einmal neu geladen werden müssen.

## 3. Spotify und Konfiguration

Spotify-Redirect-URIs müssen im Spotify Developer Dashboard **exakt** eingetragen sein. Niemals echte Passwörter, Hashes oder andere Secrets in Git committen.

### Hitster-Slide in WeihnachtsfeierBIT

Die API erwartet diese Umgebungsvariablen:

| Variable | Wert |
| --- | --- |
| `SPOTIFY_CLIENT_ID` | Client-ID der Spotify-App |
| `SPOTIFY_REDIRECT_URI` | Exakte Callback-URL, z. B. `https://<APP-DOMAIN>/admin/spotify-callback.html` |

Der OAuth-Flow verwendet PKCE; ein Spotify-Client-Secret wird für diese Integration nicht benötigt. Für lokale Nutzung die Redirect-URI auch in Spotify eintragen und die Anwendung unter derselben Host-Adresse öffnen, die in der Callback-Konfiguration verwendet wird, z. B. `http://127.0.0.1:8080/admin/spotify-callback.html`.

Die lokale Compose-Datei liest diese Werte aus `.env` im `WeihnachtsfeierBIT`-Verzeichnis. Die Produktionsdatei liest sie aus `.env` im Repository-Root (siehe Startbefehle weiter unten). Beide Compose-Dateien übergeben die Werte an `apiservice`.

## 4. Lokal WeihnachtsfeierBIT starten

Im Repository-Root:

```powershell
cd WeihnachtsfeierBIT
```

Lege hier bei Bedarf eine nicht eingecheckte `.env` mit deinen Spotify-Werten an. Ohne diese Werte startet der Stack, aber die Spotify-Anmeldung der Hitster-Slide ist nicht konfiguriert:

```dotenv
SPOTIFY_CLIENT_ID=<Spotify-Client-ID>
SPOTIFY_REDIRECT_URI=http://127.0.0.1:8080/admin/spotify-callback.html
```

Danach:

```powershell
docker compose up --build -d
docker compose ps
```

- Web-App: <http://127.0.0.1:8080>
- Admin: <http://127.0.0.1:8080/admin>
- API: <http://127.0.0.1:8081>
- OpenAPI in Development: <http://127.0.0.1:8081/openapi/v1.json>

Beim ersten Setup gibt es kein vorkonfiguriertes Admin-Konto. Das erste Konto über `http://127.0.0.1:8080/admin/register` erstellen; danach im Adminbereich anmelden und ein Projekt anlegen.

Logs und Stoppen:

```powershell
docker compose logs -f apiservice
docker compose logs -f webfrontend
docker compose down
```

`docker compose down` behält Datenbank- und Schlüssel-Volumes. `docker compose down --volumes` löscht diese Daten und ist nur für eine ausdrücklich gewünschte vollständige lokale Löschung gedacht.

## 5. NPM, DNS und HTTPS

### Gemeinsames Docker-Netzwerk

Die Produktions-Compose-Datei erwartet ein externes Netzwerk mit dem tatsächlichen Namen `proxy`. Prüfe auf dem Server, ob NPM bereits in diesem Netzwerk hängt:

```bash
docker network ls
docker network inspect proxy
```

Falls das Netzwerk noch nicht existiert, kann es mit `docker network create proxy` angelegt werden. NPM muss ebenfalls an dieses Netzwerk angeschlossen sein. Wenn NPM selbst per Compose verwaltet wird, die Netzwerkzuordnung dauerhaft in dessen Compose-Konfiguration eintragen; ein nur manuell verbundenes Netzwerk kann beim Ersetzen des NPM-Containers verloren gehen. Falls das tatsächlich vorhandene NPM-Netzwerk anders heißt, muss die externe Netzwerkkonfiguration in `docker-compose.production.yml` passend angepasst werden.

### DNS und Firewall

Für jede verwendete Domain einen DNS-A- und/oder AAAA-Eintrag auf die öffentliche Adresse des NPM setzen. Öffentliche eingehende Ports 80 und 443 müssen am NPM erreichbar sein, damit NPM Zertifikate beziehen und HTTPS ausliefern kann. Nicht benötigte App-Ports nicht öffentlich freigeben.

### Proxy Host für WeihnachtsfeierBIT

In NPM **Hosts → Proxy Hosts → Add Proxy Host**:

| NPM-Feld | Wert |
| --- | --- |
| Domain Names | `<APP-DOMAIN>` |
| Scheme | `http` |
| Forward Hostname / IP | `weihnachtsfeierbit` |
| Forward Port | `8080` |
| Websockets Support | Aktivieren |

Der Hostname `weihnachtsfeierbit` ist der in der Produktions-Compose-Datei definierte Netzwerk-Alias von `webfrontend`; NPM muss im selben `proxy`-Netzwerk sein. Unter **SSL** ein Let's-Encrypt-Zertifikat anfordern und **Force SSL** aktivieren. Admin- und Teilnehmeroberfläche nutzen dieselbe Domain, u. a. `/admin` und `/play`; auch der Spotify-Callback liegt unter `/admin/spotify-callback.html`.

## 6. WeihnachtsfeierBIT auf dem Server starten

Im Repository-Root eine zugriffsgeschützte `.env` erstellen. Die Werte in spitzen Klammern sind **Platzhalter**, keine gültigen Zugangsdaten:

```dotenv
POSTGRES_PASSWORD=<starkes-zufaelliges-Datenbankpasswort>
SPOTIFY_CLIENT_ID=<Spotify-Client-ID>
SPOTIFY_REDIRECT_URI=https://<APP-DOMAIN>/admin/spotify-callback.html
```

Das Datenbankpasswort kann zum Beispiel mit `openssl rand -hex 32` erzeugt werden. Die Redirect-URI muss zur NPM-Domain passen und exakt in Spotify eingetragen sein. Die `.env` nicht committen und mit restriktiven Dateirechten schützen, z. B. `chmod 600 .env`.

Vom Repository-Root:

```bash
docker compose --env-file .env -f WeihnachtsfeierBIT/docker-compose.production.yml -p weihnachtsfeierbit up -d --build
docker compose --env-file .env -f WeihnachtsfeierBIT/docker-compose.production.yml -p weihnachtsfeierbit ps
```

Nach dem Start NPM wie oben beschrieben konfigurieren und die Domain per HTTPS öffnen. Die PostgreSQL-Datenbank wird nicht am Host-Port veröffentlicht. Das PostgreSQL-Volume nicht löschen; `down --volumes` vernichtet die Datenbank und die Data-Protection-Schlüssel.

Beim ersten Setup das erste Admin-Konto über `https://<APP-DOMAIN>/admin/register` erstellen. Es gibt keine vorkonfigurierten Zugangsdaten.

## 7. Updates, Validierung und Stoppen

### WeihnachtsfeierBIT

Manuelles Produktionsupdate im Repository-Root:

```bash
git pull
docker compose --env-file .env -f WeihnachtsfeierBIT/docker-compose.production.yml -p weihnachtsfeierbit up -d --build
```

Das aktualisiert die Images und behält die benannten Volumes. Die API wendet neue Migrationen beim Start an. Migrationen vor dem Deployment prüfen und vor dem Update ein Datenbank-Backup anlegen.

Für Entwicklungsrechner stehen im Repository-Root `scripts/validate.ps1` (Windows) und `scripts/validate.sh` (macOS/Linux) für Build und Compose-Prüfung sowie `scripts/update-and-validate.ps1` bzw. `scripts/update-and-validate.sh` für Build, Compose-Neuaufbau und Start des lokalen Stacks bereit. Die Update-Skripte starten Container.

Reguläres Stoppen der Stacks:

```bash
# Im WeihnachtsfeierBIT-Verzeichnis:
docker compose down

```

Das normale `down` erhält die PostgreSQL- und Data-Protection-Volumes. Für Produktionsdeployments niemals `down --volumes` verwenden.

Optional deployt `.github/workflows/deploy-production.yml` bei Pushes auf `main`. Dafür sind ein selbstgehosteter Runner auf dem Server mit Label `weihnachtsfeierbit-deploy`, Zugriff auf den Docker-Daemon und ein Server-Checkout unter `/home/frankyserver/dev/weihnachtsfeier_BIT` samt `.env` erforderlich. Der Runner ist damit privilegierter Servercode; Änderungen an `main` entsprechend behandeln.

## 8. Backups und Wiederherstellung

Es ist kein automatischer Backup-Job in den Compose-Dateien enthalten. Backups regelmäßig außerhalb des Servers und zugriffsgeschützt aufbewahren. Sie enthalten Datenbankinhalte, Schlüssel oder Secrets.

### WeihnachtsfeierBIT-Datenbank

Im Repository-Root, solange der Produktionsstack läuft:

```bash
mkdir -p backups
docker compose --env-file .env -f WeihnachtsfeierBIT/docker-compose.production.yml -p weihnachtsfeierbit exec -T postgres \
  pg_dump -U postgres -d weihnachtsfeierdb > "backups/weihnachtsfeierdb-$(date +%F).sql"
```

Die drei Data-Protection-Volumes zusätzlich sichern, da sie Cookies und Antiforgery-Daten schützen. Bei festem Compose-Projektnamen `weihnachtsfeierbit` lauten die Standardnamen:

```bash
docker run --rm -v weihnachtsfeierbit_admin-dataprotection-keys:/source:ro -v "$PWD/backups:/backup" postgres:17-alpine \
  tar -czf /backup/admin-dataprotection-keys.tgz -C /source .
docker run --rm -v weihnachtsfeierbit_participant-dataprotection-keys:/source:ro -v "$PWD/backups:/backup" postgres:17-alpine \
  tar -czf /backup/participant-dataprotection-keys.tgz -C /source .
docker run --rm -v weihnachtsfeierbit_web-dataprotection-keys:/source:ro -v "$PWD/backups:/backup" postgres:17-alpine \
  tar -czf /backup/web-dataprotection-keys.tgz -C /source .
```

Vor Wiederherstellung den betroffenen Stack stoppen. Einen SQL-Dump nur in eine leere bzw. ausdrücklich vorbereitete Zieldatenbank einspielen, nicht ungeprüft über einen laufenden Datenbestand:

```bash
docker compose --env-file .env -f WeihnachtsfeierBIT/docker-compose.production.yml -p weihnachtsfeierbit up -d postgres
docker compose --env-file .env -f WeihnachtsfeierBIT/docker-compose.production.yml -p weihnachtsfeierbit exec -T postgres \
  psql -U postgres -d weihnachtsfeierdb < backups/weihnachtsfeierdb-<DATUM>.sql
```

Die drei Schlüsselarchive in ihre gleichnamigen Volumes zurückspielen, bevor die Webclients gestartet werden; Beispiel für den Admin-Keyring:

```bash
docker run --rm -v weihnachtsfeierbit_admin-dataprotection-keys:/target -v "$PWD/backups:/backup:ro" postgres:17-alpine \
  tar -xzf /backup/admin-dataprotection-keys.tgz -C /target
```

Für Teilnehmer- und Webfrontend-Keyring entsprechend deren jeweiliges Volume und Archiv verwenden. Bei abweichendem Compose-Projektnamen zuerst mit `docker volume ls` die tatsächlich verwendeten Volumenamen prüfen. `.env` sicher separat sichern und nicht in ein öffentliches Backup-Archiv legen.

## 9. Fehlersuche

### Container oder Start

```bash
docker compose --env-file .env -f WeihnachtsfeierBIT/docker-compose.production.yml -p weihnachtsfeierbit ps
docker compose --env-file .env -f WeihnachtsfeierBIT/docker-compose.production.yml -p weihnachtsfeierbit logs --tail 100 apiservice
docker compose --env-file .env -f WeihnachtsfeierBIT/docker-compose.production.yml -p weihnachtsfeierbit logs --tail 100 webfrontend
```

### NPM, DNS und Verbindung

- Prüfen, dass DNS auf die NPM-IP zeigt, NPM ein gültiges Zertifikat hat und die korrekte Domain im Proxy Host eingetragen ist.
- **Websockets Support** aktivieren, damit die Blazor-/SignalR-Verbindung des Webfrontends über NPM funktioniert.
- Beim WeihnachtsfeierBIT-Upstream `weihnachtsfeierbit:8080` muss NPM im Docker-Netzwerk `proxy` sein.

### Hitster und Spotify

- Ist die Hitster-Slide nicht mit Spotify verbindbar, zuerst `SPOTIFY_CLIENT_ID` und `SPOTIFY_REDIRECT_URI` in der `.env` des WeihnachtsfeierBIT-Compose-Aufrufs prüfen. Die Redirect-URI muss exakt `/admin/spotify-callback.html` verwenden.
- Die Redirect-URI in Serverkonfiguration, Browseradresse und Spotify Developer Dashboard müssen identisch sein. Nach einer Änderung den Stack mit `up -d --build` aktualisieren.
- Prüfen, dass der Spotify-Account für die App freigeschaltet ist und Spotify Premium für Web Playback vorhanden ist.
- Hitster lässt sich nur starten und steuern, wenn sein Event den Status „aktiv“ hat und im Vordergrund läuft. Bei geplanten Events zuerst „Jetzt starten“ wählen; passive Events müssen zuvor in den Vordergrund geholt werden. Die Steuerung wird für diese Zustände ausgeblendet.
- Für die integrierte Hitster-Slide werden laufende Spielsteuerungsberechtigungen (`CanControlSession`) benötigt. Teilnehmer stimmen über ihre Teilnehmer-Sitzung ab; sie benötigen keine Admin-Steuerungsberechtigung.

### Datenbank, Migrationen und Schlüssel

- Bei Datenbankproblemen zuerst `apiservice`-Logs lesen; sie enthalten Verbindungs- und Migrationsfehler.
- Das PostgreSQL-Volume bei normalen Updates erhalten. `docker compose down --volumes` löscht die Datenbank.
- Die Data-Protection-Volumes nicht entfernen. Bei ungültigen Cookies nach erster Aktivierung der Volumes Browserseite neu laden und gegebenenfalls erneut anmelden.
