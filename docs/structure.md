## 3. Projektstruktur

```
Wordle/                            (Repository-Root)
├── Planning/
│   └── prompt.md                  Ursprüngliches Planungs-/Anforderungsdokument
└── WordleApp/                     Das eigentliche .NET-Projekt
    ├── Program.cs                 Einstiegspunkt (Main)
    ├── WordleApp.csproj           Projektdatei (SDK, Pakete, Dateien zum Kopieren)
    ├── WordleApp.sln              Visual-Studio-Solution
    ├── config.json                Standardkonfiguration (wird zur Laufzeit editiert)
    │
    ├── Core/                      Spiellogik & Anwendungssteuerung (kein Rendering!)
    │   ├── AppShell.cs            Hauptmenü, Bildschirmnavigation, Name-Eingabe
    │   ├── WordleGame.cs          Die eigentliche Spiel-Engine (eine Runde)
    │   ├── WordEvaluator.cs       Vergleich Guess ↔ Zielwort (grün/gelb/grau)
    │   ├── ScoreCalculator.cs     Punkteberechnung
    │   ├── SettingsEditor.cs      Zustand & Logik des Einstellungsbildschirms
    │   └── InputReader.cs         Zentrale Tastatur-Poll-Schleife
    │
    ├── Models/                    Reine Datenklassen (keine Logik, keine I/O)
    │   ├── GameConfig.cs          Abbild von config.json
    │   ├── Difficulty.cs          Schwierigkeitsgrade + Regeltabelle
    │   ├── LetterState.cs         Enum: Empty / Absent / Present / Correct
    │   ├── WordleGuess.cs         Ein abgegebener Rateversuch + Bewertung
    │   ├── BoardView.cs           Schnappschuss einer laufenden Runde (fürs Rendering)
    │   ├── RoundResult.cs         Ergebnis einer abgeschlossenen Runde
    │   ├── LeaderboardEntry.cs    Ein Eintrag der Bestenliste
    │   └── SettingsRow.cs         Eine Zeile des Einstellungsbildschirms
    │
    ├── Services/                  Externe Anbindungen & Persistenz
    │   ├── ConfigService.cs       Laden/Speichern von config.json
    │   ├── LocalizationService.cs Laden der Übersetzungsdateien, Textauflösung
    │   ├── DictionaryApiService.cs HTTP-Zugriff auf Wort- und Wörterbuch-API
    │   ├── WordPool.cs            Zwischenspeicher/Puffer für Zielwörter
    │   └── LeaderboardService.cs  Laden/Speichern von leaderboard.json
    │
    ├── UI/                        Die einzige Schicht, die die Konsole beschreibt
    │   ├── ConsoleSession.cs      Terminal vorbereiten/zurücksetzen (IDisposable)
    │   └── FrontendRenderer.cs    Zeichnet sämtliche Bildschirme mit Spectre.Console
    │
    └── Resources/Languages/
        ├── de.json                 Deutsche Übersetzung (Startsprache)
        └── en.json                 Englische Übersetzung (Fallback-Sprache)
```

Zur Laufzeit kommen im Ausgabeverzeichnis noch hinzu:
- `leaderboard.json` – wird beim ersten Speichern eines Ergebnisses automatisch angelegt.

---

