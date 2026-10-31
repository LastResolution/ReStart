<p align="center"><img src="assets/ReStart.png" width="96" alt="ReStart" /></p>
<h1 align="center">ReStart</h1>
<p align="center"><strong>Das Vollbild-Startmenü zurückholen.</strong><br>Deine Apps, dein Layout, deine eigenen Live-Kacheln.</p>

<!-- languages:start -->
<p align="center">
  <a href="README.md">English</a> · <a href="README.de.md">Deutsch</a> · <a href="README.es.md">Español</a> · <a href="README.fr.md">Français</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a>
</p>
<!-- languages:end -->

<p align="center"><a href="https://lastresolution.github.io/ReStart/">Website</a> · <a href="https://lastresolution.github.io/ReStart/developers/">Entwicklerleitfaden (Englisch)</a></p>

> **KI-Übersetzung:** Dieser Leitfaden wurde mithilfe von KI aus dem Englischen übersetzt und kann Fehler enthalten. Die [englische README](README.md) ist die maßgebliche Referenz mit weiteren Beispielen. Korrekturen sind willkommen. Die Screenshots verwenden eine englische Demo mit Beispieldaten.

<p align="center"><strong>60-Sekunden-Demo ansehen</strong><br><sub>1080p · Englisch · Ohne Ton</sub></p>

https://github.com/user-attachments/assets/a893cd20-8c82-4c47-b5fb-bf22743f67e1

Vermisst du das Vollbild-Startmenü von Windows 10? ReStart gibt deinen Apps unter Windows 11 und Windows 10 ein vertrautes Zuhause. Hefte häufig verwendete Inhalte an, ordne den Bildschirm frei an und ergänze Live-Kacheln, die du in JavaScript schreiben kannst.

**Portabel · JavaScript-Live-Kacheln · x64 und ARM64 · Acht Sprachen · Keine separate .NET-Installation**

## Zuverlässigkeit und private Sicherungen

Live-Kacheln außerhalb des sichtbaren Bereichs pausieren; Browserfehler werden mit begrenzten Wiederholungen behandelt. Beide Funktionen sind einzeln abschaltbar. Unter **Einstellungen → Layout und Daten** sichern und restaurieren Sie Layout, Darstellung, installierte Kacheln, Entwürfe und Hintergründe zusammen. Verbindungen, Zugangsdaten, Fotoordnerpfade und Caches sind ausgeschlossen. App-Namen, Startpfade und eigener Code bleiben private Daten. [Anleitung auf Englisch](docs/backups.md).

## Mehr Kontrolle. Mehr Raum zum Gestalten.

Sperre das gesamte Layout oder einzelne Gruppen. Strg+Umschalt+L schaltet die globale Sperre um. Strg+E wählt Kacheln; Strg+Pfeil verschiebt sie, Strg+Umschalt+Pfeil die Gruppe und Alt+1–4 ändert die Größe. Strg+G gruppiert eine Auswahl; Umschalt+F10 öffnet ihr Menü. Rückgängig/Wiederholen bleibt verfügbar. Ein eigenes Hintergrundbild kann unabhängig vom Desktop gespeichert werden.

Unter **Einstellungen → Darstellung** lassen sich die Hintergrundtönung auf **10–100 %** und die Unschärfe des Hintergrundbilds auf **0–50** einstellen. Der Standardwert ist **12**; **0** zeigt das Bild scharf. Hoher Kontrast hat Vorrang. **Auswahl beenden** oder ein Klick auf **Angeheftete Kacheln** führt direkt zur Kachelansicht zurück.

Darstellung, Kachelskalierung, Gruppenbreite und Sprache lassen sich vor dem Speichern ausprobieren. Beim Schließen stehen Speichern, Verwerfen oder Weiterbearbeiten zur Wahl; Verwerfen stellt die gespeicherte Ansicht wieder her. Nach einem ausdrücklich gestarteten Update-Download bleibt eine Neustart-Erinnerung sichtbar. Das geprüfte Paket wird beim nächsten Start wiedererkannt, jedoch nicht automatisch installiert.

19 HTML/CSS/JavaScript-Kacheln sind enthalten. Tile Studio bearbeitet Quellen und Manifest, zeigt vier Größen mit Offline-Beispieldaten und speichert Entwürfe oder angeheftete Kacheln. Neu sind Wettergrafik, Systemübersicht, Nachrichtenwand und Canvas-Garten; die SVG-Uhr folgt der PC-Zeit. Live-Kachelnamen sind standardmäßig sichtbar und in den Einstellungen abschaltbar.

Beim Ziehen folgt eine halbtransparente Kopie der Kachel, Gruppe oder Auswahl dem Mauszeiger. Die blaue Markierung zeigt das Ziel; verdrängte Kacheln zeigen ihre neue Position vor dem Ablegen.

![Tile Studio: HTML, CSS and JavaScript with four live previews](docs/images/tile-studio.png)

![Weather scene, System dashboard, News board and Canvas garden](docs/images/web-examples.png)

**Editor-Steuerung:** Syntaxhervorhebung für JavaScript, HTML, CSS und JSON; **Tab / Umschalt+Tab** zum Einrücken; **Format document / Strg+Umschalt+F** zur Offline-Formatierung mit einem Rückgängig-Schritt. Feed headlines und News board scrollen innerhalb ihrer Kachel. Geschlossene Ordner zeigen gleich große Live-Vorschauen. Neue und zurückgesetzte Layouts nutzen Month dots und Orbit clock. Optionale Startprüfungen melden neue Versionen mit einem ausblendbaren Hinweis.

## Schnellstart

1. Lade das **[x64-ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-x64.zip)** für Intel-/AMD-PCs oder das **[ARM64-ZIP](https://github.com/LastResolution/ReStart/releases/latest/download/ReStart-win-arm64.zip)** für Windows auf ARM herunter.
2. Entpacke das **gesamte ZIP**. Belasse `TileHost`, `Examples` und `third-party` neben `ReStart.exe`.
3. Starte **ReStart.exe**. Drücke die **Windows-Taste** und tippe einen App-Namen. Auch **Strg+Alt+Leertaste** und das Infobereich-Menü öffnen Start.

Über das Zahnrad wählst du Design, Autostart, Windows-Tasten-/Startschaltflächen-Integration und Updates. Für die normale Nutzung sind keine Administratorrechte nötig. [SHA-256-Prüfsummen](https://github.com/LastResolution/ReStart/releases/latest) stehen bereit.

## Windows-App-Integration

Die Suche ergänzt Windows-eigene Einstellungsbegriffe und Systemsteuerungseinträge. Optionale Advanced Query Syntax bietet native Dateifilter und Relevanzsortierung; **In Windows suchen** übergibt weitere Quellen ausdrücklich an Windows.

Zwei getrennte Optionen sind zunächst aus: vorhandene Windows-App-Nutzungszahlen für Meistverwendet und schreibgeschützte Windows-Kachel-/Abzeichenkompatibilität. Häufige Größenlayouts, lokale Rasterbilder und Datenbindungen werden unterstützt. Eingestellte Push-Dienste werden nicht wiederhergestellt; ohne Daten bleibt das App-Symbol sichtbar.

[Einstellungen und Kompatibilität](docs/start-behavior.md) · [Abdeckung](docs/fidelity.md)

## Funktionen

![Öffnen und Schließen von ReStart](docs/images/start-motion.gif)

**Standardmäßig Gruppen, auf Wunsch freie Platzierung.** Leere Flächen erstellen eine unbenannte Gruppe; über einer Kachel warten erstellt einen Ordner. <kbd>Shift</kbd> beim Ziehen ordnet ohne Ordner um; <kbd>Alt</kbd> platziert auf leerer Fläche unabhängig. **Use free tile placement** unter Settings → Start behavior macht dies zum Standard. Vorhandene Layouts und Ordner bleiben erhalten. Nachbarn gleiten in ihre neue Position, ohne das Ziel unter dem Zeiger zu verändern.

Beim Ziehen erscheinen kurz Tastentipps am unteren Rand. Sie lassen sich in den Startverhalten-Einstellungen deaktivieren. <kbd>F1</kbd> oder **Tastenkombinationen** in diesen Einstellungen öffnet die vollständige Übersicht.


| Bereich | Enthalten |
| --- | --- |
| Öffnen und Schließen | Optionale Windows-Taste und Startschaltfläche, Strg+Alt+Leertaste, Infobereich, Vordergrundaktivierung, Schließen beim App-/Taskleistenwechsel, monitorabhängiger Arbeitsbereich, nur eine Instanz |
| Start | Optionaler Autostart bei der Anmeldung, Hintergrundmodus, themenabhängige Ladeanzeige mit Eingabepuffer, Icon-Cache und Katalogaktualisierung |
| Kacheln und Gruppen | Vier Größen, unabhängige Kacheln, bewegliche rechteckige Gruppen mit inhaltsabhängigen Abmessungen, horizontale Halbrasterpositionen, automatische Platzierung und Scrollen auf beiden Achsen |
| Ordner | Durch anhaltendes Überlagern erstellen, direkt im Layout öffnen, Kinder hinzufügen/entfernen/verschieben, inline umbenennen und auflösen; ein einzelnes Kind bleibt im Ordner |
| Layout bearbeiten | Bearbeitungsmodus, Mehrfachauswahl, gemeinsames Verschieben und Rückgängig/Wiederholen pro Sitzung; Abbrechen erhält das Layout |
| Ziehfeedback | Unterscheidbare Ziele für Gruppen und Ordner, Wartefortschritt, optionale Hinweise und Gruppengrenzen, automatisches Scrollen am Rand; Gruppierung und Ordnererstellung abschaltbar |
| Anheften | Apps, Dateien, Verzeichnisse, Verknüpfungen und Websites; Explorer-Befehl **Pin to ReStart**, Kontextmenüs, Ziehen aus der App-Liste und explizite Ziele |
| Suche | Normalisierte App-Namen, ausführbare Dateinamen und OS-Aliase; 63 übersetzte Windows-Einstellungsseiten mit englischen Suchbegriffen, optionale indizierte Dateinamen und zuschaltbare indizierte Dokumentinhalte (bis zu 50 lokale Treffer), Pfeiltasten/Enter und Verwerfen veralteter Anfragen |
| Windows-Sitzung | Benutzerwechsel über die Windows-Anmeldung; Ruhezustand/Energiesparen nach Geräteunterstützung und Richtlinien, Herunterfahren/Neustart und Windows-Update-Aktionen bei ausstehendem Neustart |
| Alle Apps | Responsive Spalten mit sprachabhängigen Windows-Rubriken (Kana, Hangul, Pinyin und Strichzahlen), aufklappbare Startmenüordner, scrollbare Buchstabenauswahl, Unicode-Anfangszeichen zur Navigation, Aktualisierung installierter/entfernter Apps, zuletzt hinzugefügte und optional häufig genutzte Apps |
| App-Befehle | Kontextabhängige Menüs: Start-Aktionen unter Mehr, direkte Suchaktionen, Datei-/Ordnerspeicherort, App-Verwaltung/Bewertungen und verfügbare Windows-Befehle. Optionale Dokumentlisten bieten eigene Aktionen; App-Verlaufseinträge lassen sich ausblenden und Navigationslisten anpassen. [Menüleitfaden und Windows-10-Abdeckung](docs/context-menus.md) Windows-Sprunglisten unterstützen gemeinsame Pins und registrierte Anwendungsaufgaben. Verlauf, Aufgaben und angeheftete Ziele lassen sich unabhängig anzeigen oder ausblenden. Dateifreigabe gilt für zugängliche Dateien auf unterstützten Windows-Systemen; Eigenschaften erfordern eine vorhandene Datei, einen Ordner oder eine ursprüngliche Verknüpfung. Nicht verfügbare Aktionen werden ausgeblendet. Anwendungsspezifische Sprunglistenkategorien behalten ihre registrierten Namen und Aktionen. |
| Startoptionen | Pro Kachel aktivierbare Argumente, Arbeitsverzeichnis und Administratorstart für unterstützte Desktop-Apps/Verknüpfungen; Windows verwaltet UAC |
| Navigation | Optionales Ausklappen bei Mausberührung, einzeln wählbare Dokumente/Downloads/Musik/Bilder/Videos/Netzwerk/persönlicher Ordner, Konto- und Energieaktionen |
| Darstellung | Fünf Farbfamilien mit hellen/dunklen Varianten, Systemmodus, eigene RGB-Hintergrund-/Akzentfarben, Hintergrundbild, Start-Färbung, individuelle Kachelfarben, passende Steuerelemente/Scrollleisten, hoher Kontrast, optionale Animation |
| Live-Kacheln | 19 JavaScript-Beispiele, vier responsive Größen, lokale Klickaktionen, Live-Updates ein/aus, Datenkonfiguration, Vorlagenerstellung, Paketimport und automatisch beobachtete Bibliothek |
| Kachel-APIs | PC-Zeit, Sprache und Größe, gespeicherter Laufzeitzustand, CPU/RAM/Netzwerk/Akku/Speicher, ausgewählte Fotos, zwischengespeicherte Web-API-/JSON-/RSS-/Atom-/Dateidaten und lokale App-Integration |
| Sprachen | Englisch, Japanisch, Deutsch, Französisch, Spanisch, Koreanisch, vereinfachtes und traditionelles Chinesisch; auch Texte der mitgelieferten Kacheln |
| Daten und Updates | Lokale Einstellungen, Layoutimport/-export/-reset, Sicherungen/Migration, löschbare App-Nutzungsdaten, Version/Build/Lizenzen, Updateprüfung ohne API-Schlüssel, geprüfte portable Updates mit Wiederherstellung und Diagnosevorschau |
| Verteilung | Portable Windows-10/11-Pakete für x64 und ARM64, Laufzeit und Lizenzhinweise enthalten; reversible Integration ohne Explorer-Patches |

[Schalter und Standardwerte (Englisch)](docs/start-behavior.md) · [Kompatibilität (Englisch)](docs/fidelity.md)

**Ein nützliches Startlayout.** Beim ersten Start und Zurücksetzen werden installierte Apps nach Aufgabe anhand ihrer IDs und exakten übersetzten Namen ausgewählt. Unterschiedliche mitgelieferte Offline-Kacheln ergänzen dünn besetzte Gruppen auf mindestens sechs Kacheln. Gespeicherte Layouts bleiben erhalten. [Auswahl und Zurücksetzen (Englisch)](docs/start-behavior.md#initial-layout-and-reset).

**Gemessene Plattformabdeckung.** Native Tests prüfen die Fensterplatzierung auf zwei Bildschirmen mit 100 %/125 %/150 % Skalierung, auch mit gemischtem Hoch- und Querformat. Die Barrierefreiheits-Schnittstellen werden in acht Sprachen und beiden Designs geprüft; grundlegende Narrator-Ansagen wurden über die Sprachzusammenfassung kontrolliert. Erneutes Anschließen, physische Touch-Eingabe und vollständige Screenreader-Abläufe erfordern weitere Gerätetests. [Ergebnisse und Geräteabdeckung (Englisch)](docs/fidelity.md#observed-platform-coverage).

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

Webkacheln bestehen aus manifest.json, index.html, tile.css und tile.js sowie optionalen assets. HTML/CSS, SVG, Canvas und transparente Flächen bleiben innerhalb der vier Größen frei gestaltbar. Native JS-Pakete werden weiter unterstützt. WebView2 Runtime ist erforderlich. Direkter Netzwerkzugriff braucht eine deklarierte Fähigkeit und eine separate Erlaubnis; allgemeine Datei-, CLR- und Shell-Brücken fehlen. [HTML/CSS/JavaScript API](docs/javascript-tiles.md) · [Examples](examples/tiles)

## Bedienung

| Aktion | Ergebnis |
| --- | --- |
| Windows-Taste / <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>Space</kbd> | Öffnen oder schließen |
| Tippen / <kbd>Ctrl</kbd> + <kbd>F</kbd> | Suchen; Enter startet den markierten Treffer, Pfeile ändern die Auswahl |
| <kbd>Esc</kbd> | Suche, Ordner oder Overlay schließen, danach Start |
| Kachel / Gruppenüberschrift ziehen | Verschieben; leere Fläche erstellt eine unbenannte Gruppe |
| <kbd>Alt</kbd> + Ziehen auf leere Fläche | Für diesen Zug unabhängig platzieren |
| <kbd>Shift</kbd> + Ziehen | Nachbarn verschieben, ohne Überlappungsgruppen oder Ordneraktionen |
| Eine Kachel etwa 0,6 s überlagern | Nach der Vorschau einen Ordner erstellen; bestehende Ordner nehmen Kacheln auf |
| **Kacheln auswählen** in der Seitenleiste/im Hintergrundmenü / <kbd>Strg</kbd> + <kbd>E</kbd> | Mehrfachauswahl ohne App-Starts; direktes Ziehen ist jederzeit möglich |
| <kbd>Ctrl</kbd> + <kbd>A</kbd> / <kbd>Delete</kbd> im Bearbeitungsmodus | Hauptkacheln auswählen / Auswahl lösen |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> / <kbd>Ctrl</kbd> + <kbd>Y</kbd> oder <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Z</kbd> | Layoutänderung rückgängig / wiederholen |
| Stift / Doppelklick auf Gruppen-/Ordnernamen | Direkt umbenennen |
| <kbd>Shift</kbd> + Mausrad | Horizontal scrollen |

Windows-Tastenkombinationen werden an Windows weitergegeben. Rückgängig/Wiederholen behält bis zu 100 Änderungen innerhalb von 32 MiB während der Sitzung; Textfelder verwenden ihre eigene Textbearbeitung. Erneutes Öffnen kehrt zum angehefteten Startbildschirm zurück.

## Einstellungen, Datenschutz und Updates

Einstellungen: `%LOCALAPPDATA%\ReStart\layout.json`. Die optionale App-Aktivität liegt getrennt in `activity.json` und lässt sich löschen. Sicherungen schützen Migrationen; [Details (Englisch)](docs/migration.md). Unter About stehen Version, Build und Lizenzen.

Updates lesen öffentliche GitHub Releases ohne API-Schlüssel. Wähle unter Einstellungen → Updates zuerst **Update herunterladen**, dann **Neu starten und aktualisieren**. Vollständige, unveränderte portable Pakete in beschreibbaren Ordnern unterstützen geprüfte Updates mit Wiederherstellung bei Installations- oder Startfehlern. Entwicklerbuilds und ältere Pakete lassen sich weiterhin manuell ersetzen. Unter Info → Diagnoseinformationen kannst du einen Bericht ohne Pfade, App-Listen, Kacheldaten, Zugangsdaten oder Rohprotokolle prüfen und kopieren. Es wird nichts automatisch hochgeladen. [Updates und Diagnose (Englisch)](docs/updates.md). Bei einem neuen Speicherort speicherst du Autostart und Explorer-Integration erneut. Es gibt keine Werbung oder Nutzungsanalyse; App-Listen, Layouts und Fotoauswahl werden nicht hochgeladen. Konfigurierte Datenanbieter erhalten ihre jeweiligen Anfragen. [Sicherheit (Englisch)](SECURITY.md)

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

[Fragen und Beispiele](https://github.com/LastResolution/ReStart/discussions) · [Fehler melden](https://github.com/LastResolution/ReStart/issues/new/choose) · [Mitwirken (Englisch)](CONTRIBUTING.md) · [Architektur (Englisch)](docs/architecture.md) · [Tests (Englisch)](docs/testing.md) · [Releases](https://github.com/LastResolution/ReStart/releases)

ReStart ist ein unabhängiges, von Microsoft nicht unterstütztes MIT-Projekt, inspiriert vom späteren Windows-10-Vollbild-Startmenü. Screenshots und Animation zeigen die echte App mit einer englischen Demo, synthetischen Messwerten und erzeugten Fotofarben. Namen und Icons gehören ihren Rechteinhabern. [MIT-Lizenz](LICENSE) · [Drittanbieterhinweise (Englisch)](docs/third-party.md) · [Support (Englisch)](SUPPORT.md)

---

<!-- support:start -->
<h2 align="center">Unabhängige Entwicklung unterstützen</h2>
<p align="center">
  Gefällt dir ReStart? Unterstütze LastResolution dabei, nützliche Werkzeuge zu entwickeln und zu verbessern: ReStart und künftige Projekte.<br>
  <sub>ReStart ist kostenlos und Open Source. Deine Unterstützung ist freiwillig.</sub>
</p>
<p align="center">
  <a href="https://buymeacoffee.com/LastResolution"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" height="48" alt="Buy Me a Coffee · LastResolution" /></a>
</p>
<!-- support:end -->
