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

### Teilnehmer-Berechtigungen & 3-State-Overrides

Projektweite Standards und individuelle Teilnehmer-Berechtigungen (z. B. *Chat-Nachrichten schreiben*, *Chat lesen*, *Neue Chats erstellen*, *Teamnamen vorschlagen und abstimmen*, *Musikwünsche einreichen und abstimmen*, *Sprechrecht & Interaktion erlaubt*, *Eigenen Namen ändern*) werden über folgende Endpunkte verwaltet:

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `GET /api/projects/{projectId}/permissions/global` | Projektadmin | Globale Projekt-Standardberechtigungen abrufen. |
| `PUT /api/projects/{projectId}/permissions/global` | Projektadmin | Globale Projekt-Standardberechtigungen aktualisieren. |
| `PUT /api/projects/{projectId}/participants/{participantId}/permissions` | Projektadmin | Individuelle 3-State-Berechtigungs-Overrides für einen Teilnehmer setzen (*Global übernehmen*, *An (Erzwungen)*, *Aus (Sperre)*). |

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
| `GET /api/game-instances/by-code/{code}` | öffentlich, rate-limited | Löst einen aktiven 6-stelligen Code auf und liefert zusätzlich die minimale und maximale Ziffernzahl der Teilnehmernummern des Projekts. |
| `GET /api/game-instances/by-code/{code}/canvas` | öffentlich, rate-limited | Liefert den ausgewählten Beamerinhalt und Team-Punktestand. |
| `POST /api/projects/{projectId}/participant-sessions` | öffentlich, rate-limited | Login mit `{ "number": 12 }`. |
| `DELETE /api/projects/{projectId}/participant-sessions/current` | Teilnehmer | Aktuelle Sitzung widerrufen. |

Der Teilnehmerclient führt nach erfolgreicher Anmeldung die Route `/play/{code}/{number}`. Bei einem Neuladen wird über den Session-Code und die Teilnehmernummer erneut angemeldet; das API-Session-Token bleibt dabei außerhalb von URL und Browser-Speicher. Ein freiwilliges Überspringen der erstmaligen Namenseingabe wird nur für die aktuelle Browser-Session gespeichert.

## Projekt-Info-Seiten

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `GET /api/projects/{projectId}/info-pages` | Projektadmin / Teilnehmer | Info-Seiten eines Projekts abrufen; Teilnehmer erhalten nur sichtbare Seiten. |
| `GET /api/projects/{projectId}/info-pages/{id}` | Projektadmin / Teilnehmer | Eine projektgebundene Info-Seite abrufen. |
| `POST /api/projects/{projectId}/info-pages` | Projektadmin | Info-Seite anlegen; optionales `id` ist eine Wiederholungs-ID. Eine identische Wiederholung liefert dieselbe Seite statt ein Duplikat anzulegen. |
| `PUT /api/projects/{projectId}/info-pages/{id}` | Projektadmin | Titel, Icon, Position, Sichtbarkeit und Markdown-Inhalt aktualisieren. |
| `DELETE /api/projects/{projectId}/info-pages/{id}` | Projektadmin | Info-Seite löschen. |

Info-Seiten-Endpunkte liefern ausschließlich Response-DTOs, keine EF-Entitäten mit Navigationen. So kann die JSON-Antwort nach dem Speichern keine zyklischen Projektbeziehungen serialisieren.

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
| `PUT /api/projects/{projectId}/session/canvas-settings` | Projektadmin (`CanControlSession`) | Anzeigeeinstellungen der aktiven Session setzen. |

Pantomime-Aktionen verwenden separate, serverseitig geschützte Endpunkte:

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `GET /api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/pantomime` | Projektadmin (`CanControlSession`) | Teamfortschritt und aktuelle Begriffe für die Regie abrufen. |
| `POST /api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/pantomime/admin/{action}` | Projektadmin (`CanControlSession`) | Runde starten, pausieren, fortsetzen, beenden, einen Team-Begriff als erraten bestätigen (`action=correct`) oder überspringen (`action=skip` bzw. `action=free-skip`). Teamaktionen enthalten `teamId` und `expectedTermIndex`; `free-skip` verbraucht kein Skip-Limit. |
| `GET /api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/pantomime/current-term` | Zugewiesene darstellende Person | Eigenen aktuellen Begriff und verbleibende Skips abrufen. |
| `POST /api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/pantomime/skip` | Zugewiesene darstellende Person mit `IsMuted` (Interaktion erlaubt) | Aktuellen Begriff überspringen, sofern die Runde läuft und das konfigurierte Skip-Limit noch nicht erreicht ist. |

Die Beamer-Einstellungen werden mit der aktiven `GameInstance` gespeichert und bei einer neuen Session auf die Standardwerte zurückgesetzt. `scoreboardLayout` akzeptiert `right` (Punktestand und Diagramm rechts), `bottom` (schmale Punkte-Leiste) oder `hidden`; `qrCodeMode` akzeptiert `card`, `popup` (große Teilnahme-Überlagerung mit QR-Code und Session-Code) oder `hidden`. `fontScalePercent`, `eventScalePercent`, `scoreboardScalePercent` und `qrCodeScalePercent` steuern die jeweilige Größe und sind jeweils auf 60 bis 160 Prozent begrenzt; Event- und Punktestandskalierung beeinflussen auch die relative Kachelgröße. `showProjectTitle` und `showEventTypeLabel` steuern die entsprechenden Beschriftungen. Fehlende Werte in älteren gespeicherten Einstellungen entsprechen den Standardwerten. Änderungen werden live an die Beameransicht übertragen, die ihre Inhalte ohne Scrollen an den verfügbaren Bildschirmplatz anpasst.

## Chat-Moderation & Endpunkte

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `GET /api/projects/{projectId}/chat/groups` | Admin / Teilnehmer mit `CanReadChat` | Chat-Gruppen und Supportkanäle auflisten (Admins sehen spezifische Teilnehmernamen in Support-Chats wie `Support: Max (#12)`). |
| `GET /api/projects/{projectId}/chat/participants` | Admin / Teilnehmer mit `CanCreateChats` | Verfügbare Chat-Kontakte auflisten. |
| `GET /api/projects/{projectId}/chat/groups/{groupId}/messages` | Admin / Teilnehmer mit `CanReadChat` | Nachrichten eines Kanals abrufen; Teilnehmer ohne Leserecht erhalten `403`. |
| `POST /api/projects/{projectId}/chat/groups/{groupId}/messages` | Admin / Teilnehmer mit `CanSendChatMessages` und `IsMuted` | Nachricht senden (mit Enter-Taste oder Senden-Button). |
| `POST /api/projects/{projectId}/chat/direct/{targetParticipantId}` | Admin / Teilnehmer mit `CanCreateChats` und `IsMuted` (für neue Direktchats) | Support- bzw. Direktchat öffnen oder erstellen. |
| `POST /api/projects/{projectId}/chat/groups` | Admin / Teilnehmer mit `CanCreateChats` und `IsMuted` | Benutzerdefinierte Chat-Gruppe erstellen. |
| `DELETE /api/projects/{projectId}/chat/messages/{messageId}` | Projektadmin | Einzelne Chat-Nachricht löschen (auch per Rechtsklick in der UI). |
| `DELETE /api/projects/{projectId}/chat/groups/{groupId}` | Projektadmin | Chat-Gruppe löschen (außer globale Hauptgruppe). |
| `PUT /api/projects/{projectId}/chat/groups/{groupId}` | Projektadmin | Team- oder benutzerdefinierte Chat-Gruppe umbenennen. |

SignalR WebSockets unter `/hubs/gamestate` übertragen den `ProjectGameState`. Chat-Nachrichten, Löschungen und Umbenennungen werden nur an Admins und Teilnehmer mit effektivem `CanReadChat`-Recht ausgeliefert; Änderungen des Rechts aktualisieren offene Verbindungen sofort.

## Teilnehmeraktionen

| Methode/Route | Teilnehmerrecht |
| --- | --- |
| `PUT /api/projects/{projectId}/participants/me` | `CanChangeName` und `IsMuted` (Interaktion erlaubt). |
| `POST /api/projects/{projectId}/teams/my/suggestions` | `CanSuggestTeamNames` und `IsMuted`. |
| `POST /api/projects/{projectId}/teams/my/suggestions/{suggestionId}/vote` | `CanVoteTeamNames` und `IsMuted`. |
| `POST /api/projects/{projectId}/song-wishes` | `CanSuggestSongs` und `IsMuted`. |
| `POST /api/projects/{projectId}/song-wishes/{wishId}/vote` | `CanVoteSongs` und `IsMuted`. |
| `POST /api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/responses` | `IsMuted` (Interaktion erlaubt). |
| `POST /api/projects/{projectId}/session/events/{eventId}/hitster/{slideId}/vote` | `IsMuted` (Interaktion erlaubt). |

Die Berechtigungen werden zusätzlich zu den UI-Steuerelementen in der API geprüft. Bei unabhängigen Vorschlags- und Abstimmungsrechten wird ein neu eingereichter Vorschlag nur dann automatisch als eigene Stimme gezählt, wenn das jeweilige Abstimmungsrecht ebenfalls aktiv ist.
