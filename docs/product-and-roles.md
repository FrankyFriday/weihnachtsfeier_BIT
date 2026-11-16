# Produktvision und Rollen

## Zielbild

WeihnachtsfeierBIT ist eine interaktive Event-Software für Feiern, Teamevents und Spielabende. Innerhalb eines Projekts sammeln Teams über verschiedene Spiele hinweg Punkte. Spiele können spontan gestartet oder in vorbereiteten Shows durchgeführt werden. Eine Show besteht aus geordneten Slides; jede Slide beschreibt ein interaktives Spielelement, wie etwa Quizfragen, Hitster-Musikraten, Jeopardy-Kategoriengitter, Abstimmungen, Punktevergaben oder Live-Ranglisten.

Shows sind wiederverwendbar und können in verschiedenen Projekten eingebunden oder zwischen Administratoren geteilt werden. Teilnehmer melden sich über einen 6-stelligen Session-Code und ihre Teilnehmernummer auf ihrem Smartphone an.

## Rollen und Berechtigungskonzept

| Rolle | Ziel | Berechtigungen & Möglichkeiten |
| --- | --- | --- |
| **Projekt-Ersteller (`IsCreator`)** | Volle Kontrolle über das Event | Darf das Projekt verwalten, löschen, weitere Admins einladen und granulare Berechtigungen festlegen. |
| **Eingeladener Co-Admin** | Gezieltes Mitgestalten & Moderieren | Berechtigungen hängen von den zugewiesenen Flags ab:<br>- **Live-Session steuern**: Shows/Spontanevents starten, Folien schalten, Punkte vergeben, Session pausieren.<br>- **Teilnehmer & Teams**: Teilnehmer/Teams anlegen, bearbeiten, zufällig und möglichst gleichmäßig zuweisen.<br>- **Einstellungen**: Projektdetails & Namen anpassen.<br>- **Shows**: Shows im Projekt verknüpfen oder entfernen. |
| **Show-Ersteller** | Eigene Shows erstellen & teilen | Besitzt die Show; kann Folien bearbeiten, die Show duplizieren, löschen oder für andere Admins freigeben. |
| **Teilnehmer** | Mit Smartphone interaktiv teilnehmen | Login per Code & Nummer; zeigt aktive Events, Eingabemasken, Live-Ergebnisse, Chat, Musik-Wünsche und Infoseiten. Schaltet bei Verbindungsabbruch oder Pausierung automatisch Vollbild-Overlays frei. |
| **Beamer / Leinwand (`Canvas`)** | Projektion für den Saal | Unter `/play/beamer/{SessionCode}`. Zeigt das vom Spielleiter ausgewählte Live-Event, den Beamer-Fokus sowie animierte Live-Säulendiagramme mit Punktestand. |

## Teilnehmer-Client

Der Einstieg unter `/play` führt in einer kurzen, mobiloptimierten Schrittfolge zur Eingabe von Session-Code und Teilnehmernummer. Der öffentliche Code-Resolver liefert die minimale und maximale Ziffernzahl der im Projekt angelegten Teilnehmernummern. Nach der Anmeldung lautet die URL `/play/{session-code}/{teilnehmernummer}`; ein Neuladen meldet anhand dieser Route erneut an, ohne ein Sitzungstoken in der URL oder im Browser-Speicher abzulegen. Abmelden widerruft die aktuelle API-Sitzung.

Wenn noch kein Name gespeichert ist und die Rechte es erlauben, wird vor dem Teilnehmerbereich nach einem Anzeigenamen gefragt. Die Teilnehmenden können ihn speichern oder ohne Namen fortfahren; ein Überspringen wird für die aktuelle Browser-Session gemerkt. Der Hinweis erklärt, dass Namen für andere Teilnehmende und Moderatoren sichtbar sind. Erfolg-, Warn- und Fehlermeldungen erscheinen als kurz eingeblendete Snackbar oben, die automatisch verschwindet und sich auch per Wischgeste schließen lässt. Im Teilnehmerbereich bieten die Profil-Schaltfläche und der Aktionen-Tab den Abmeldezugang. Das Profilmenü öffnet und schließt mit einer kurzen Animation und lässt sich durch Klick außerhalb schließen. Tabs, Aktionsansichten und Navigationselemente verwenden dezente Übergänge. In der Programmübersicht stehen laufende Aktionen zuerst; geplante Events sind nach ihrer Startzeit aufsteigend sortiert. Aktionen sind in kurze, antippbare Menüpunkte gruppiert: Profil, Team & Party, Informationen und Sitzung (nur Abmelden). Info-Seiten verwenden Markdown für Überschriften, Absätze, hervorgehobene Texte, Listen, Zitate, Links, Codeblöcke und Tabellen; spezielle Wörter werden nicht automatisch in Badges umgewandelt.

## Teilnehmer-Berechtigungen & Overrides (3-State System)

Das Berechtigungssystem ermöglicht Administratoren eine feingranulare Steuerung der Teilnehmer-Privilegien:
- **Globale Projekt-Standards**: Legt fest, welche Rechte (z. B. Chat-Nachrichten schreiben, Chat lesen, neue Chats erstellen, Teamnamen vorschlagen/abstimmen, Musikwünsche einreichen/abstimmen, Sprechrecht & Interaktion, eigenen Namen ändern) standardmäßig für alle Teilnehmer im Projekt gelten.
- **3-Zustands-Overrides pro Teilnehmer**: Jeder Teilnehmer kann individuell konfiguriert werden:
  - *Global übernehmen* (Standard): Übernimmt das globale Projekt-Standardrecht.
  - *An (Erzwungen)* (`+`): Erzwingt das Recht für diesen Teilnehmer, selbst wenn es global deaktiviert ist.
  - *Aus (Sperre)* (`-`): Entzieht dem Teilnehmer dieses Recht, selbst wenn es global aktiv ist.
- Chat-Leserechte werden serverseitig für API-Abrufe und Live-Chat-Events geprüft. Teilnehmer ohne Leserecht erhalten weder Nachrichten noch Vorschauen; das Erstellen von Direkt- und Gruppenchats ist separat steuerbar. Vorschläge, Abstimmungen, Eventantworten und Namensänderungen werden jeweils serverseitig gegen ihre Einzelrechte geprüft.
- **Regie-Seitenleisten-Indikator**: Die Teilnehmerliste im Dashboard zeigt Abweichungen vom Standard als kompakte Badges an (zusätzliche Rechte `+X` in **Grün**, entzogene Rechte `-Y` in **Rot**).

## Regie-Seitenleiste & Multi-Compartment Drawer

Das Session-Dashboard besitzt eine rechte Regie-Seitenleiste mit einem Icon-Dock für fünf Fachbereiche:
1. **Handy-Emulator (📱):** Live-Simulation eines Teilnehmer-Smartphones mit anpassbarer maximaler Höhe, um Ansichten direkt zu testen.
2. **Teilnehmer & Rechte (👥):** Durchsuchbare Teilnehmerliste mit Filtern (*Alle*, *Abweichend*, *+ Rechte*, *- Rechte*), Berechtigungs-Modal-Trigger und Direkt-Emulator.
3. **Punkte & Rangliste (📊):** Live-Zeitverlaufsgraph, Team-Punktestände, Punkte-Anpassen-Trigger und Quick-Start-Dropdown für Zwischenstand-Events.
4. **Quick-Chat (💬):** Eingebetteter Echtzeit-Chat mit Hub-Sharing, um ohne Seitenwechsel direkt mit Teams und Teilnehmern zu kommunizieren.
5. **Beamer-Steuerung (📽️):** Event auswählen oder nur Punktestand zeigen, Beamer öffnen, Live-Vorschau sowie Einstellungen für Punktestand-Layout, Teilnahme-QR-Code und Anzeigegrößen.

Die mobile Regie-Seite und die Desktop-Seitenleiste verwenden dieselben Tabs und Inhalte. Neue Regie-Bereiche werden daher zentral konfiguriert und erscheinen automatisch auch mobil.

Für die aktuell angezeigte Slide lassen sich die nur für Admins bestimmten Moderator-Notizen über eine unten angeheftete, höhenverstellbare Schublade einblenden. Die Breite der rechten Regie-Schublade ist per Maus verstellbar.

Beamer-Einstellungen gelten nur für die aktive Session und werden nach einer Dropdown-Auswahl sofort übernommen. Team-Punktestände können rechts mit Diagramm, als schmale Punkte-Leiste unten oder gar nicht angezeigt werden. Der QR-Code kann als kleine Karte, als großes Popup mit Teilnahmehinweisen und Session-Code oder gar nicht erscheinen. Schrift, Event-Inhalt, Punktestand und QR-Code lassen sich in begrenzten Stufen skalieren; Event- und Punktestandskalierung ändern zusätzlich die relative Größe ihrer Kacheln. Projekttitel und Event-Typ-Beschriftung lassen sich ausblenden. Die Beameransicht blendet Scrollleisten aus und passt Event- und Punktestandsinhalte automatisch an den verfügbaren Bildschirmplatz an. Für die lokale Vorschau lassen sich Breite und Höhe der Beamer-Auflösung manuell in einem Popup einstellen; die Vorschau rendert mit diesen Viewport-Abmessungen und wird in die Sidebar-Fläche skaliert. Standardmäßig bleibt das bisherige Layout mit rechter Punktestand-Anzeige und QR-Karte aktiv.

## Erweiterte Chat-Moderation & UX

- **Sender-Vorschau**: In der Chat-Listenübersicht wird vor jeder Nachrichtenvorschau der Absender angezeigt (z. B. *„Max: Hallo zusammen“*).
- **Gruppen-Umbenennung**: Moderatoren können Team- und benutzerdefinierte Chat-Gruppen umbenennen.
- **Support-Namen für Moderatoren**: Admin-Support-Chats werden für Moderatoren übersichtlich nach Teilnehmern benannt (z. B. *Support: Max (#12)*), während Teilnehmer weiterhin *„Moderation & Hilfe“* sehen.
- **Direktchat-Shortcut**: Ein Klick auf den Namen eines Teilnehmers im Chat-Raum öffnet sofort den Direktchat mit ihm.
- **Scroll-to-Bottom Pill**: Ein schwebendes *„↓ Neue Nachrichten“*-Element erscheint beim Hochscrollen, wenn neue Nachrichten eintreffen.
- **Enter-Taste & Textbox-Leeren**: Nachrichten senden per `Enter` (Zeilenumbruch per `Shift+Enter`) mit sofortigem Leeren des Eingabefelds.

## Interaktive Spiele & Slide-Typen

Das System unterstützt 16 spezialisierte Folientypen:
1. **Punktevergabe (`PointAwardSlide`):** Vertikales Säulendiagramm mit automatischer Smart-Zoom Skalierung und schwebenden `+10 Pkt!` Delta-Badges bei Punktebuchungen.
2. **Hitster:** Spotify-Anbindung mit Song-Player, Titel/Interpret/Veröffentlichungsjahr-Raten.
3. **Jeopardy:** Interaktives Kategoriengitter (100–500 Punkte) mit Antwortaufdeckung und Team-Punkteauswahl.
4. **Quiz:** Multiple-Choice-Fragen mit moderatorgesteuerten Frage-, Antwort-, gesperrt- und Ergebnisphasen. Optionale Timer steuern den Übergang zur Antwortphase und sperren Antworten nach Ablauf. Im offenen Modus erhält das Team für jede richtige Teilnehmerantwort Punkte; im Teammodus stimmen die Teilnehmenden gemeinsam ab, können aber nur ihre eigene Stimme ändern. Die Abstimmung zeigt Stimmenzahlen und Teilnehmer-Chips pro Antwort. Die Regie legt vor der Auflösung die offizielle Teamantwort fest.
5. **Poll:** Abstimmung mit prozentualer Live-Visualisierung.
6. **Estimation:** Schätzfragen mit denselben Phasen- und Timersteuerungen. Im offenen Modus erhalten die Teams mit der geringsten Abweichung die Punkte, Gleichstände eingeschlossen. Im Teammodus können Beiträge des aktiven Teams gemeinsam angepasst werden; der Moderator legt den offiziellen Teamwert fest und vergibt Punkte manuell.
7. **Buzzer:** Schnelligkeits-Buzzer für das schnellste Team.
8. **Ranking:** Live-Tabellenstand & zeitlicher Treppenstufen-Verlaufsgraph.
9. **Announcement, Text, WordCloud, Slider, Rating, Ordering, Countdown:** Ankündigungen, Freitext, Wortwolken, Regler, Bewertungen, Sortierungen und Timer.
10. **Pantomime:** Der Admin wählt pro Team eine darstellende Person und eine gemeinsame Rundendauer. Alle Teams starten gleichzeitig. Zufällig ausgegebene Begriffe werden innerhalb der Runde nicht wiederholt; das Slide kann ein Skip-Limit pro darstellender Person festlegen (0 deaktiviert das Überspringen). Die darstellende Person kann Begriffe innerhalb des Limits überspringen; Moderatoren können dies ebenfalls für ein Team tun oder nach einer zusätzlichen Bestätigung einen kostenlosen Skip ausführen. In der Regie können Moderatoren erratene Begriffe bestätigen; Teilnehmende können keine Punkte selbst bestätigen. Für die Nutzung am Handy kann die Regie eine Vollbild-Teamansicht öffnen, die nur den Begriff, Timer und Aktionen für ein Team zeigt. Der Beamer und übrige Teilnehmergeräte zeigen keinen Begriff, sondern gemeinsame Restzeit, Teams, Darstellende und Fortschritt. Nach Zeitablauf werden keine weiteren Begriffe ausgegeben und der Endstand zeigt die erratenen Begriffe pro Team.

Die Beameransicht kann während einer aktiven Sitzung einen QR-Code anzeigen, der die Teilnehmerseite mit vorausgefülltem Session-Code öffnet. Anzeige und Platzierung werden über die Beamer-Steuerung festgelegt.

Quiz- und Schätzfragen lassen sich in der Regie über die vier Schritte **Frage**, **Antworten**, **Antwortzeit beendet** und **Auflösung** mit Vor- und Zurück-Navigation steuern. Frage und Antwortoptionen bleiben in allen Schritten sichtbar. Timer laufen in den ersten beiden Schritten, lassen sich pausieren, fortsetzen, zurücksetzen, deaktivieren oder im Zeit-Popup bearbeiten. Ein Timer auf 0 beendet den aktuellen Schritt; bei manuellem Sperren endet er ebenfalls. Abgelaufene Antwortzeiten sperren Eingaben, ohne automatisch die Lösung zu zeigen. Bei Teamfragen muss nach Ende der Antwortzeit die offizielle Antwort bzw. Schätzung festgelegt werden. Moderatoren können Quizstimmen in jeder Phase ändern oder löschen; bereits vergebene Quizpunkte werden bei Korrekturen automatisch abgeglichen. Die Teilnehmer sehen im Sperrschritt einen deutlichen Hinweis und die Auflösung erscheint vollflächig grün oder rot mit eigener bzw. offizieller Teamantwort, korrekter Antwort und optionaler Erklärung. Die schnelle Punkteanpassung ist nur im Auflösungsschritt verfügbar. Richtige Antworten, Zielwerte und Erklärungen werden erst nach der Ergebnisfreigabe an Teilnehmergeräte und Beamer übertragen. Punkte aus Quiz und offenen Schätzfragen werden automatisch anhand der Antworten verbucht und bei späteren Moderationskorrekturen angepasst. Team-exklusive Antworten bleiben auf das aktive Team begrenzt. Jeopardy-Feldwerte dienen nur zur Anzeige und haben keinen Einfluss auf die Wertung dieser Fragen. Der vorhandene Speed-Bonus-Schalter konfiguriert derzeit noch keine zusätzliche Wertung.

## Verbindungsqualität & Status-Overlays

- **Live-Latenz-Anzeige:** Ein Empfangssymbol in der TopBar misst fortlaufend den Ping zum SignalR-Server (Grün/Gelb/Rot).
- **Verbindungsabbruch:** Bei Unterbrechung erscheint ein Vollbild-Overlay *"Verbindung unterbrochen"* mit Neuverbindungs-Button.
- **Session Pausieren:** Wenn ein Admin die Session pausiert, erscheint auf allen Smartphones das Vollbild-Overlay *"Session aktuell pausiert"*.
- **Session Beenden:** Beim Beenden der Session werden alle Teilnehmer auf das *"Session beendet"* Vollbild-Overlay mit Option zum Beitreten einer neuen Session geleitet.
