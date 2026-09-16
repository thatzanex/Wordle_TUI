## 1. Überblick

**WordleApp** ist ein Klon des Spiels *Wordle*, gebaut als **Terminal User Interface
(TUI)** in **C# / .NET 8**. Es läuft komplett in der Konsole, sieht aber dank der
Bibliothek [`Spectre.Console`](https://spectreconsole.net/) sehr gepflegt aus: großer
ASCII-Schriftzug ("Figlet"), abgerundete Panels, farbige Kacheln, eine Ladeanimation
und eine Bildschirm-Tastatur, die den Zustand der Buchstaben anzeigt.

Wichtigste Eigenschaften:

- **Zwei Sprachen** (Deutsch und Englisch), beliebig erweiterbar über JSON-Dateien.
- **Drei Schwierigkeitsgrade** (Easy / Normal / Hard) mit unterschiedlichen Regeln
  und Punktemultiplikatoren.
- **Externe Wortquelle**: Zielwörter werden über eine Zufallswort-API geladen,
  Rateversuche optional gegen ein Online-Wörterbuch geprüft.
- **Persistente Einstellungen und Bestenliste** in `config.json` bzw.
  `leaderboard.json`, die neben der `.exe` liegen.
- **Adaptive Darstellung**: Das Layout (Kachelgröße, ASCII-Banner, Bildschirmtastatur)
  passt sich in Echtzeit an die Fenstergröße des Terminals an.

Das Projekt wurde ursprünglich anhand eines Planungsdokuments (`Planning/prompt.md`)
entwickelt, das die Architektur- und Stilvorgaben festlegt (siehe
[Architektur & Design-Prinzipien](architecture.md)).

---

## 2. Schnellstart

Voraussetzung: **.NET 8 SDK**.

```bash
cd WordleApp
dotnet run
```

Der erste Start fragt nach einem Spielernamen, danach erscheint das Hauptmenü.
Steuerung überall: **Pfeiltasten** (oder W/A/S/D) zum Navigieren, **Enter/Leertaste**
zum Bestätigen, **Escape** zum Zurückgehen.

Zum Veröffentlichen als eigenständige `.exe`:

```bash
dotnet publish -c Release
```

Wichtig: `config.json` und die Sprachdateien unter `Resources/Languages/*.json`
werden laut `WordleApp.csproj` automatisch mit in das Ausgabeverzeichnis kopiert
(`CopyToOutputDirectory`). Ohne diese Dateien direkt neben der `.exe` startet das
Spiel zwar trotzdem (Fallback auf Standardwerte), aber ohne die gewünschten
Übersetzungen bzw. Einstellungen.

---

