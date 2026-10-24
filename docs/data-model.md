# Datenmodell

Das Modell liegt in `WeihnachtsfeierBit.Data`. `WeihnachtsfeierDbContext` definiert EF-Mappings; `Migrations/` enthält versionierte Schemaänderungen.

## Kernentitäten

| Entity | Bedeutung |
| --- | --- |
| `Projekt` | Event-Kontext mit Adminmitgliedschaften und Show-Zuordnungen. |
| `ProjektAdmin` | Globales Konto mit eindeutigem normalisiertem Benutzernamen und Passwort-Hash. |
| `ProjektAdminProjekt` | Join-Entity für die n:m-Beziehung Konto ↔ Projekt, inklusive Zuordnungszeitpunkt. |
| `ProjektAdminSitzung` | Widerrufbare globale Admin-Anmeldesitzung; nicht projektgebunden. |
| `Teilnehmer` | Gehört genau zu einem Projekt; Nummer ist pro Projekt eindeutig, Team-Zuordnung optional. |
| `GameInstance` | Aktive Anmelde-Sitzung eines Projekts mit 6-stelligem Code. Höchstens eine aktive GameInstance pro Projekt. Speichert optional `ActiveCanvasEventId` für die Beamer/Leinwand-Anzeige. |
| `GameInstanceShow` | Instanz einer in der aktiven Session gestarteten Show (Modus `active`/`passive`, `CurrentSlideIndex`). |
| `GameInstanceEvent` | Konkretes aktives oder passives Event einer Show-Slide oder On-The-Fly Spontanevents (Status `active`/`completed`). |
| `GameInstanceEventResponse` | Antwort/Stimme/Schätzwert eines Teilnehmers für ein Event. Unique Index auf `(GameInstanceEventId, ParticipantId)` erzwingt Upserts ohne Duplikate. |
| `Sitzung` | Widerrufbare Teilnehmer-Sitzung mit Fremdschlüssel auf den Teilnehmer und die jeweilige `GameInstance`. |
| `Team` | Gehört zu einem Projekt (DisplayName, Color, Punkte). |
| `Show` | Projektunabhängige, benannte wiederverwendbare Show mit geordneten Slides. |
| `ShowProjektZuo` | Join-Entity für die n:m-Beziehung Show ↔ Projekt. Eine Show kann null, einem oder mehreren Projekten zugeordnet sein. |
| `Slide` | Show-ID, Position und konkrete Untertypen Poll, Announcement, Quiz, Ranking, Estimation oder Buzzer. |

Passwörter werden mit PBKDF2 gehasht. API-Tokens werden zufällig generiert; persistiert wird ausschließlich ihr SHA-256-Hash.

## Slides und JSONB

EF verwendet TPH: alle Slide-Untertypen liegen in `Slides`; `SlideType` ist der Discriminator. `ShowId` und `Position` bleiben relationale Spalten. Typspezifische Einstellungen liegen in `Parameters` als PostgreSQL-`jsonb`.

| Typ | Parameterbeispiele |
| --- | --- |
| Poll | Interner Name, Regie-Notizen, Deaktivieren, Frage, Optionen, Mehrfachauswahl, Verdeckte Ergebnisse, Prozentuale Anzeige, Nachträgliche Meinungsänderung erlauben (`allowChangeVote`) |
| Announcement | Interner Name, Regie-Notizen, Deaktivieren, Titel, Nachricht |
| Quiz | Interner Name, Regie-Notizen, Deaktivieren, Frage, Antworten, Richtige Antwortindizes, Punkte, Speed-Bonus, Zeitlimit, Erklärung |
| Ranking | Interner Name, Regie-Notizen, Deaktivieren, Titel (automatischer Projekt-Punktestand), Top-X Begrenzung, Podium-Animation |
| Estimation | Interner Name, Regie-Notizen, Deaktivieren, Schätzfrage, Richtiger Wert, Einheit, Maximale Punkte |
| Buzzer | Interner Name, Regie-Notizen, Deaktivieren, Buzzer-Frage, Punkte, Zeitlimit |

`SaveChanges` und `SaveChangesAsync` serialisieren die aktuellen mutable Parameter getrackter Slides. Neue Parameterfelder erfordern keine Tabellenmigration. Ein neuer Untertyp muss in `Slide.cs` und im EF-Discriminator in `OnModelCreating` ergänzt werden. `SchemaVersion` ist für spätere JSON-Formatmigrationen vorgesehen.

Der Show-Editor bietet für alle Slide-Typen (Ankündigung, Abstimmung, Quiz und Zwischenstand) ein grafisches Master-Detail-Formular inklusive internem Namen, Sortierpfeilen und PopUp-Dialog zum Hinzufügen neuer Slides.

## Admin- und Teilnehmerzugriff

Ein Admin-Konto ist projektunabhängig. Berechtigungen entstehen ausschließlich durch einen Datensatz in `ProjektAdminProjekt`; ein Admin kann damit mehrere Projekte verwalten. Entfernen einer Membership löscht nicht das globale Konto.

Teilnehmer und Teilnehmernummern gehören zu genau einem Projekt. Eine aktive Sitzung gilt als gültig, wenn sie nicht widerrufen und noch nicht abgelaufen ist. Adminsitzungen sind global und können über Memberships auf mehrere Projekte zugreifen.

## Shows und Projekte

Eine Show wird global erstellt und kann zunächst ohne Projektverknüpfung existieren. `ShowProjektZuo` gewährt dem Projekt Zugriff auf diese Show. Eine Verknüpfung zu lösen entfernt nur den Join-Datensatz; die Show und ihre Slides bleiben bestehen. Der Showname ist auf 120 Zeichen begrenzt. Die Migration `AddGlobalShowName` setzt für bestehende Shows den Defaultnamen `Neue Show`.

## Multi-Show & Live Event Ausführung

In einer aktiven `GameInstance` (Play-Session) können mehrere Shows und spontane Events gleichzeitig betrieben werden:

- **`GameInstanceShow`**: Verknüpft eine gestartete Show mit der Play-Session. Besitzt den Modus (`active` = Vordergrund / `passive` = Menüliste) und den aktuellen Slide-Index (`CurrentSlideIndex`).
- **`GameInstanceEvent`**: Repräsentiert ein konkretes Live-Event auf Teilnehmer-Smartphones. Es entsteht entweder automatisch durch die aktuell aktive Slide einer `GameInstanceShow` oder als On-The-Fly spontanes Event. Speichert `SlideType`, `Title`, `Mode` (`active`/`passive`), `Status` (`active`/`completed`), Start-/Endzeitpunkt und Parameter-JSON.

## Migrationen

Die Migration `GlobalAdminAccounts` übernimmt frühere Projektzuordnungen in die Join-Tabelle, bevor sie die alte Projektspalte entfernt. Admin-IDs und Adminsessions bleiben erhalten. Doppelte alte Benutzernamen werden deterministisch suffixiert, weil Benutzernamen jetzt global eindeutig sind. Der Downgrade bricht absichtlich ab, wenn ein Konto inzwischen null oder mehrere Projektmitgliedschaften besitzt.