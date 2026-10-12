# API und Berechtigungen

Routen liegen in `WeihnachtsfeierBit.ApiService/Endpoints/`, Authentifizierung und Filter in `Auth/`. OpenAPI ist in Development verfügbar: <http://localhost:8081/openapi/v1.json>.

## Globale Admin-Konten

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
| `POST /api/admin-accounts` | öffentlich, rate-limited | Globales Konto registrieren; Body `{ "username", "password" }`; erstellt eine Adminsession. |
| `POST /api/admin-sessions` | öffentlich, rate-limited | Global anmelden; Body `{ "username", "password" }`; keine Projekt-ID. |
| `DELETE /api/admin-sessions/current` | Bearer-Token | Globale Adminsession widerrufen. |

Registrierung, Login und Teilnehmernummern-Login sind auf zehn Requests pro IP und Minute begrenzt.

## Projektzugriff

Geschützte Projektendpunkte verwenden `/api/projects/{projectId}` und `Authorization: Bearer <AccessToken>`.

| Rolle | Rechte |
| --- | --- |
| Teilnehmer mit aktiver Sitzung dieses Projekts | Projekt, eigenes Profil, Teams, Shows und Slides lesen; eigene Sitzungen ansehen/widerrufen. |
| Admin mit aktiver globaler Sitzung und Membership im Projekt | Projekt-/Teilnehmer-/Team-/Show-/Slide-CRUD sowie Admin- und Sitzungspflege. |

Bei jedem Request prüft der Filter Token-Hash, Ablauf, Widerruf und Projektmitgliedschaft. Ein Admin-Token kann für mehrere Memberships verwendet werden; ohne Membership gibt es für das Projekt `401`. Ungültige/fehlende Sessions liefern `401`; gültige Teilnehmer ohne Adminrecht erhalten bei Schreibzugriff `403`.

## Teilnehmer-Sessions

| Methode/Route | Zugriff | Zweck |
| --- | --- | --- |
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

## Fachliche Endpunkte

Mit aktiver Teilnehmer- oder Adminsession lesbar: `GET /api/projects/{projectId}`, `/participants/me`, `/teams`, `/shows`, `/shows/{showId}/slides`.

Admin-only: `PUT`/`DELETE /api/projects/{projectId}`; Teilnehmer `GET`/`POST /participants`, `PUT`/`DELETE /participants/{participantId}`; Teams `POST /teams`, `PUT`/`DELETE /teams/{teamId}`; Shows `POST /shows`, `DELETE /shows/{showId}`; Slides `POST /shows/{showId}/slides`, `PUT`/`DELETE /shows/{showId}/slides/{slideId}`.

Verschachtelte IDs werden gegen Projekt und Show geprüft. Das Datenmodell unterstützt wiederverwendbare Shows, aber ein API-Endpunkt zum Verknüpfen einer vorhandenen Show mit einem weiteren Projekt fehlt noch.

## Noch nicht umgesetzt

Es gibt noch keine Projektanlage oder Projektverwaltung im Adminclient. Memberships lassen sich aktuell nur über die API an bereits vorhandene Projekte hängen. Teilnehmernummern sind vorhersehbar und derzeit allein ausreichend für den Teilnehmerlogin; das ist für einen privaten Prototyp gedacht, kein starker öffentlicher Identitätsnachweis. Vor öffentlichem Einsatz zweiten Faktor/Teilnahmecode und robusteren Abuse-Schutz ergänzen.