# Datenmodell

Das Modell liegt in `WeihnachtsfeierBit.Data`. `WeihnachtsfeierDbContext` definiert EF-Mappings; `Migrations/` enthält versionierte Schemaänderungen.

## Kernentitäten

| Entity | Bedeutung |
| --- | --- |
| `Projekt` | Event-Kontext mit Adminmitgliedschaften (`Admins`), Ersteller-Zuordnung (`ErstellerAdminId`) und Show-Zuordnungen. |
| `ProjektAdmin` | Globales Konto mit eindeutigem normalisiertem Benutzernamen und Passwort-Hash. |
| `ProjektAdminProjekt` | Join-Entity für die n:m-Beziehung Konto ↔ Projekt, inklusive granularen Berechtigungen (`CanControlSession`, `CanManageParticipants`, `CanEditProjectSettings`, `CanManageShows`). |
| `ProjektAdminShow` | Join-Entity für die n:m-Beziehung Konto ↔ Show für das Teilen von Shows. |
| `ProjektAdminSitzung` | Widerrufbare globale Admin-Anmeldesitzung; nicht projektgebunden. |
| `Teilnehmer` | Gehört genau zu einem Projekt; Nummer ist pro Projekt eindeutig, Team-Zuordnung optional. |
| `GameInstance` | Aktive Anmelde-Sitzung eines Projekts mit 6-stelligem Code. Höchstens eine aktive GameInstance pro Projekt. Speichert `ActiveCanvasEventId` für die Beamer-Anzeige und `IsPaused` für Session-Pausierung. |
| `GameInstanceEvent` | Vereinheitlichtes Live-Event (Multi-Slide Show oder Spontanevent). Speichert `Title`, `Mode` (`active`/`passive`), `NavigationMode` (`host_controlled`/`self_paced`), `CurrentSlideIndex`, `IsHidden`, `ScheduledStartUtc`, `AutoStartOnSchedule` und `SlidesJson` (`jsonb` Array aller Folien). |
| `GameInstanceEventResponse` | Antwort/Stimme/Schätzwert eines Teilnehmers für eine konkrete Folie eines Events. Unique Index auf `(GameInstanceEventId, SlideId, ParticipantId)` erzwingt eindeutige Upserts ohne Duplikate. |
| `Sitzung` | Widerrufbare Teilnehmer-Sitzung mit Fremdschlüssel auf den Teilnehmer und die jeweilige `GameInstance`. |
| `Team` | Gehört zu einem Projekt (DisplayName, Color, Punkte). |
| `TeamPointLog` | Chronologisches Punkte-Logbuch für Buchungen, Bonifikationen und Abzüge mit Zeitstempel und Begründung. |
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
| Quiz | Frage, Antworten, Richtige Antwortindizes, Punkte, Speed-Bonus, Zeitlimit, Erklärung |
| Ranking | Titel (automatischer Projekt-Punktestand), Top-X Begrenzung, Podium-Animation und Diagrammtyp (`bar_chart` oder `line_graph`) |
| PointAward | Punktevergabe-Slide mit echtem vertikalem Säulendiagramm, automatischer Smart-Zoom Skalierung und schwebenden `+10 Pkt!` Delta-Badges |
| Hitster | Spotify-Integration, Song-Titel, Interpret, Veröffentlichungsjahr, Musikplayer-Steuerung |
| Estimation | Schätzfrage, Richtiger Wert, Einheit, Maximale Punkte |
| Buzzer | Buzzer-Frage, Punkte, Zeitlimit |
| Jeopardy | Kategorien mit Fragen (Punktwert, Frage, Antwort); Laufzeitstatus mit verwendeten Feldern, aktivem Team, aktiver Frage und Antwortfreigabe. |
| Pantomime | Kategorie, 3–100 eindeutige Begriffe und Rundendauer (10–600 Sekunden); Laufzeitstatus mit Teamzuordnungen, Darstellenden, gemeinsamer Startzeit/Deadline, Team-Punktzahlen und zufälliger Begriffszuteilung. Der Laufzeitstatus speichert nur interne Begriffsindizes, keine Klartextbegriffe. |
| Text / WordCloud | Freitext-Fragen & dynamische Wortwolken-Auswertung |
| Slider / Rating | Schieberegler & Sterne-Bewertungen |
| Ordering / Countdown | Reihenfolge-Sortierung & visuelle Timer |

`SaveChanges` und `SaveChangesAsync` serialisieren die aktuellen mutable Parameter getrackter Slides. Neue Parameterfelder erfordern keine Tabellenmigration.

Pantomime-Rundendaten liegen im `state`-Objekt der Event-Slide-Parameter und benötigen keine Schemaänderung. Die öffentliche Event-/Beameransicht erhält ausschließlich freigegebene Teamfortschritte; Begriffspool und interne Zuteilungsindizes werden herausgefiltert. Der aktuelle Klartextbegriff wird nur über einen Teilnehmer-Endpunkt ausgegeben, der die aktive Sitzung und die gespeicherte Zuordnung zur darstellenden Person prüft.

## Admin- und Teilnehmerzugriff

Ein Admin-Konto ist projektunabhängig. Berechtigungen für Projekte entstehen durch Datensätze in `ProjektAdminProjekt`. Der **Ersteller (`ErstellerAdminId`)** besitzt uneingeschränkte Rechte und kann für eingeladene Co-Admins folgende Berechtigungen festlegen:
- `CanControlSession` (Live-Session steuern / schalten)
- `CanManageParticipants` (Teilnehmer & Teams verwalten)
- `CanEditProjectSettings` (Projektdetails bearbeiten)
- `CanManageShows` (Shows verknüpfen / entfernen)

Teilnehmer und Teilnehmernummern gehören zu genau einem Projekt. Eine aktive Sitzung gilt als gültig, wenn sie nicht widerrufen und noch nicht abgelaufen ist.
