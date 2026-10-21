<p align="center"><img src="assets/ReStart.png" width="96" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>Das Vollbild-Startmenü zurückholen.</strong><br>Deine Apps, dein Layout, deine eigenen Live-Kacheln.</p>

<!-- languages:start -->
[English](README.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md)
<!-- languages:end -->

> **KI-Übersetzung:** Dieser Leitfaden wurde mithilfe von KI aus dem Englischen übersetzt und kann Fehler enthalten. Die [englische README](README.md) ist die maßgebliche Referenz mit weiteren Beispielen. Korrekturen sind willkommen. Die Screenshots verwenden eine englische Demo mit Beispieldaten.

![ReStart mit Apps, Ordnern und Live-Kacheln](docs/images/start.png)

Vermisst du das Vollbild-Startmenü von Windows 10? ReStart gibt deinen Apps unter Windows 11 und Windows 10 ein vertrautes Zuhause. Hefte häufig verwendete Inhalte an, ordne den Bildschirm frei an und ergänze Live-Kacheln, die du in JavaScript schreiben kannst.

**Portabel · JavaScript-Live-Kacheln · x64 und ARM64 · Acht Sprachen · Keine separate .NET-Installation**

## Schnellstart

1. Lade das **[x64-ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip)** für Intel-/AMD-PCs oder das **[ARM64-ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip)** für Windows auf ARM herunter.
2. Entpacke das **gesamte ZIP**. Belasse `TileHost`, `Examples` und `third-party` neben `ReStart.exe`.
3. Starte **ReStart.exe**. Drücke die **Windows-Taste** und tippe einen App-Namen. Auch **Strg+Alt+Leertaste** und das Infobereich-Menü öffnen Start.

Über das Zahnrad wählst du Design, Autostart, Windows-Tasten-/Startschaltflächen-Integration und Updates. Für die normale Nutzung sind keine Administratorrechte nötig. [SHA-256-Prüfsummen](https://github.com/LastResolution/ReStart/releases/latest) stehen bereit.

## Funktionen

![Öffnen und Schließen von ReStart](docs/images/start-motion.gif)

| Bereich | Enthalten |
| --- | --- |
| Öffnen und Schließen | Optionale Windows-Taste und Startschaltfläche, Strg+Alt+Leertaste, Infobereich, Vordergrundaktivierung, Schließen beim App-/Taskleistenwechsel, monitorabhängiger Arbeitsbereich, nur eine Instanz |
| Start | Optionaler Autostart bei der Anmeldung, Hintergrundmodus, themenabhängige Ladeanzeige mit Eingabepuffer, Icon-Cache und Katalogaktualisierung |
| Kacheln und Gruppen | Vier Größen, unabhängige Kacheln, bewegliche rechteckige Gruppen mit inhaltsabhängigen Abmessungen, horizontale Halbrasterpositionen, automatische Platzierung und Scrollen auf beiden Achsen |
| Ordner | Durch Überlagern innerhalb einer Gruppe erstellen, direkt im Layout öffnen, Kinder hinzufügen/entfernen/verschieben, inline umbenennen und auflösen; ein einzelnes Kind bleibt im Ordner |
| Layout bearbeiten | Bearbeitungsmodus, Mehrfachauswahl, gemeinsames Verschieben und Rückgängig/Wiederholen pro Sitzung; Abbrechen erhält das Layout |
| Ziehfeedback | Unterscheidbare Ziele für Gruppen und Ordner, Wartefortschritt, optionale Hinweise und Gruppengrenzen, automatisches Scrollen am Rand; Gruppierung und Ordnererstellung abschaltbar |
| Anheften | Apps, Dateien, Verzeichnisse, Verknüpfungen und Websites; Explorer-Befehl **Pin to ReStart**, Kontextmenüs, Ziehen aus der App-Liste und explizite Ziele |
| Suche | Normalisierte App-Namen, 15 übersetzte Windows-Einstellungsseiten mit englischen Suchbegriffen, optionale indizierte lokale Dateinamen, Pfeiltasten/Enter und Verwerfen veralteter Suchanfragen |
| Alle Apps | Responsive alphabetische Spalten, aufklappbare Startmenüordner, Buchstabenauswahl, Tastaturbedienung, Aktualisierung installierter/entfernter Apps, zuletzt hinzugefügte und optional häufig genutzte Apps |
| App-Befehle | Verfügbare Windows-Aktionen für Administratorstart, Dateispeicherort, Eigenschaften und Deinstallation; optionale zuletzt/häufig verwendete Dokumente gemäß Windows-Einstellungen und API-Verfügbarkeit |
| Startoptionen | Pro Kachel aktivierbare Argumente, Arbeitsverzeichnis und Administratorstart für unterstützte Desktop-Apps/Verknüpfungen; Windows verwaltet UAC |
| Navigation | Optionales Ausklappen bei Mausberührung, einzeln wählbare Dokumente/Downloads/Musik/Bilder/Videos/Netzwerk/persönlicher Ordner, Konto- und Energieaktionen |
| Darstellung | Fünf Farbfamilien mit hellen/dunklen Varianten, Systemmodus, eigene RGB-Hintergrund-/Akzentfarben, Hintergrundbild, Start-Färbung, individuelle Kachelfarben, passende Steuerelemente/Scrollleisten, hoher Kontrast, optionale Animation |
| Live-Kacheln | 15 JavaScript-Beispiele, vier responsive Größen, lokale Klickaktionen, Live-Updates ein/aus, Datenkonfiguration, Vorlagenerstellung, Paketimport und automatisch beobachtete Bibliothek |
| Kachel-APIs | PC-Zeit, Sprache und Größe, gespeicherter Laufzeitzustand, CPU/RAM/Netzwerk/Akku/Speicher, ausgewählte Fotos, zwischengespeicherte Web-API-/JSON-/RSS-/Atom-/Dateidaten und lokale App-Integration |
| Sprachen | Englisch, Japanisch, Deutsch, Französisch, Spanisch, Koreanisch, vereinfachtes und traditionelles Chinesisch; auch Texte der mitgelieferten Kacheln |
| Daten und Updates | Lokale Einstellungen, Layoutimport/-export/-reset, Sicherungen/Migration, löschbare App-Nutzungsdaten, Version/Build/Lizenzen, Updateprüfung ohne API-Schlüssel und manuelle Installation |
| Verteilung | Portable Windows-10/11-Pakete für x64 und ARM64, Laufzeit und Lizenzhinweise enthalten; reversible Integration ohne Explorer-Patches |

[Schalter und Standardwerte (Englisch)](docs/start-behavior.md) · [Kompatibilität (Englisch)](docs/fidelity.md)

## Suchen und konfigurieren

![Apps, Windows-Einstellungen und lokale Dateien suchen](docs/images/unified-search.png)

Unter **Settings → Start behavior** lassen sich Einstellungs-/Dateisuche, Dokumentmenüs, Hover-Effekte, automatische Gruppen/Ordner, Randscrollen und Ziehhinweise einzeln deaktivieren. Die Erfassung häufig genutzter Apps ist standardmäßig aus.

| Verhalten | Startoptionen pro Kachel |
| --- | --- |
| ![Verhaltensschalter](docs/images/start-behavior.png) | ![Argumente, Arbeitsverzeichnis und Administratorstart](docs/images/launch-options.png) |

**Use custom launch options** lässt sich ausschalten, ohne die eingegebenen Werte zu löschen. Verknüpfungen behalten ohne diese Option ihre normalen Windows-Startvorgaben.

## Live-Kacheln und eigene Daten

![Mitgelieferte Live-Kacheln](docs/images/live-tiles.png)

- **Zeit und Fokus:** Orbit clock, Digital clock, Month dots, Focus cycle, Year in motion und Countdown.
- **PC:** CPU pulse, Memory flow, Network flow, Battery und Storage.
- **Fotos:** Photo moments mit selbst gewähltem Ordner und Klick zum nächsten Bild.
- **Verbindungen:** Weather, Feed headlines und JSON monitor.

Rechtsklick auf den Hintergrund → **Add live tile**. Klicks bedienen die Kachel lokal, etwa Pause oder nächstes Bild. Für Weather/Feed/JSON öffnest du **Configure data sources…**, trägst URL oder Datei ein und speicherst. **Refresh data now** fordert neue Daten an.

**Das Wetter startet mit Berlin als Beispiel, nicht mit deinem Standort.** Es gibt keine Standortermittlung. Ändere Koordinaten und Ortsnamen wie im [Wetter-Leitfaden](docs/javascript-tiles.md#weather-location) beschrieben. Zugangsdaten werden für deinen Windows-Benutzer verschlüsselt und nicht mit dem Layout exportiert. Fehler können mit zwischengespeicherten Werten überbrückt werden; beim Ausblenden stoppt das Lesen.

Eine eigene Kachel besteht aus `manifest.json` und `tile.js`. `render(ctx)` bekommt einen begrenzten Daten-Snapshot und liefert native Anzeigedaten zurück, einschließlich größenabhängiger Layouts. JavaScript erhält keinen allgemeinen Datei-, Netzwerk- oder CLR-Zugriff. Lege Paketordner in `%LOCALAPPDATA%\ReStart\TileLibrary`; ReStart erkennt sie automatisch. [Beispiele](examples/tiles) · [JavaScript-API (Englisch)](docs/javascript-tiles.md) · [Lokaler Datenproduzent](examples/integrations/write-tile-data.ps1)

## Bedienung

| Aktion | Ergebnis |
| --- | --- |
| Windows-Taste / `Ctrl+Alt+Space` | Öffnen oder schließen |
| Tippen / `Ctrl+F` | Suchen; Enter startet den markierten Treffer, Pfeile ändern die Auswahl |
| `Esc` | Suche, Ordner oder Overlay schließen, danach Start |
| Kachel / Gruppenüberschrift ziehen | Verschieben; leere Fläche erzeugt eine unabhängige Kachel |
| Unabhängige Kachel etwa 0,3 s überlagern | Nach der Vorschau eine normale Gruppe erstellen |
| Kachel in einer Gruppe etwa 0,6 s überlagern | Nach der Vorschau einen Ordner erstellen; bestehende Ordner nehmen Kacheln auf |
| **Edit layout** / `Ctrl+E` | Mehrfachauswahl ohne versehentliche App-Starts |
| `Ctrl+A` / `Delete` im Bearbeitungsmodus | Hauptkacheln auswählen / Auswahl lösen |
| `Ctrl+Z` / `Ctrl+Y` oder `Ctrl+Shift+Z` | Layoutänderung rückgängig / wiederholen |
| Stift / Doppelklick auf Gruppen-/Ordnernamen | Direkt umbenennen |
| `Shift` + Mausrad | Horizontal scrollen |

Windows-Tastenkombinationen werden an Windows weitergegeben. Rückgängig/Wiederholen behält bis zu 100 Änderungen innerhalb von 32 MiB während der Sitzung; Textfelder verwenden ihre eigene Textbearbeitung. Erneutes Öffnen kehrt zum angehefteten Startbildschirm zurück.

## Einstellungen, Datenschutz und Updates

Einstellungen: `%LOCALAPPDATA%\ReStart\layout.json`. Die optionale App-Aktivität liegt getrennt in `activity.json` und lässt sich löschen. Sicherungen schützen Migrationen; [Details (Englisch)](docs/migration.md). Unter About stehen Version, Build und Lizenzen.

Updates lesen öffentliche GitHub Releases ohne API-Schlüssel. Beende ReStart und ersetze die vollständige entpackte Installation manuell. Bei einem neuen Speicherort speicherst du Autostart und Explorer-Integration erneut. Es gibt keine Werbung oder Nutzungsanalyse; App-Listen, Layouts und Fotoauswahl werden nicht hochgeladen. Konfigurierte Datenanbieter erhalten ihre jeweiligen Anfragen. [Sicherheit (Englisch)](SECURITY.md)

Zum Entfernen Autostart und Explorer-Integration deaktivieren, über den Infobereich beenden und den Installationsordner löschen. Lokale Daten nur löschen, wenn auch Einstellungen und eigene Kacheln entfernt werden sollen.

## Selbst bauen

Unter Windows das stabile [.NET-10-SDK](https://dotnet.microsoft.com/download/dotnet/10.0) aus `global.json` verwenden. Eine laufende App vor dem Überschreiben ihrer Dateien beenden.

```powershell
git clone https://github.com/LastResolution/ReStart.git
cd ReStart
dotnet restore ReStart.slnx --locked-mode
dotnet build ReStart.slnx -c Release --no-restore
dotnet test ReStart.slnx -c Release --no-build
.\src\ReStart.App\bin\Release\net10.0-windows\ReStart.exe
# Isolierte Vorschau:
dotnet run --project src/ReStart.App -c Release -- --preview
# Portables Paket, Runtime enthalten:
./scripts/package.ps1 -Runtime win-x64
```

Für ARM64 `win-arm64` verwenden. ZIPs und Prüfsummen entstehen in `artifacts/packages`. Immer das ganze ZIP verteilen.

[Fehler/Ideen](https://github.com/LastResolution/ReStart/issues/new/choose) · [Mitwirken (Englisch)](CONTRIBUTING.md) · [Architektur (Englisch)](docs/architecture.md) · [Tests (Englisch)](docs/testing.md) · [Releases](https://github.com/LastResolution/ReStart/releases)

ReStart ist ein unabhängiges, von Microsoft nicht unterstütztes MIT-Projekt, inspiriert vom späteren Windows-10-Vollbild-Startmenü. Screenshots und Animation zeigen die echte App mit einer englischen Demo, synthetischen Messwerten und erzeugten Fotofarben. Namen und Icons gehören ihren Rechteinhabern. [MIT-Lizenz](LICENSE) · [Support (Englisch)](SUPPORT.md)
