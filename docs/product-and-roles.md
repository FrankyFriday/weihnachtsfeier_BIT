# Produktvision und Rollen

## Zielbild

WeihnachtsfeierBIT ist eine interaktive Event-Software für Feiern, Teamevents und Spielabende. Innerhalb eines Projekts sammeln Teams über verschiedene Spiele hinweg Punkte. Spiele können spontan gestartet oder in vorbereiteten Shows durchgeführt werden. Eine Show besteht aus geordneten Slides; jede Slide beschreibt ein interaktives Spielelement, wie etwa Quizfragen, Hitster-Musikraten, Jeopardy-Kategoriengitter, Abstimmungen, Punktevergaben oder Live-Ranglisten.

Shows sind wiederverwendbar und können in verschiedenen Projekten eingebunden oder zwischen Administratoren geteilt werden. Teilnehmer melden sich über einen 6-stelligen Projekt-Code und ihre Teilnehmernummer auf ihrem Smartphone an.

## Rollen und Berechtigungskonzept

| Rolle | Ziel | Berechtigungen & Möglichkeiten |
| --- | --- | --- |
| **Projekt-Ersteller (`IsCreator`)** | Volle Kontrolle über das Event | Darf das Projekt verwalten, löschen, weitere Admins einladen und granulare Berechtigungen festlegen. |
| **Eingeladener Co-Admin** | Gezieltes Mitgestalten & Moderieren | Berechtigungen hängen von den zugewiesenen Flags ab:<br>- **Live-Session steuern**: Shows/Spontanevents starten, Folien schalten, Punkte vergeben, Session pausieren.<br>- **Teilnehmer & Teams**: Teilnehmer/Teams anlegen, bearbeiten, zufällig und möglichst gleichmäßig zuweisen.<br>- **Einstellungen**: Projektdetails & Namen anpassen.<br>- **Shows**: Shows im Projekt verknüpfen oder entfernen. |
| **Show-Ersteller** | Eigene Shows erstellen & teilen | Besitzt die Show; kann Folien bearbeiten, die Show duplizieren, löschen oder für andere Admins freigeben. |
| **Teilnehmer** | Mit Smartphone interaktiv teilnehmen | Login per Code & Nummer; zeigt aktive Events, Eingabemasken, Live-Ergebnisse, Chat, Musik-Wünsche und Infoseiten. Schaltet bei Verbindungsabbruch oder Pausierung automatisch Vollbild-Overlays frei. |
| **Beamer / Leinwand (`Canvas`)** | Projektion für den Saal | Unter `/play/beamer/{SessionCode}`. Zeigt das vom Spielleiter ausgewählte Live-Event, den Beamer-Fokus sowie animierte Live-Säulendiagramme mit Punktestand. |

## Interaktive Spiele & Slide-Typen

Das System unterstützt 16 spezialisierte Folientypen:
1. **Punktevergabe (`PointAwardSlide`):** Vertikales Säulendiagramm mit automatischer Smart-Zoom Skalierung und schwebenden `+10 Pkt!` Delta-Badges bei Punktebuchungen.
2. **Hitster:** Spotify-Anbindung mit Song-Player, Titel/Interpret/Veröffentlichungsjahr-Raten.
3. **Jeopardy:** Interaktives Kategoriengitter (100–500 Punkte) mit Antwortaufdeckung und Team-Punkteauswahl.
4. **Quiz:** Multiple-Choice Fragen mit Speed-Bonus und Zeitlimit.
5. **Poll:** Abstimmung mit prozentualer Live-Visualisierung.
6. **Estimation:** Schätzfrage mit Einheiten-Vergleich.
7. **Buzzer:** Schnelligkeits-Buzzer für das schnellste Team.
8. **Ranking:** Live-Tabellenstand & zeitlicher Treppenstufen-Verlaufsgraph.
9. **Announcement, Text, WordCloud, Slider, Rating, Ordering, Countdown:** Ankündigungen, Freitext, Wortwolken, Regler, Bewertungen, Sortierungen und Timer.
10. **Pantomime:** Der Admin wählt pro Team eine darstellende Person und eine gemeinsame Rundendauer. Alle Teams starten gleichzeitig. Zufällig ausgegebene Begriffe werden innerhalb der Runde nicht wiederholt; nur die jeweils ausgewählte Person sieht ihren aktuellen Begriff auf dem eigenen Gerät und kann ihn als erraten bestätigen. Der Beamer und die übrigen Teilnehmergeräte zeigen keinen Begriff, sondern gemeinsame Restzeit, Teams, Darstellende und Fortschritt. Nach Zeitablauf werden keine weiteren Begriffe ausgegeben und der Endstand zeigt die erratenen Begriffe pro Team.

Die Beameransicht zeigt während einer aktiven Sitzung einen QR-Code, der die Teilnehmerseite mit vorausgefülltem Session-Code öffnet.

## Verbindungsqualität & Status-Overlays

- **Live-Latenz-Anzeige:** Ein Empfangssymbol in der TopBar misst fortlaufend den Ping zum SignalR-Server (Grün/Gelb/Rot).
- **Verbindungsabbruch:** Bei Unterbrechung erscheint ein Vollbild-Overlay *"Verbindung unterbrochen"* mit Neuverbindungs-Button.
- **Session Pausieren:** Wenn ein Admin die Session pausiert, erscheint auf allen Smartphones das Vollbild-Overlay *"Session aktuell pausiert"*.
- **Session Beenden:** Beim Beenden der Session werden alle Teilnehmer auf das *"Session beendet"* Vollbild-Overlay mit Option zum Beitreten einer neuen Session geleitet.
