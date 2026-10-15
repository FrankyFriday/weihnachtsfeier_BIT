# Produktvision und Rollen

## Zielbild

WeihnachtsfeierBIT soll eine Software für Events werden. Innerhalb eines Projekts sammeln Teams über mehrere Spiele hinweg Punkte. Spiele können spontan moderiert oder in vorbereiteten Shows durchgeführt werden. Eine Show besteht aus Slides; jede Slide beschreibt einen aktiven Zustand, etwa eine Quizfrage, Ankündigung, Umfrage oder Rangliste.

Shows sollen wiederverwendbar und in mehreren Projekten einbindbar sein. Teilnehmer melden sich typischerweise über einen Projekt-QR-Code und ihre Teilnehmernummer an.

Die Teilnahme soll flexibel bleiben: Nicht jeder Gast muss ein Smartphone oder eine eigene Sitzung haben. Admins sollen Teilnehmer optional anlegen und Teams auch ohne individuelle Anmeldung verwalten können. Das ist eine Produktanforderung, noch keine vollständige UI-Funktion.

## Rollen und Stand

| Rolle | Ziel | Heute |
| --- | --- | --- |
| Admin / Moderator | Projekte konfigurieren, Teilnehmer und Teams verwalten, Shows vorbereiten | Globale Admin-Konten, Projektanlage und -übersicht, Projekteinstellungen, Teilnehmer- und Teamverwaltung (inklusive automatischer 2-, 3- oder 4-stelliger Nummern-Generierung und zufälliger, gleichmäßiger Teamverteilung) sowie eine globale Show-Bibliothek existieren. Shows können unabhängig von Projekten angelegt und mit Projekten verknüpft oder daraus gelöst werden. Der Show-Editor ist derzeit ein leerer Platzhalter. |
| Teilnehmer | Mit Nummer teilnehmen, Team/Profil sehen und Fragen beantworten | Projektgebundener Nummern-Login unter `/play/{projectId}` im eigenständigen Projekt `WeihnachtsfeierBit.ParticipantClient`, mobiles/desktop-optimiertes Team-Dashboard (Teamname, Rang, Punkte, Profilname-Eingabe, Teammitglieder, Gesamtrangliste) mit Echtzeit-WebSockets via SignalR `GameStateHub` vorhanden. Antwortabgabe für Slides fehlt noch. |
| Dashboard / Leinwand | Aktuelle Show-Slide und Eventinformationen groß anzeigen | Noch nicht implementiert. |

## Bereits vorhandene Grundlagen

- Admin-Konten sind global eindeutig und können mehreren Projekten zugeordnet werden.
- Teilnehmernummern sind pro Projekt eindeutig. Eine Nummer ist eine Kennung, kein Geheimnis.
- Admin- und Teilnehmersitzungen können ablaufen oder widerrufen werden.
- Shows besitzen einen Namen und bestehen unabhängig von Projekten. Die Join-Entity `ShowProjektZuo` verknüpft eine Show mit null oder mehreren Projekten; das Entfernen eines Links löscht weder Show noch Slides.
- Slides haben die Typen Poll, Announcement, Quiz und Ranking. Parameter liegen als JSONB.
- Der Team-Punktestand lässt sich administrativ aktualisieren. Ein Punkteverlauf, Spielrunden, Antwortwertung, ein Show-Editor und Live-Showzustand existieren nicht.

## Nicht-Fokus

`Hitster/` ist ein separates Randprojekt. Es ist aktuell nicht in WeihnachtsfeierBIT integriert; eine spätere Einbettung ist denkbar, aber nicht Teil des gegenwärtigen Scopes.