# Datenmodell

Das Modell liegt in `WeihnachtsfeierBit.Data`. `WeihnachtsfeierDbContext` definiert EF-Mappings; `Migrations/` enthält versionierte Schemaänderungen.

## Kernentitäten

| Entity | Bedeutung |
| --- | --- |
| `Projekt` | Event-Kontext mit Adminmitgliedschaften (`Admins`), Ersteller-Zuordnung (`ErstellerAdminId`), Show-Zuordnungen und globalen Projekt-Standardberechtigungen (`PermissionsJson`). |
| `ProjektAdmin` | Globales Konto mit eindeutigem normalisiertem Benutzernamen und Passwort-Hash. |
| `ProjektAdminProjekt` | Join-Entity für die n:m-Beziehung Konto ↔ Projekt, inklusive granularen Berechtigungen (`CanControlSession`, `CanManageParticipants`, `CanEditProjectSettings`, `CanManageShows`). |
| `ProjektAdminShow` | Join-Entity für die n:m-Beziehung Konto ↔ Show für das Teilen von Shows. |
| `ProjektAdminSitzung` | Widerrufbare globale Admin-Anmeldesitzung; nicht projektgebunden. |
| `Teilnehmer` | Gehört genau zu einem Projekt; Nummer ist pro Projekt eindeutig, Team-Zuordnung optional. |
| `TeilnehmerBerechtigung` | Speichert individuelle 3-State-Berechtigungs-Overrides für einen Teilnehmer (`PermissionsJson`), darunter Chat-, Vorschlags-, Abstimmungs-, Interaktions- und Namensänderungsrechte. |
| `GameInstance` | Aktive Anmelde-Sitzung eines Projekts mit 6-stelligen Code. Höchstens eine aktive GameInstance pro Projekt. Speichert `ActiveCanvasEventId` für die Beamer-Anzeige, `IsPaused` für Session-Pausierung und `CanvasSettingsJson` für sessionbezogene Beamer-Layouts. |
| `GameInstanceEvent` | Vereinheitlichtes Live-Event (Multi-Slide Show oder Spontanevent). Speichert `Title`, `Mode` (`active`/`passive`), `NavigationMode` (`host_controlled`/`self_paced`), `CurrentSlideIndex`, `Position` (manuelle Event-Reihenfolge je Session, neue Events werden hinten angehängt), `IsHidden`, `ScheduledStartUtc`, `AutoStartOnSchedule` und `SlidesJson` (`jsonb` Array aller Folien). |
| `GameInstanceEventResponse` | Antwort/Stimme/Schätzwert eines Teilnehmers für eine konkrete Folie eines Events. Unique Index auf `(GameInstanceEventId, SlideId, ParticipantId)` erzwingt eindeutige Upserts ohne Duplikate. |
| `Sitzung` | Widerrufbare Teilnehmersitzung mit Fremdschlüssel auf Teilnehmer und `GameInstance`, User-Agent, optionalem per Projekt gehashtem Browser-Schlüssel, abgeleitetem Browser-/Plattformnamen und `CreatedByAdminEmulator`. |
| `Team` | Gehört zu einem Projekt (DisplayName, Color, Punkte). |
| `TeamPointLog` | Chronologisches Punkte-Logbuch für Buchungen, Bonifikationen und Abzüge mit Zeitstempel und Begründung. |
| `ChatGroup`, `ChatGroupMember`, `ChatMessage`, `ChatUserRead` | Chat-System für globale Kanäle, Team-Chats, Admin-Support-Kanäle (`admin_support`), Direktchats und benutzerdefinierte Gruppen mit Live-Sync und Lesestatus. |
| `Show` | Projektunabhängige, benannte wiederverwendbare Show mit geordneten Slides und Ersteller-Zuordnung (`ErstellerAdminId`). |
| `ShowProjektZuo` | Join-Entity für die n:m-Beziehung Show ↔ Projekt. |
| `Slide` | Show-ID, Position und konkrete Untertypen (Poll, Announcement, Quiz, Ranking, Estimation, Buzzer, Jeopardy, Hitster, Pantomime, PointAward, Text, WordCloud, Slider, Rating, Ordering, Countdown). |

Passwörter werden mit PBKDF2 gehasht. API-Tokens werden zufällig generiert; persistiert wird ausschließlich ihr SHA-256-Hash.

## Slides und JSONB

EF verwendet TPH: alle Slide-Untertypen liegen in `Slides`; `SlideType` ist der Discriminator. `ShowId` und `Position` bleiben relationale Spalten. Typspezifische Einstellungen liegen in `Parameters` als PostgreSQL-`jsonb`.

| Typ | Parameterbeispiele |
| --- | --- |
| Poll | Frage, Optionen, Mehrfachauswahl, Verdeckte Ergebnisse, Prozentuale Anzeige, Nachträgliche Meinungsänderung erlauben (`allowChangeVote`) |
| Announcement | Titel, Nachricht |
| Quiz | Frage, Antworten, Richtige Antwortindizes, Punkte, Speed-Bonus, Antwortzeitlimit (`timeLimitSeconds`), optionale Fragezeit (`questionTimeLimitSeconds`), Modus (`teamMode`), Erklärung; Laufzeitphase und Timer liegen in `state.questionWorkflow`. |
| Ranking | Titel (automatischer Projekt-Punktestand), Top-X Begrenzung, Podium-Animation und Diagrammtyp (`bar_chart` oder `line_graph`) |
| PointAward | Punktevergabe-Slide mit echtem vertikalem Säulendiagramm, automatischer Smart-Zoom Skalierung und schwebenden `+10 Pkt!` Delta-Badges |
| Hitster | Spotify-Integration, Song-Titel, Interpret, Veröffentlichungsjahr, Musikplayer-Steuerung |
| Estimation | Schätzfrage, Richtiger Wert, Einheit, Maximale Punkte, Antwortzeitlimit (`timeLimitSeconds`), optionale Fragezeit (`questionTimeLimitSeconds`), Modus (`teamMode`), Erklärung; Laufzeitphase, offizieller Teamwert und Timer liegen in `state.questionWorkflow`. |
| Buzzer | Buzzer-Frage, Punkte, Zeitlimit |
| Jeopardy | Kategorien mit Fragen (Punktwert, Frage, Antwort); Laufzeitstatus mit verwendeten Feldern, aktivem Team, aktiver Frage und Antwortfreigabe. |
| Pantomime | Kategorie, 3–100 eindeutige Begriffe, Rundendauer (10–600 Sekunden) und Skip-Limit pro darstellender Person (0–100; fehlend bei älteren Slides entspricht 0); Laufzeitstatus mit Teamzuordnungen, Darstellenden, gemeinsamer Startzeit/Deadline, Team-Punktzahlen, verwendeten regulären Skips und zufälliger Begriffszuteilung. Ein bestätigter kostenloser Admin-Skip erhöht den Skip-Zähler nicht. Der Laufzeitstatus speichert nur interne Begriffsindizes, keine Klartextbegriffe. |
| Text / WordCloud | Freitext-Fragen & dynamische Wortwolken-Auswertung |
| Slider / Rating | Schieberegler & Sterne-Bewertungen |
| Ordering / Countdown | Reihenfolge-Sortierung & visuelle Timer |

`SaveChanges` und `SaveChangesAsync` serialisieren die aktuellen mutable Parameter getrackter Slides. Neue Parameterfelder erfordern keine Tabellenmigration.

Quiz- und Schätzfragen speichern ihren Liveablauf ohne Schemaänderung in `SlidesJson`: `state.questionWorkflow` enthält Phase, Timerzustand, offizielle Teamantwort bzw. offiziellen Schätzwert und die nachvollziehbaren automatischen Punktebuchungen. Die Phasen sind `question`, `answers`, `locked` und `results`. Einzel- und Teamantworten liegen als Upserts in `GameInstanceEventResponse`; die Datensätze bleiben nach der Ergebnisfreigabe zur Überprüfung erhalten. Automatische Punktebuchungen werden anhand des gespeicherten Wertungsstands angepasst, wenn die Moderation Ergebnisse korrigiert. Team-exklusive Beiträge werden nur an Admins und das aktive Team verteilt; Lösung und Erklärung werden in öffentlichen Zuständen erst in der Ergebnisphase freigegeben.

Pantomime-Rundendaten liegen im `state`-Objekt der Event-Slide-Parameter und benötigen keine Schemaänderung. Die öffentliche Event-/Beameransicht erhält ausschließlich freigegebene Teamfortschritte; Begriffspool und interne Zuteilungsindizes werden herausgefiltert. Der aktuelle Klartextbegriff wird nur über den Teilnehmer-Endpunkt für die zugewiesene darstellende Person oder die geschützte Admin-Regie ausgegeben. Skip-Limits und Moderatorbestätigung werden serverseitig geprüft.

Für die Geräteübersicht erzeugt der Teilnehmerclient einmalig eine zufällige Browser-ID im First-Party-`localStorage`. Die API speichert davon nur einen SHA-256-Hash, zusätzlich zum bereits erfassten User-Agent und einem daraus abgeleiteten Browser-/Plattformnamen. Der Hash ist projektgebunden; es werden keine Canvas-, Schrift-, Bildschirm- oder anderen Hardwaremerkmale für ein Fingerprinting ausgewertet. Sitzungen aus dem Admin-Emulator werden durch ein kurzlebiges, einmaliges und an Projekt, Session und Teilnehmer gebundenes Ticket markiert; ein frei übermitteltes Emulator-Boolean wird nicht vertraut. Geräte-Metadaten folgen dem bestehenden Lebenszyklus der Sitzung. Die aktive Geräteübersicht filtert abgelaufene und widerrufene Sitzungen aus; alte Sitzungen ohne Browser-ID bleiben einzeln erkennbar.

## Admin- und Teilnehmerzugriff

Ein Admin-Konto ist projektunabhängig. Berechtigungen für Projekte entstehen durch Datensätze in `ProjektAdminProjekt`. Der **Ersteller (`ErstellerAdminId`)** besitzt uneingeschränkte Rechte und kann für eingeladene Co-Admins folgende Berechtigungen festlegen:
- `CanControlSession` (Live-Session steuern / schalten)
- `CanManageParticipants` (Teilnehmer & Teams verwalten)
- `CanEditProjectSettings` (Projektdetails bearbeiten)
- `CanManageShows` (Shows verknüpfen / entfernen)

Teilnehmer und Teilnehmernummern gehören zu genau einem Projekt. Eine aktive Sitzung gilt als gültig, wenn sie nicht widerrufen und noch nicht abgelaufen ist.
