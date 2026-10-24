# Produktvision und Rollen

## Zielbild

WeihnachtsfeierBIT soll eine Software für Events werden. Innerhalb eines Projekts sammeln Teams über mehrere Spiele hinweg Punkte. Spiele können spontan moderiert oder in vorbereiteten Shows durchgeführt werden. Eine Show besteht aus Slides; jede Slide beschreibt einen aktiven Zustand, etwa eine Quizfrage, Ankündigung, Umfrage oder Rangliste.

Shows sollen wiederverwendbar und in mehreren Projekten einbindbar sein. Teilnehmer melden sich typischerweise über einen Projekt-QR-Code und ihre Teilnehmernummer an.

Die Teilnahme soll flexibel bleiben: Nicht jeder Gast muss ein Smartphone oder eine eigene Sitzung haben. Admins sollen Teilnehmer optional anlegen und Teams auch ohne individuelle Anmeldung verwalten können. Das ist eine Produktanforderung, noch keine vollständige UI-Funktion.

## Rollen und Stand

| Rolle | Ziel | Heute |
| --- | --- | --- |
| Admin / Moderator | Projekte konfigurieren, Teilnehmer und Teams verwalten, Shows vorbereiten und live moderieren | Globale Admin-Konten, Projektverwaltung, Teilnehmer/Team-CRUD (inkl. Nummern-Generator und Zufallsverteilung), Play-Sessions (`GameInstance`), globale Show-Bibliothek mit Master-Detail-Editor & atomarer Sortierung. Live-Session-Steuerung mit einheitlichem Tabbed-Layout (1 Tab pro Show/Spontanevent), PowerPoint-Folienstreifen mit Direkt-Klick-Sprung, Show-Navigation sowie scrollbarem Spontanevent-Popup mit voll integrierten Slide-Editoren. |
| Teilnehmer | Mit Nummer teilnehmen, Team/Profil sehen und interaktiv an Events teilnehmen | Teilnehmer-Login unter `/play/{code}` oder `/play` im Projekt `WeihnachtsfeierBit.ParticipantClient`. Bietet rahmenlose Full-Bleed-Vollbildansichten für aktive Events, Menüauswahl für passive Events, automatische SignalR-Live-Aktualisierung bei Slide-Wechseln, benutzerfreundliche deutsche Eventnamen sowie modulare Slide-Renderers für alle 6 Slide-Typen (Ankündigung, Abstimmung, Quiz, Schätzung, Buzzer, Rangliste) mit Antwortabgabe. |
| Dashboard / Leinwand | Aktuelle Show-Slide und Eventinformationen groß anzeigen | Noch nicht implementiert. |

## Bereits vorhandene Grundlagen

- Admin-Konten sind global eindeutig und können mehreren Projekten zugeordnet werden.
- Teilnehmernummern sind pro Projekt eindeutig. Eine Nummer ist eine Kennung, kein Geheimnis.
- Admin- und Teilnehmersitzungen können ablaufen oder widerrufen werden.
- Shows besitzen einen Namen und bestehen unabhängig von Projekten. Die Join-Entity `ShowProjektZuo` verknüpft eine Show mit null oder mehreren Projekten; das Entfernen eines Links löscht weder Show noch Slides.
- Slides haben die Typen Poll, Announcement, Quiz und Ranking. Parameter liegen als JSONB.
- Der Team-Punktestand lässt sich administrativ aktualisieren. Ein Punkteverlauf, Spielrunden, Antwortwertung und Live-Showzustand existieren nicht.

## Nicht-Fokus

`Hitster/` ist ein separates Randprojekt. Es ist aktuell nicht in WeihnachtsfeierBIT integriert; eine spätere Einbettung ist denkbar, aber nicht Teil des gegenwärtigen Scopes.