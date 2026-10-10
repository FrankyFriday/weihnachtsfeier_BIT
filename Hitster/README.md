# Hitster digital (localhost)

Ein Host-Rechner spielt per Spotify (Premium) die Songs ab, Mitspieler legen ihre Karten im Browser.

## Einrichtung

1. https://developer.spotify.com/dashboard → App anlegen
   - Redirect URI: `http://127.0.0.1:3000/host.html`
   - Für einen Server zusätzlich `${PUBLIC_URL}/host.html` eintragen (z. B. `https://hitster.example.com/host.html`)
   - API: "Web Playback SDK" und "Web API" ankreuzen
   - Unter "User Management" den eigenen Spotify-Account eintragen (Dev-Modus)
2. Im Spotify Developer Dashboard zusätzlich die Redirect-URI `http://127.0.0.1:8888/callback` eintragen.
3. `.env.example` nach `.env` kopieren und `SPOTIFY_CLIENT_ID` eintragen. Ein Client-Secret wird für den Playlist-Import nicht benötigt.
4. Host-Passwort-Hash erzeugen: `npm run hash-password` (Passwort mindestens 12 Zeichen) und die ausgegebene `HOST_PASSWORD_HASH=...`-Zeile in `.env` eintragen. Der Server speichert kein Klartextpasswort; beim Login wird es mit scrypt geprüft.
5. Songliste erzeugen:
   `npm run import -- <Playlist-URL oder -ID>`
   Jahre kommen aus MusicBrainz (Originaljahr), Fallback Spotify-Albumjahr. Ergebnis in `data/songs.json`, dort lassen sich falsche Jahre korrigieren.
   Bei jedem Import öffnet sich der Browser zur Spotify-Anmeldung; beim ersten Mal muss der Zugriff bestätigt werden. Die Playlist muss im eigenen Account liegen oder dort kollaborativ sein. Falls nötig, die Titel in eine neue eigene Playlist kopieren.
6. `npm start`

Für einen Docker-/Serverbetrieb `HOST_PASSWORD_HASH` als Secret setzen und den Dienst hinter HTTPS betreiben. Bei einem Reverse Proxy muss `X-Forwarded-Proto: https` gesetzt werden. Wenn genau ein vertrauenswürdiger Reverse Proxy vorgeschaltet ist, zusätzlich `TRUST_PROXY=1` setzen, damit die Login-Sperre die Client-IP statt der Proxy-IP verwendet. `PUBLIC_URL` kann auf die externe Basisadresse gesetzt werden (z. B. `https://hitster.example.com`); diese Adresse muss auch als Spotify-Redirect-URI mit `/host.html` im Developer Dashboard eingetragen sein. Ohne konfigurierten Passwort-Hash bleiben Host-Anmeldung und Spielsteuerung gesperrt.

## Server mit Docker Compose und Nginx Proxy Manager

Voraussetzungen: Docker mit Compose v2, Nginx Proxy Manager (NPM) und eine Domain, deren DNS-A/AAAA-Eintrag auf NPM zeigt. NPM übernimmt HTTPS-Zertifikat und TLS. Die App veröffentlicht nur den konfigurierten HTTP-Port; diesen Port in der Firewall möglichst nur vom NPM-Host erreichbar machen.

1. Auf dem App-Server `.env.docker.example` nach `.env` kopieren und dort `DOMAIN` (ohne Schema) sowie optional `APP_PORT` setzen. `APP_PORT` ist der Host-Port, den NPM erreichen muss; Standard ist `3000`.
2. `npm run hash-password` lokal oder auf einem vertrauenswürdigen Rechner ausführen. In `.env.server` aus der Vorlage `.env.server.example` die Spotify Client-ID und den erzeugten Passwort-Hash eintragen. Den Hash als einfach quotierten Wert speichern: `HOST_PASSWORD_HASH='scrypt$...'`. `.env.server` enthält Geheimnisse und darf nicht eingecheckt werden.
3. Vor dem Deployment die Songliste erzeugen und `data/songs.json` auf den Server kopieren. Sie wird im Container schreibgeschützt eingebunden.
4. In NPM einen Proxy Host erstellen:
   - Domain Names: die konfigurierte `DOMAIN`
   - Scheme: `http`
   - Forward Hostname / IP: IP-Adresse des App-Docker-Hosts
   - Forward Port: der konfigurierte `APP_PORT` (standardmäßig `3000`)
   - „Websockets Support“ aktivieren
   - Unter „SSL“ ein Let's Encrypt-Zertifikat anfordern und „Force SSL“ aktivieren
5. Im Spotify Developer Dashboard als Redirect-URI `https://<DOMAIN>/host.html` eintragen, zusätzlich zu `http://127.0.0.1:3000/host.html` für lokale Nutzung. Web Playback SDK und Web API aktivieren sowie den Spotify-Account unter „User Management“ freigeben.
6. Im Projektverzeichnis starten: `docker compose up -d --build`. Status und Logs: `docker compose ps` und `docker compose logs -f app`. Aktualisieren: `git pull` und erneut `docker compose up -d --build`.
7. Host öffnen unter `https://<DOMAIN>/host.html`; Mitspieler unter `https://<DOMAIN>/play.html`. Beim ersten Host-Aufruf das Host-Passwort eingeben, danach bei Spotify anmelden.

`PUBLIC_URL` wird automatisch aus `DOMAIN` gebildet und sorgt dafür, dass der Host in der Lobby den HTTPS-Link der Domain anzeigt. `TRUST_PROXY=1` ist für genau einen vorgeschalteten Proxy konfiguriert, damit HTTPS-Cookies und die Login-Fehlversuchsbegrenzung korrekt funktionieren. NPM muss den `X-Forwarded-Proto`-Header setzen (Standard). WebSockets werden über „Websockets Support“ weitergeleitet. Den App-Stack regulär mit `docker compose down` stoppen.

## Spielen

- Host: http://127.0.0.1:3000/host.html (Spotify anmelden, Raum erstellen)
- Spieler: http://127.0.0.1:3000/play.html bzw. `http://<LAN-IP>:3000/play.html` am Handy, Raumcode eingeben
- Wer zuerst 10 Karten in der Zeitleiste hat, gewinnt.
- Teams: In der Lobby stellt der Host mit ＋/− die Teamanzahl ein (0 = Einzelspiel, bis 20 Teams). Spieler werden automatisch gleichmäßig verteilt und können am Handy das Team wechseln. Jedes Team hat eine gemeinsame Zeitleiste; pro Zug legt ein wechselnder Spielführer die Karte (ist er offline, darf jedes Teammitglied).
