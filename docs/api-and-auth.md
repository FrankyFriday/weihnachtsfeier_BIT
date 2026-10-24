# API und Berechtigungen

Routen liegen in `WeihnachtsfeierBit.ApiService/Endpoints/`, Authentifizierung und Filter in `Auth/`. OpenAPI ist in Development verfügbar: <http://localhost:8081/openapi/v1.json>.

## Globale Admin-Konten

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `POST /api/admin-accounts` | öffentlich, rate-limited | Globales Konto registrieren; Body `{ "username", "password" }`; erstellt eine Adminsession. |
| `POST /api/admin-sessions` | öffentlich, rate-limited | Global anmelden; Body `{ "username", "password" }`; keine Projekt-ID. |
| `DELETE /api/admin-sessions/current` | Bearer-Token | Globale Adminsession widerrufen. |

Globale Shows werden mit einer aktiven Adminsession, unabhängig von Projektmitgliedschaften, verwaltet:

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `GET /api/admin/shows` | Admin-Bearer-Token | Globale Shows samt Anzahl verknüpfter Projekte auflisten. |
| `POST /api/admin/shows` | Admin-Bearer-Token | Show global anlegen; Body `{ "name": "..." }`. |
| `GET /api/admin/shows/{showId}` | Admin-Bearer-Token | Eine globale Show laden. |
| `PUT /api/admin/shows/{showId}` | Admin-Bearer-Token | Shownamen aktualisieren; Body `{ "name": "..." }`. |
| `DELETE /api/admin/shows/{showId}` | Admin-Bearer-Token | Show und alle zugehörigen Slides löschen. |
| `POST /api/admin/shows/{showId}/duplicate` | Admin-Bearer-Token | Show mit allen Slides als Kopie duplizieren. |
| `GET /api/admin/shows/{showId}/slides` | Admin-Bearer-Token | Geordnete Slides einer globalen Show laden. |
| `POST /api/admin/shows/{showId}/slides` | Admin-Bearer-Token | Slide mit Typ, Position und JSON-Parametern anlegen. |
| `PUT /api/admin/shows/{showId}/slide-positions` | Admin-Bearer-Token | Slide-Reihenfolge atomar per ID-Array aktualisieren (`{ "slideIds": [...] }`). |
| `PUT /api/admin/shows/{showId}/slides/{slideId}` | Admin-Bearer-Token | Position und typspezifische JSON-Parameter aktualisieren. |
| `DELETE /api/admin/shows/{showId}/slides/{slideId}` | Admin-Bearer-Token | Slide löschen. |

Registrierung, Login und Teilnehmernummern-Login sind auf zehn Requests pro IP und Minute begrenzt.

## Projektzugriff

Geschützte Projektendpunkte verwenden `/api/projects/{projectId}` und `Authorization: Bearer <AccessToken>`.

| Rolle | Rechte |
| --- | --- |
| Teilnehmer mit aktiver Sitzung dieses Projekts | Projekt, eigenes Profil, Teams, Shows und Slides lesen; eigene Sitzungen ansehen/widerrufen. |
| Admin mit aktiver globaler Sitzung und Membership im Projekt | Projekt-/Teilnehmer-/Team-CRUD, Show-Projekt-Verknüpfungen und Slide-CRUD sowie Admin- und Sitzungspflege. Globale Show-Metadaten verwaltet ein Admin mit aktiver Sitzung unabhängig vom Projekt. |

Bei jedem Request prüft der Filter Token-Hash, Ablauf, Widerruf und Projektmitgliedschaft. Ein Admin-Token kann für mehrere Memberships verwendet werden; ohne Membership gibt es für das Projekt `401`. Ungültige/fehlende Sessions liefern `401`; gültige Teilnehmer ohne Adminrecht erhalten bei Schreibzugriff `403`.

## Teilnehmer-Sessions

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `POST /api/participant-sessions/by-code` | öffentlich, rate-limited | Login mit 6-stelligem Session-Code `{ "code": "482910", "number": 12 }`; verknüpft die Sitzung mit der aktiven `GameInstance`. |
| `GET /api/game-instances/by-code/{code}` | öffentlich, rate-limited | Löst einen aktiven 6-stelligen Code auf. |
| `POST /api/projects/{projectId}/participant-sessions` | öffentlich, rate-limited | Login mit `{ "number": 12 }`; Teilnehmernummer ist alleiniger Kennzeichner. |
| `DELETE /api/projects/{projectId}/participant-sessions/current` | Teilnehmer | Aktuelle Sitzung widerrufen. |
| `GET /api/projects/{projectId}/me/sessions` | Teilnehmer | Eigene Sessions anzeigen. |
| `DELETE /api/projects/{projectId}/me/sessions/{sessionId}` | Teilnehmer | Eigene Session widerrufen. |

## Projektadmins verwalten

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `GET /api/projects/{projectId}/admins` | Projektadmin | Memberships anzeigen. |
| `POST /api/projects/{projectId}/admins` | Projektadmin | Bereits registriertes Konto mit `{ "username": "..." }` zuordnen. Erstellt kein Konto. |
| `DELETE /api/projects/{projectId}/admins/{adminId}` | Projektadmin | Membership entfernen; Selbstentfernung und Entfernen des letzten Admins sind gesperrt. |
| `GET /api/projects/{projectId}/admin-sessions` | Projektadmin | Aktive Sitzungen der Projektadmins ansehen. |
| `DELETE /api/projects/{projectId}/admin-sessions/{sessionId}` | Projektadmin | Adminsession widerrufen. |
| `GET /api/projects/{projectId}/participants/{participantId}/sessions` | Projektadmin | Aktive Teilnehmersitzungen ansehen. |
| `DELETE /api/projects/{projectId}/participants/{participantId}/sessions/{sessionId}` | Projektadmin | Teilnehmersitzung widerrufen. |

## Live Session & Event Steuerung

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `GET /api/projects/{projectId}/session/state` | Admin / Teilnehmer | Aktuellen vollen Session-Livezustand (`ProjectGameState`) abrufen. |
| `POST /api/projects/{projectId}/session/shows` | Projektadmin | Show in Session starten (Modus `"active"` oder `"passive"`). |
| `PUT /api/projects/{projectId}/session/shows/{instanceShowId}` | Projektadmin | Show-Slide wechseln oder Modus ändern. |
| `DELETE /api/projects/{projectId}/session/shows/{instanceShowId}` | Projektadmin | Laufende Show aus Session stoppen. |
| `POST /api/projects/{projectId}/session/events` | Projektadmin | Spontanes Event erstellen (Aktiv oder Passiv). |
| `PUT /api/projects/{projectId}/session/events/{eventId}/mode` | Projektadmin | Event-Modus ändern (`"active"` oder `"passive"`). |
| `PUT /api/projects/{projectId}/session/events/{eventId}/close` | Projektadmin | Event beenden. |
| `POST /api/projects/{projectId}/session/events/{eventId}/responses` | Teilnehmer / Admin | Antwort oder Aktion (Vote, Quiz-Auswahl, Schätzwert, Buzzer) für ein Event einreichen. |

## Fachliche Endpunkte

Mit aktiver Teilnehmer- oder Adminsession lesbar: `GET /api/projects/{projectId}`, `/overview`, `/participants/me`, `PUT /participants/me` (einen eigenen Namen eintragen), `/teams`, `/shows`, `/shows/{showId}/slides`, `/session/state`. Die Projekt-Showliste enthält nur Shows mit einer `ShowProjektZuo`-Verknüpfung.

SignalR WebSockets: `/hubs/gamestate` erlaubt Clients, per `JoinProject(projectId)` Live-Updates (`GameStateUpdated`) mit dem vollständigen `ProjectGameState` (Teams, Punktestände, Ränge, aktives Vordergrund-Event, passive Hintergrund-Events, laufende Shows) zu empfangen.

Admin-only: `PUT`/`DELETE /api/projects/{projectId}`; Teilnehmer `GET`/`POST /participants`, `POST /participants/generate` (automatische 2-, 3- oder 4-stellige Nummern-Generierung), `POST /participants/randomize-teams` (gleichmäßige Zufallsverteilung auf Teams), `PUT`/`DELETE /participants/{participantId}`; Teams `POST /teams`, `PUT`/`DELETE /teams/{teamId}`; Show-Links `POST /shows` mit `{ "showId": "..." }` und `DELETE /shows/{showId}`; Slides `POST /shows/{showId}/slides`, `PUT`/`DELETE /shows/{showId}/slides/{slideId}`. Show-Unlink entfernt nur die Projektverknüpfung; Show und Slides bleiben bestehen.

Verschachtelte IDs werden gegen Projekt und Show geprüft. `GET /api/projects/{projectId}/overview` liefert Projektname sowie Show-, Teilnehmer- und Teamanzahl aggregiert in einer Antwort.

## Noch nicht umgesetzt

Das Leinwand-Dashboard (Projektor-Anzeige für Großbildschirme) ist als Nächstes geplant. Vor öffentlichem Einsatz zweiten Faktor/Teilnahmecode und robusteren Abuse-Schutz ergänzen.