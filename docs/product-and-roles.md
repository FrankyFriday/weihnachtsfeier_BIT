# Produktvision und Rollen

## Zielbild

WeihnachtsfeierBIT soll eine Software für Events werden. Innerhalb eines Projekts sammeln Teams über mehrere Spiele hinweg Punkte. Spiele können spontan moderiert oder in vorbereiteten Shows durchgeführt werden. Eine Show besteht aus Slides; jede Slide beschreibt einen aktiven Zustand, etwa eine Quizfrage, Ankündigung, Umfrage oder Rangliste.

Shows sollen wiederverwendbar und in mehreren Projekten einbindbar sein. Teilnehmer melden sich typischerweise über einen Projekt-QR-Code und ihre Teilnehmernummer an.

Die Teilnahme soll flexibel bleiben: Nicht jeder Gast muss ein Smartphone oder eine eigene Sitzung haben. Admins sollen Teilnehmer optional anlegen und Teams auch ohne individuelle Anmeldung verwalten können. Das ist eine Produktanforderung, noch keine vollständige UI-Funktion.

## Rollen und Stand

| Rolle | Ziel | Heute |
| --- | --- | --- |
| Admin / Moderator | Projekt konfigurieren, Teilnehmer und Teams verwalten, Shows moderieren | Globale Admin-Konten, Projektmitgliedschaften, Login/Logout und leeres Dashboard existieren. Projektanlage und Verwaltungsoberflächen fehlen. |
| Teilnehmer | Mit Nummer teilnehmen, Team/Profil sehen und Fragen beantworten | Projektgebundener Nummern-Login und widerrufbare Sessions existieren. Teilnehmerclient und Antworten fehlen. |
| Dashboard / Leinwand | Aktuelle Show-Slide und Eventinformationen groß anzeigen | Noch nicht implementiert. |

## Bereits vorhandene Grundlagen

- Admin-Konten sind global eindeutig und können mehreren Projekten zugeordnet werden.
- Teilnehmernummern sind pro Projekt eindeutig. Eine Nummer ist eine Kennung, kein Geheimnis.
- Admin- und Teilnehmersitzungen können ablaufen oder widerrufen werden.
- Projekte enthalten Teams und Teilnehmer; Shows werden über eine Join-Entity Projekten zugeordnet.
- Slides haben die Typen Poll, Announcement, Quiz und Ranking. Parameter liegen als JSONB.
- Der Team-Punktestand lässt sich administrativ aktualisieren. Ein Punkteverlauf, Spielrunden, Antwortwertung und Live-Showzustand existieren nicht.

## Nicht-Fokus

`Hitster/` ist ein separates Randprojekt. Es ist aktuell nicht in WeihnachtsfeierBIT integriert; eine spätere Einbettung ist denkbar, aber nicht Teil des gegenwärtigen Scopes.