# Dokumentation

Die Fachseiten beschreiben **WeihnachtsfeierBIT** und trennen den überprüfbaren Implementierungsstand von der Produktvision. Die [Systemaufbau-Anleitung](system-setup.md) beschreibt den Docker-Betrieb von WeihnachtsfeierBIT samt Nginx Proxy Manager und integrierter Hitster-Slide. Deren Playlist wird beim API-Build aus `Hitster/data/songs.json` eingebettet.

## Einstieg

- [Produktvision und Rollen](product-and-roles.md): Zielbild, Rollen und Funktionsstand
- [Architektur](architecture.md): Solution-Projekte, Laufzeit und Anfragefluss
- [Datenmodell](data-model.md): Entities, Beziehungen, Sessions und Slide-JSONB
- [API und Berechtigungen](api-and-auth.md): Endpunkte, Authentifizierung und Zugriffsmatrix
- [Entwicklung und Betrieb](development-and-operations.md): Docker, Migrationen, DataGrip und Diagnose
- [Gesamtsystem aufsetzen und betreiben](system-setup.md): Voraussetzungen, Konfiguration, Docker, NPM, Updates, Backups und Fehlerbehebung
- [Repository-Anweisungen für Agents](../AGENTS.md): kurze verbindliche Regeln für Änderungen

## Quellen der Wahrheit

Entities, `WeihnachtsfeierDbContext`, Migrationen und registrierte Endpunkte bestimmen das tatsächliche Verhalten. Bei Abweichungen zuerst den Code prüfen und danach die zuständige Doku aktualisieren.

## Dokumentation pflegen

Änderungen an Datenmodell, API, Berechtigungen, Betrieb oder Produktumfang müssen im selben Änderungsschritt die betreffende Fachseite aktualisieren. Diese Übersicht und die Root-README bleiben kurz und verlinken auf die zuständigen Seiten.