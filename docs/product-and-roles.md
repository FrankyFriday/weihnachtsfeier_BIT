# Produktvision und Rollen

## Zielbild

WeihnachtsfeierBIT soll eine Software für Events werden. Innerhalb eines Projekts sammeln Teams über mehrere Spiele hinweg Punkte. Spiele können spontan moderiert oder in vorbereiteten Shows durchgeführt werden. Eine Show besteht aus Slides; jede Slide beschreibt einen aktiven Zustand, etwa eine Quizfrage, Ankündigung, Umfrage oder Rangliste.

Shows sollen wiederverwendbar und in mehreren Projekten einbindbar sein. Teilnehmer melden sich typischerweise über einen Projekt-QR-Code und ihre Teilnehmernummer an.

Die Teilnahme soll flexibel bleiben: Nicht jeder Gast muss ein Smartphone oder eine eigene Sitzung haben. Admins sollen Teilnehmer optional anlegen und Teams auch ohne individuelle Anmeldung verwalten können. Das ist eine Produktanforderung, noch keine vollständige UI-Funktion.

## Rollen und Stand

| Rolle | Ziel | Heute |
| --- | --- | --- |
| Admin / Moderator | Projekte konfigurieren, Teilnehmer und Teams verwalten, Shows vorbereiten, Events planen und live moderieren | Globale Admin-Konten, Projektverwaltung, Teilnehmer/Team-CRUD (inkl. Nummern-Generator und Zufallsverteilung), Play-Sessions (`GameInstance`), globale Show-Bibliothek mit Master-Detail-Editor & atomarer Sortierung. Live-Session-Steuerung mit vereinheitlichtem Tabbed-Layout (1 Tab pro Show/Spontanevent), PowerPoint-Folienstreifen mit Direkt-Klick-Sprung, Beamer-Fokussierung (`📺 Beamer` Icon in Tab-Bar), zeitlich geplante Events mit Auto-Start-Worker, Event-Sichtbarkeit (`IsHidden`), Steuerungsmodus-Wahl (`host_controlled` vs. `self_paced`), Live-Auswertungs-Views für jede Slide sowie scrollbarem Spontanevent-Popup mit voll integrierten Slide-Editoren. |
| Teilnehmer | Mit Nummer teilnehmen, Team/Profil sehen, Event-Timeline verfolgen und interaktiv teilnehmen | Teilnehmer-Login unter `/play/{code}` oder `/play` im Projekt `WeihnachtsfeierBit.ParticipantClient`. Bietet ein modernes App-Layout mit fixer Bottom Navigation Bar (`🏠 Home` und `⚙️ Aktionen`), vertikalem Zeitstrahl-Feed (mit echten Knotenpunkten und Farbcodes für aktive/geplante Events), eigenem Einstellungs-Bildschirm für Namensänderungen sowie rahmenlose Vollbildansichten für interaktive Events. |
| Dashboard / Leinwand | Aktuelle Show-Slide und Eventinformationen groß anzeigen | API-Vorbereitung für Beamer-Eventauswahl (`ActiveCanvasEventId`) und Admin-Steuerung umgesetzt; Canvas-Client im Aufbau. |

## Bereits vorhandene Grundlagen

- Admin-Konten sind global eindeutig und können mehreren Projekten zugeordnet werden.
- Teilnehmernummern sind pro Projekt eindeutig. Eine Nummer ist eine Kennung, kein Geheimnis.
- Admin- und Teilnehmersitzungen können ablaufen oder widerrufen werden.
- Shows besitzen einen Namen und bestehen unabhängig von Projekten. Die Join-Entity `ShowProjektZuo` verknüpft eine Show mit null oder mehreren Projekten; das Entfernen eines Links löscht weder Show noch Slides.
- Slides haben die Typen Poll, Announcement, Quiz und Ranking. Parameter liegen als JSONB.
- Der Team-Punktestand lässt sich administrativ aktualisieren. Ein Punkteverlauf, Spielrunden, Antwortwertung und Live-Showzustand existieren nicht.

## Nicht-Fokus

`Hitster/` ist ein separates Randprojekt. Es ist aktuell nicht in WeihnachtsfeierBIT integriert; eine spätere Einbettung ist denkbar, aber nicht Teil des gegenwärtigen Scopes.