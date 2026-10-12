# Dokumentation

Diese Dokumentation beschreibt ausschließlich **WeihnachtsfeierBIT**. Sie trennt den überprüfbaren Implementierungsstand von der Produktvision. `Hitster/` ist ein unabhängiges Randprojekt und derzeit nicht im Fokus.

## Einstieg

- [Produktvision und Rollen](product-and-roles.md): Zielbild, Rollen und Funktionsstand
- [Architektur](architecture.md): Solution-Projekte, Laufzeit und Anfragefluss
- [Datenmodell](data-model.md): Entities, Beziehungen, Sessions und Slide-JSONB
- [API und Berechtigungen](api-and-auth.md): Endpunkte, Authentifizierung und Zugriffsmatrix
- [Entwicklung und Betrieb](development-and-operations.md): Docker, Migrationen, DataGrip und Diagnose
- [Repository-Anweisungen für Agents](../AGENTS.md): kurze verbindliche Regeln für Änderungen

## Quellen der Wahrheit

Entities, `WeihnachtsfeierDbContext`, Migrationen und registrierte Endpunkte bestimmen das tatsächliche Verhalten. Bei Abweichungen zuerst den Code prüfen und danach die zuständige Doku aktualisieren.

## Dokumentation pflegen

Änderungen an Datenmodell, API, Berechtigungen, Betrieb oder Produktumfang müssen im selben Änderungsschritt die betreffende Fachseite aktualisieren. Diese Übersicht und die Root-README bleiben kurz und verlinken auf die zuständigen Seiten.