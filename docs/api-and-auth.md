# API und Berechtigungen

Routen liegen in `WeihnachtsfeierBit.ApiService/Endpoints/`, Authentifizierung und Filter in `Auth/`. OpenAPI ist in Development verfügbar: <http://localhost:8081/openapi/v1.json>.

## Globale Admin-Konten

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `POST /api/admin-accounts` | öffentlich, rate-limited | Globales Konto registrieren; Body `{ "Username", "Password" }`; erstellt eine Adminsession. |
| `POST /api/admin-sessions` | öffentlich, rate-limited | Global anmelden; Body `{ "Username", "Password" }`; keine Projekt-ID. |
| `DELETE /api/admin-sessions/current` | Bearer-Token | Globale Adminsession widerrufen. |
| `GET /api/admin/admins/search?q={query}` | Admin-Bearer-Token | Autocomplete-Suche nach registrierten Admin-Konten anhand des Benutzernamens. |

## Globale Shows & Sharing

Globale Shows werden mit einer aktiven Adminsession verwaltet:

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `GET /api/admin/shows` | Admin-Bearer-Token | Shows auflisten, die der Admin erstellt hat oder zu denen er eingeladen wurde. |
| `POST /api/admin/shows` | Admin-Bearer-Token | Show global anlegen; Body `{ "name": "..." }`; setzt den Ersteller (`ErstellerAdminId`). |
| `GET /api/admin/shows/{showId}` | Admin-Bearer-Token | Eine globale Show laden. |
| `PUT /api/admin/shows/{showId}` | Admin-Bearer-Token | Shownamen aktualisieren; Body `{ "name": "..." }`. |
| `DELETE /api/admin/shows/{showId}` | Show-Ersteller | Show und alle zugehörigen Slides löschen. |
| `POST /api/admin/shows/{showId}/duplicate` | Admin-Bearer-Token | Show mit allen Slides als Kopie duplizieren. |
| `GET /api/admin/shows/{showId}/admins` | Show-Admin | Liste der freigegebenen Admins und Ersteller-Status abrufen. |
| `POST /api/admin/shows/{showId}/admins` | Show-Ersteller | Weiteren Admin zur Show einladen; Body `{ "username": "..." }`. |
| `DELETE /api/admin/shows/{showId}/admins/{adminId}` | Show-Ersteller | Einladung eines Admins für diese Show widerrufen. |
| `GET /api/admin/shows/{showId}/slides` | Admin-Bearer-Token | Geordnete Slides einer globalen Show laden. |
| `POST /api/admin/shows/{showId}/slides` | Admin-Bearer-Token | Slide mit Typ, Position und JSON-Parametern anlegen. |
| `PUT /api/admin/shows/{showId}/slide-positions` | Admin-Bearer-Token | Slide-Reihenfolge atomar per ID-Array aktualisieren (`{ "slideIds": [...] }`). |
| `PUT /api/admin/shows/{showId}/slides/{slideId}` | Admin-Bearer-Token | Position und typspezifische JSON-Parameter aktualisieren. |
| `DELETE /api/admin/shows/{showId}/slides/{slideId}` | Admin-Bearer-Token | Slide löschen. |

Registrierung, Login und Teilnehmernummern-Login sind auf zehn Requests pro IP und Minute begrenzt.

## Projektzugriff & Granulare Berechtigungen

Neue Projekte speichern den erstellenden Admin als `ErstellerAdminId`. Bei älteren Projekten ohne gespeicherten Ersteller gilt kein impliziter Erstellerzugriff: Admins benötigen eine explizite Projektmitgliedschaft. Projektmitglieder können solche Projekte nicht übernehmen oder Admins einladen bzw. entfernen; eine Wiederherstellung der Erstellerzuordnung muss über einen separaten, geprüften Prozess erfolgen.

Geschützte Projektendpunkte verwenden `/api/projects/{projectId}` und `Authorization: Bearer <AccessToken>`.

| Rolle | Rechte |
| --- | --- |
| **Projekt-Ersteller (`IsCreator`)** | Volle Kontrolle: Projekt bearbeiten/löschen, Co-Admins einladen, Berechtigungen anpassen. |
| **Eingeladener Co-Admin** | Berechtigungen hängen von den zugewiesenen Flags ab:<br>- `CanControlSession`: Live-Session starten, pausieren, Folien wechseln, Punkte vergeben.<br>- `CanManageParticipants`: Teilnehmer & Teams anlegen, bearbeiten, zufällig verteilen.<br>- `CanEditProjectSettings`: Projektdetails & Name bearbeiten.<br>- `CanManageShows`: Shows im Projekt verknüpfen oder entfernen. |
| **Teilnehmer** | Projekt, eigenes Profil, Teams, Shows und Slides lesen; eigene Sitzungen ansehen/widerrufen; Antworten abgeben. |

Die Hitster-Steuerungsendpunkte (Start, aktuellen Song abrufen, aufdecken und zum nächsten Song wechseln) erfordern `CanControlSession`, wie die übrigen Live-Session-Steuerungsendpunkte.

## Projektadmins & Sharing verwalten

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `GET /api/admin/projects/{projectId}/admins` | Projektadmin | Liste aller Projektadmins samt Erstellerstatus und Berechtigungs-Flags abrufen. |
| `POST /api/admin/projects/{projectId}/admins` | Projekt-Ersteller | Admin per Benutzernamen `{ "username": "..." }` zum Projekt einladen. |
| `PUT /api/admin/projects/{projectId}/admins/{adminId}/permissions` | Projekt-Ersteller | Granulare Berechtigungs-Flags eines Co-Admins anpassen (`CanControlSession`, `CanManageParticipants`, `CanEditProjectSettings`, `CanManageShows`). |
| `DELETE /api/admin/projects/{projectId}/admins/{adminId}` | Projekt-Ersteller | Co-Admin aus dem Projekt entfernen. |
| `GET /api/projects/{projectId}/admin-sessions` | Projektadmin | Aktive Sitzungen der Projektadmins ansehen. |
| `DELETE /api/projects/{projectId}/admin-sessions/{sessionId}` | Projektadmin | Adminsession widerrufen. |
| `GET /api/projects/{projectId}/participants/{participantId}/sessions` | Projektadmin | Aktive Teilnehmersitzungen ansehen. |
| `DELETE /api/projects/{projectId}/participants/{participantId}/sessions/{sessionId}` | Projektadmin | Teilnehmersitzung widerrufen. |

## Teilnehmer-Sessions

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `POST /api/participant-sessions/by-code` | öffentlich, rate-limited | Login mit 6-stelligem Session-Code `{ "code": "482910", "number": 12 }`. |
| `GET /api/game-instances/by-code/{code}` | öffentlich, rate-limited | Löst einen aktiven 6-stelligen Code auf. |
| `GET /api/game-instances/by-code/{code}/canvas` | öffentlich, rate-limited | Liefert den ausgewählten Beamerinhalt und Team-Punktestand. |
| `POST /api/projects/{projectId}/participant-sessions` | öffentlich, rate-limited | Login mit `{ "number": 12 }`. |
| `DELETE /api/projects/{projectId}/participant-sessions/current` | Teilnehmer | Aktuelle Sitzung widerrufen. |

## Live Session & Event Steuerung

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `GET /api/projects/{projectId}/session/state` | Admin / Teilnehmer | Aktuellen vollen Session-Livezustand (`ProjectGameState` inkl. `IsPaused` und `ScheduledEvents`) abrufen. |
| `POST /api/projects/{projectId}/session/shows` | Projektadmin (`CanControlSession`) | Show als Event starten/planen. |
| `POST /api/projects/{projectId}/session/events` | Projektadmin (`CanControlSession`) | Spontanes Event erstellen/planen. |
| `POST /api/projects/{projectId}/session/toggle-pause` | Projektadmin (`CanControlSession`) | Session pausieren / fortsetzen. |
| `PUT /api/projects/{projectId}/session/events/{eventId}` | Projektadmin (`CanControlSession`) | CurrentSlideIndex, Modus, Pausierung oder Status ändern. |
| `PUT /api/projects/{projectId}/session/events/{eventId}/close` | Projektadmin (`CanControlSession`) | Event beenden. |
| `POST /api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/responses` | Teilnehmer / Admin | Antwort/Aktion einreichen. |
| `PUT /api/projects/{projectId}/session/canvas` | Projektadmin (`CanControlSession`) | Welches Event auf der Beamer-Ansicht angezeigt werden soll. |
| `GET /api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/pantomime` | Projektadmin (`CanControlSession`) | Pantomime-Begriffe und Laufzeitstatus für die Spielleitung abrufen. |
| `POST /api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/pantomime/{action}` | Projektadmin (`CanControlSession`) | Pantomime-Runde starten, pausieren, fortsetzen, beenden, Begriff als erraten markieren oder überspringen. |

SignalR WebSockets unter `/hubs/gamestate` übertragen den `ProjectGameState`. Bei Unterbrechungen, Pausierung oder Session-Ende empfangen alle verbundenen Clients in Echtzeit entsprechende Nachrichten und schalten Vollbild-Overlays frei.

Pantomime-Begriffe werden aus Teilnehmer- und Beamerzuständen entfernt; nur der geschützte Admin-Endpunkt liefert die aktuelle Begriffsliste. Der QR-Code auf der Beamerseite führt zur Teilnehmeranmeldung mit vorausgefülltem Session-Code.
