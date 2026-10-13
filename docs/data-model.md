# Datenmodell

Das Modell liegt in `WeihnachtsfeierBit.Data`. `WeihnachtsfeierDbContext` definiert EF-Mappings; `Migrations/` enthält versionierte Schemaänderungen.

## Kernentitäten

| Entity | Bedeutung |
| --- | --- |
| `Projekt` | Event-Kontext mit Adminmitgliedschaften und Show-Zuordnungen. |
| `ProjektAdmin` | Globales Konto mit eindeutigem normalisiertem Benutzernamen und Passwort-Hash. |
| `ProjektAdminProjekt` | Join-Entity für die n:m-Beziehung Konto ↔ Projekt, inklusive Zuordnungszeitpunkt. |
| `ProjektAdminSitzung` | Widerrufbare globale Admin-Anmeldesitzung; nicht projektgebunden. |
| `Teilnehmer` | Gehört genau zu einem Projekt; Nummer ist pro Projekt eindeutig, Team optional. |
| `Sitzung` | Widerrufbare Teilnehmer-Sitzung mit Fremdschlüssel auf den Teilnehmer. |
| `Team` | Gehört zu einem Projekt und enthält aktuell einen Punktestand. |
| `Show` | Projektunabhängige, benannte wiederverwendbare Show mit geordneten Slides. |
| `ShowProjektZuo` | Join-Entity für die n:m-Beziehung Show ↔ Projekt. Eine Show kann null, einem oder mehreren Projekten zugeordnet sein. |
| `Slide` | Show-ID, Position und konkrete Untertypen Poll, Announcement, Quiz oder Ranking. |

Passwörter werden mit PBKDF2 gehasht. API-Tokens werden zufällig generiert; persistiert wird ausschließlich ihr SHA-256-Hash.

## Slides und JSONB

EF verwendet TPH: alle Slide-Untertypen liegen in `Slides`; `SlideType` ist der Discriminator. `ShowId` und `Position` bleiben relationale Spalten. Typspezifische Einstellungen liegen in `Parameters` als PostgreSQL-`jsonb`.

| Typ | Parameterbeispiele |
| --- | --- |
| Poll | Frage, Optionen, Mehrfachauswahl |
| Announcement | Titel, Nachricht |
| Quiz | Frage, Antworten, richtige Antwortindizes, Zeitlimit |
| Ranking | Titel, Ranglisteneinträge |

`SaveChanges` und `SaveChangesAsync` serialisieren die aktuellen mutable Parameter getrackter Slides. Neue Parameterfelder erfordern keine Tabellenmigration. Ein neuer Untertyp muss in `Slide.cs` und im EF-Discriminator in `OnModelCreating` ergänzt werden. `SchemaVersion` ist für spätere JSON-Formatmigrationen vorgesehen.

## Admin- und Teilnehmerzugriff

Ein Admin-Konto ist projektunabhängig. Berechtigungen entstehen ausschließlich durch einen Datensatz in `ProjektAdminProjekt`; ein Admin kann damit mehrere Projekte verwalten. Entfernen einer Membership löscht nicht das globale Konto.

Teilnehmer und Teilnehmernummern gehören zu genau einem Projekt. Eine aktive Sitzung gilt als gültig, wenn sie nicht widerrufen und noch nicht abgelaufen ist. Adminsitzungen sind global und können über Memberships auf mehrere Projekte zugreifen.

## Shows und Projekte

Eine Show wird global erstellt und kann zunächst ohne Projektverknüpfung existieren. `ShowProjektZuo` gewährt dem Projekt Zugriff auf diese Show. Eine Verknüpfung zu lösen entfernt nur den Join-Datensatz; die Show und ihre Slides bleiben bestehen. Der Showname ist auf 120 Zeichen begrenzt. Die Migration `AddGlobalShowName` setzt für bestehende Shows den Defaultnamen `Neue Show`.

## Migrationen

Die Migration `GlobalAdminAccounts` übernimmt frühere Projektzuordnungen in die Join-Tabelle, bevor sie die alte Projektspalte entfernt. Admin-IDs und Adminsessions bleiben erhalten. Doppelte alte Benutzernamen werden deterministisch suffixiert, weil Benutzernamen jetzt global eindeutig sind. Der Downgrade bricht absichtlich ab, wenn ein Konto inzwischen null oder mehrere Projektmitgliedschaften besitzt.