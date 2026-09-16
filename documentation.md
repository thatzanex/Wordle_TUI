# WordleApp – Projektdokumentation

Eine vollständige Dokumentation der WordleApp: Aufbau, Architektur, Datenflüsse und
Erklärungen zu allen Klassen und den kniffligeren Codestellen. Diese Dokumentation
richtet sich an jemanden, der das Projekt noch nie gesehen hat, und soll genügen, um
selbstständig damit weiterzuarbeiten.

---

## Index

1. [Überblick](#1-überblick)
2. [Schnellstart](#2-schnellstart)
3. [Projektstruktur](#3-projektstruktur)
4. [Architektur & Design-Prinzipien](#4-architektur--design-prinzipien)
5. [Der Programmablauf (Lifecycle)](#5-der-programmablauf-lifecycle)
6. [Models (`Models/`)](#6-models-models)
   - 6.1 [`GameConfig.cs`](#61-gameconfigcs)
   - 6.2 [`Difficulty.cs`](#62-difficultycs)
   - 6.3 [`LetterState.cs`](#63-letterstatecs)
   - 6.4 [`WordleGuess.cs`](#64-wordleguesscs)
   - 6.5 [`BoardView.cs`](#65-boardviewcs)
   - 6.6 [`RoundResult.cs`](#66-roundresultcs)
   - 6.7 [`LeaderboardEntry.cs`](#67-leaderboardentrycs)
   - 6.8 [`SettingsRow.cs`](#68-settingsrowcs)
7. [Core – Spiellogik (`Core/`)](#7-core--spiellogik-core)
   - 7.1 [`WordEvaluator.cs` – Die Bewertungslogik](#71-wordevaluatorcs--die-bewertungslogik)
   - 7.2 [`WordleGame.cs` – Die Spiel-Engine](#72-wordlegamecs--die-spiel-engine)
   - 7.3 [`ScoreCalculator.cs` – Punkteberechnung](#73-scorecalculatorcs--punkteberechnung)
   - 7.4 [`AppShell.cs` – Die Anwendungshülle](#74-appshellcs--die-anwendungshülle)
   - 7.5 [`SettingsEditor.cs` – Der Einstellungs-Editor](#75-settingseditorcs--der-einstellungs-editor)
   - 7.6 [`InputReader.cs` – Tastatureingabe & Resize-Erkennung](#76-inputreadercs--tastatureingabe--resize-erkennung)
8. [Services (`Services/`)](#8-services-services)
   - 8.1 [`ConfigService.cs`](#81-configservicecs)
   - 8.2 [`LocalizationService.cs`](#82-localizationservicecs)
   - 8.3 [`DictionaryApiService.cs`](#83-dictionaryapiservicecs)
   - 8.4 [`WordPool.cs`](#84-wordpoolcs)
   - 8.5 [`LeaderboardService.cs`](#85-leaderboardservicecs)
9. [UI-Schicht (`UI/`)](#9-ui-schicht-ui)
   - 9.1 [`ConsoleSession.cs`](#91-consolesessioncs)
   - 9.2 [`FrontendRenderer.cs` – Das Herzstück der Darstellung](#92-frontendrenderercs--das-herzstück-der-darstellung)
10. [Konfigurationsdatei `config.json`](#10-konfigurationsdatei-configjson)
11. [Übersetzungen (`Resources/Languages/`)](#11-übersetzungen-resourceslanguages)
12. [Persistente Dateien zur Laufzeit](#12-persistente-dateien-zur-laufzeit)
13. [Wichtige Workflows im Detail](#13-wichtige-workflows-im-detail)
    - 13.1 [Ablauf einer Runde](#131-ablauf-einer-runde)
    - 13.2 [Resize-Handling](#132-resize-handling)
    - 13.3 [Die "Shimmer"-Animation beim Prüfen eines Wortes](#133-die-shimmer-animation-beim-prüfen-eines-wortes)
    - 13.4 [Wie eine neue Sprache hinzugefügt wird](#134-wie-eine-neue-sprache-hinzugefügt-wird)
    - 13.5 [Wie eine neue Einstellung hinzugefügt wird](#135-wie-eine-neue-einstellung-hinzugefügt-wird)
14. [Fehlerbehandlung & Robustheit](#14-fehlerbehandlung--robustheit)
15. [Bekannte Grenzen / Was fehlt](#15-bekannte-grenzen--was-fehlt)
16. [Glossar](#16-glossar)

---

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
[Abschnitt 4](#4-architektur--design-prinzipien)).

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

## 4. Architektur & Design-Prinzipien

Das Projekt folgt einer strikten **Schichtentrennung** (Separation of Concerns), die
sich an klassischer Clean-Architecture orientiert:

| Schicht      | Ordner       | Zuständigkeit                                                | Darf auf Konsole schreiben? |
|--------------|--------------|---------------------------------------------------------------|------------------------------|
| Models       | `Models/`    | Reine Datenstrukturen (records/klassen), keine Logik          | Nein |
| Core         | `Core/`      | Spielregeln, Zustandsmaschinen, Menüsteuerung                  | Nein |
| Services     | `Services/`  | Dateizugriff, HTTP-Aufrufe, Übersetzung                        | Nein |
| UI           | `UI/`        | **Einzige** Schicht, die `AnsiConsole`/`Spectre.Console` nutzt | **Ja, ausschließlich hier** |

**Kernregel** (explizit so im Planungsdokument festgehalten):
> *"Never mix console output (`AnsiConsole.Write`) with game logic. All drawing must
> happen in `FrontendRenderer.cs`."*

Das bedeutet konkret: `WordleGame`, `AppShell` und `SettingsEditor` bauen nur
Datenobjekte (z. B. `BoardView`, `SettingsRow[]`) und übergeben diese an
`FrontendRenderer`. Der Renderer selbst kennt keine Spielregeln – er weiß nicht,
*warum* eine Kachel grün ist, nur *dass* sie es ist (`LetterState.Correct`).

Weitere Prinzipien, die im Code konsequent umgesetzt sind:

- **Keine hartcodierten Texte.** Jeder sichtbare String kommt über
  `LocalizationService.Get(key)` bzw. `.Format(key, args)` aus einer JSON-Datei.
- **Keine hartcodierten Werte, wo Konfiguration sinnvoll ist.** Rundenlänge, Anzahl
  Versuche, Farben und API-Endpunkte liegen in `config.json`.
- **Grafische Degradation statt Absturz.** Fehlt `config.json`, ist sie kaputt, ist
  das Terminal zu klein, das Netzwerk nicht erreichbar, keine Farbunterstützung
  vorhanden – überall gibt es einen sinnvollen Fallback statt einer Exception.
- **Dependency Injection per Konstruktor** (kein DI-Framework, alles wird in
  `Program.cs` von Hand verdrahtet – siehe [Abschnitt 5](#5-der-programmablauf-lifecycle)).
- **`virtual`-Methoden für Testbarkeit.** `InputReader.WaitForInput()` und die
  Methoden von `DictionaryApiService` sind `virtual`, damit man sie in Tests mocken
  könnte (aktuell gibt es im Repository jedoch **keine automatisierten Tests**).

---

## 5. Der Programmablauf (Lifecycle)

Der Einstiegspunkt ist `Program.Main()` (`Program.cs`). Er verdrahtet alle Services
von Hand (kein DI-Container) und übergibt sie an `AppShell`:

```csharp
var configService = new ConfigService();
var config = configService.Load();
var localizer = new LocalizationService(config.Language);

config.Language = localizer.CurrentLanguage;   // ggf. korrigierte Sprache zurückschreiben

using var session = ConsoleSession.Start();    // Terminal vorbereiten (Cursor aus, schwarzer BG)

var renderer = new FrontendRenderer(config, localizer);
var leaderboard = new LeaderboardService();
var api = new DictionaryApiService(config.ApiEndpoints);
var words = new WordPool(api);
var input = new InputReader();

var shell = new AppShell(config, renderer, localizer, configService, leaderboard, api, words, input);
shell.Run();
```

**Wichtiges Detail:** `using var session = ConsoleSession.Start();` sorgt dafür, dass
`ConsoleSession.Dispose()` **immer** aufgerufen wird – auch bei einer unerwarteten
Exception – und damit Cursor-Sichtbarkeit und Konsolenfarben wiederhergestellt werden.
Ohne das könnte ein Absturz das Terminal des Nutzers in einem unbrauchbaren Zustand
(unsichtbarer Cursor, falsche Farben) zurücklassen.

Der gesamte weitere Ablauf liegt danach in `AppShell.Run()` (siehe
[Abschnitt 7.4](#74-appshellcs--die-anwendungshülle)).

Ablaufdiagramm auf hoher Ebene:

```
Program.Main()
   │
   ├─ ConfigService.Load()              → GameConfig
   ├─ LocalizationService(config.Language)
   ├─ ConsoleSession.Start()            → Terminal vorbereiten
   │
   └─ AppShell.Run()
        │
        ├─ (kein Keyboard? z. B. Pipe) → einen Frame zeichnen, beenden
        ├─ WaitForUsableWindow()        → Terminal ggf. zu klein → warten
        ├─ WordPool.WarmUp(...)         → Wörter im Hintergrund vorladen
        ├─ DrawLoadingScreen()          → Ladeanimation (Progress-Bars)
        ├─ AskForName() (nur beim 1. Start)
        │
        └─ RunMainMenu()  ← Endlosschleife bis ESC/Quit
             ├─ "Play"         → StartGame() → WordleGame.Run(level)
             ├─ "Leaderboard"  → DrawLeaderboard(...)
             ├─ "How to Play"  → DrawHelpScreen()
             └─ "Settings"     → RunSettingsEditor() → SettingsEditor
```

---

## 6. Models (`Models/`)

Die Models sind bewusst "dumm": keine I/O, keine Geschäftslogik außer einfachen
Ableitungen (z. B. Prozentwerte). Viele sind `readonly record struct`, also
unveränderliche Wertetypen mit automatisch generierter Gleichheit – ideal für
Schnappschüsse, die pro Frame neu gebaut werden.

### 6.1 `GameConfig.cs`

Bildet `config.json` 1:1 als C#-Objekt ab (siehe [Abschnitt 10](#10-konfigurationsdatei-configjson)).
Besteht aus vier Unterobjekten:

- `GameSettings` – `MaxAttempts` (3–10), `WordLength` (3–8)
- `ThemeSettings` – Farbnamen für die vier Kachelzustände + Titelfarbe
- `ApiSettings` – URLs, Timeouts, ob Validierung aktiv ist

Wichtige Methoden:

| Methode | Zweck |
|---|---|
| `Clamp()` | Zwingt `MaxAttempts`/`WordLength` zurück in ihre gültigen Bereiche. Wird nach jedem Laden aufgerufen, damit eine von Hand verstümmelte `config.json` nie zu einem unspielbaren Brett führt. |
| `Clone()` | Tiefe Kopie – wird von `SettingsEditor` benutzt, um vor dem Bearbeiten einen Schnappschuss ("Snapshot") zu erstellen. |
| `CopyFrom(other)` | Überschreibt **alle Felder in-place** statt das Objekt auszutauschen. Wichtig, weil `FrontendRenderer` eine Referenz auf genau dieses `GameConfig`-Objekt hält – ein Zurücksetzen ("Cancel" im Einstellungsmenü) muss also den Objektinhalt ändern, nicht die Referenz. |

### 6.2 `Difficulty.cs`

Enum `Difficulty { Easy, Normal, Hard }` plus der `readonly record struct
DifficultyRules`, der die konkreten Regeln pro Stufe hinterlegt:

```csharp
private static readonly DifficultyRules[] All =
{
    new DifficultyRules(Difficulty.Easy,   1,  false, 0.75),
    new DifficultyRules(Difficulty.Normal, 0,  false, 1.0),
    new DifficultyRules(Difficulty.Hard,  -1,  true,  1.5)
};
```

Interpretation der vier Felder: `ExtraAttempts` (Bonus-/Abzugsversuche gegenüber der
Konfiguration), `EnforceRevealedHints` (bei Hard müssen bereits aufgedeckte Hinweise
in jedem weiteren Versuch wiederverwendet werden – die klassische "Hard Mode"-Regel),
`ScoreMultiplier` (Faktor auf die Punktzahl).

**Design-Prinzip:** Die Spiel-Engine (`WordleGame`) enthält **kein** `switch` über
`Difficulty` für diese Regeln – sie fragt stattdessen `DifficultyRules.For(level)` und
liest die Werte aus. Neue Schwierigkeitsgrade lassen sich also durch Erweitern des
Arrays hinzufügen, ohne den Spielablauf-Code anzufassen (bis auf das Enum selbst).

### 6.3 `LetterState.cs`

```csharp
public enum LetterState { Empty, Absent, Present, Correct }
```

Die Reihenfolge ist **bewusst aufsteigend nach "Wichtigkeit"** gewählt: In
`WordleGame.UpdateKeyboard(...)` wird ein Vergleich `state > known` genutzt, um zu
verhindern, dass ein einmal als `Correct` (grün) erkannter Buchstabe auf der
Bildschirmtastatur später wieder auf `Present` (gelb) oder `Absent` (grau)
zurückfällt. Das funktioniert nur, weil die Enum-Werte in dieser Reihenfolge
deklariert sind (Enum-Werte sind in C# vergleichbar über ihre zugrunde liegende
Ganzzahl).

### 6.4 `WordleGuess.cs`

Ein abgegebener Rateversuch: `Word` (immer Großbuchstaben) + `States[]` (ein
`LetterState` pro Buchstabe). `IsWinning` prüft, ob alle Zustände `Correct` sind.

### 6.5 `BoardView.cs`

Der wichtigste "Transport"-Datentyp zwischen `WordleGame` und `FrontendRenderer`:
ein unveränderlicher Schnappschuss der laufenden Runde (bereits abgegebene Guesses,
aktuelle Eingabe, verstrichene Zeit, Tastaturzustände, etc.). Enthält zusätzlich
`ShimmerFrame` (Standard: `-1`), das nur während der Wörterbuch-Prüfung eines Guesses
gesetzt wird und die laufende Wellenanimation der aktiven Zeile steuert (siehe
[Abschnitt 13.3](#133-die-shimmer-animation-beim-prüfen-eines-wortes)).

### 6.6 `RoundResult.cs`

Fasst das Ergebnis einer abgeschlossenen Runde zusammen (gewonnen?, Zielwort,
gebrauchte/erlaubte Versuche, Dauer, Punkte, Schwierigkeitsgrad). Wird sowohl vom
Abschlussbildschirm als auch von `LeaderboardService.RecordResult(...)` konsumiert.

### 6.7 `LeaderboardEntry.cs`

Ein Zeileneintrag von `leaderboard.json` – kumulierte Statistik **pro Spielername**
(nicht pro Runde). Enthält zwei berechnete, nicht gespeicherte Felder
(`[JsonIgnore]`): `WinRate` und `AverageWinGuesses`.

### 6.8 `SettingsRow.cs`

Eine bereits übersetzte Zeile für den Einstellungsbildschirm: `Label`, `Value`,
optionale `SwatchColor` (Farbvorschau-Kästchen). `SettingsEditor` entscheidet, *was*
in einer Zeile steht; `FrontendRenderer` zeichnet nur, *was* er bekommt.

---

## 7. Core – Spiellogik (`Core/`)

### 7.1 `WordEvaluator.cs` – Die Bewertungslogik

Das ist die **kniffligste Logik im ganzen Projekt**: der klassische
Wordle-Doppelbuchstaben-Fall. Die Herausforderung: Wenn das Zielwort einen Buchstaben
nur einmal enthält, der Rateversuch ihn aber zweimal, darf **nur eine** der beiden
Stellen als "vorhanden" (gelb) markiert werden.

Beispiel aus dem Code-Kommentar: Zielwort `GHOST`, Guess `SASSY` → nur das *erste* S
wird farbig markiert, obwohl `SASSY` drei S enthält. Analog bleibt bei Zielwort
`LEVEL` und Guess `LLAMA` das zweite L grau, weil `LEVEL` nur ein L hat und dieses
bereits durch das erste L (an korrekter Position) "verbraucht" wurde.

Die Lösung ist ein **Zwei-Durchlauf-Algorithmus**:

```csharp
public LetterState[] EvaluateGuess(string guess, string target)
{
    var states = new LetterState[guess.Length];
    var consumed = new bool[target.Length];   // welche Zielbuchstaben sind schon "verbraucht"?

    // --- Pass 1: exakte Treffer (richtige Position) ---
    for (var index = 0; index < guess.Length; index++)
    {
        if (guess[index] != target[index]) continue;
        states[index] = LetterState.Correct;
        consumed[index] = true;
    }

    // --- Pass 2: Buchstabe kommt woanders im Zielwort vor ---
    for (var index = 0; index < guess.Length; index++)
    {
        if (states[index] == LetterState.Correct) continue;
        states[index] = LetterState.Absent;

        for (var candidate = 0; candidate < target.Length; candidate++)
        {
            if (consumed[candidate] || target[candidate] != guess[index]) continue;
            states[index] = LetterState.Present;
            consumed[candidate] = true;
            break;
        }
    }

    return states;
}
```

**Warum zwei Durchläufe nötig sind:** Würde man Treffer- und Vorhanden-Prüfung in
einem einzigen Durchlauf von links nach rechts erledigen, könnte ein Buchstabe, der
weiter rechts eigentlich exakt passt, fälschlich schon vorher als "irgendwo vorhanden"
verbraucht werden. Erst wenn **alle** exakten Treffer feststehen (Pass 1), lässt sich
in Pass 2 korrekt entscheiden, welche verbleibenden Zielbuchstaben noch "frei" sind.

Diese Klasse hat **keine Abhängigkeiten** und keinen Zustand (außer lokalen
Variablen) – ein reiner Funktionsbaustein, der isoliert getestet werden könnte.

### 7.2 `WordleGame.cs` – Die Spiel-Engine

Verantwortlich für **eine komplette Runden-Session** (kann aus mehreren
Wörtern/"Runden" hintereinander bestehen, solange der Spieler nach jedem
Abschlussbildschirm "weiterspielen" wählt).

**Konstruktor-Abhängigkeiten:** `config`, `renderer`, `localizer`, `api`, `words`,
`leaderboard`, `input` – plus zwei intern selbst erzeugte Helfer:
`WordEvaluator evaluator` und `ScoreCalculator scoreCalculator`.

**Hauptmethode `Run(Difficulty level)`:**

```csharp
public void Run(Difficulty level)
{
    while (PlayRound(level)) { /* Abschlussbildschirm entscheidet, ob's weitergeht */ }
}
```

**`PlayRound(level)` – Ablauf einer einzelnen Runde:**

1. **Zielwort besorgen.** Zuerst wird versucht, ein bereits vorgeladenes Wort aus dem
   `WordPool` zu nehmen (`TryTake`, synchron, kein Netzwerk). Ist der Pool leer, wird
   *ausnahmsweise* eine Ladeanimation gezeigt (`renderer.RunWithStatus(...)`), während
   ein Wort direkt von der API geholt wird. Kommt gar kein Wort zustande, wird eine
   Fehlermeldung angezeigt und die Runde bricht ab.
2. **Eingabeschleife.** Solange der Spieler tippt, wird bei jedem Tastendruck neu
   gezeichnet (`DrawBoard`). Buchstaben werden in `currentInput` gesammelt (maximal
   `wordLength` Zeichen), `Backspace` löscht das letzte Zeichen, `Escape` bricht die
   Runde **ohne** Niederlage-Wertung ab.
3. **Bei `Enter`:** `TrySubmit(...)` prüft in dieser Reihenfolge (bewusst
   "billigste Prüfung zuerst"):
   - Ist das Wort lang genug? (`game.tooShort`)
   - Bei Hard-Mode: Wurden alle bisher aufgedeckten Hinweise wiederverwendet?
     (`FindHintViolation`)
   - Nur wenn beides bestanden ist und Validierung aktiv ist: HTTP-Aufruf ans
     Wörterbuch (`api.IsValidWordAsync`), *während* das Brett weiter angezeigt wird
     und die aktive Zeile "schimmert" (siehe [13.3](#133-die-shimmer-animation-beim-prüfen-eines-wortes)).
   - Erst danach: `evaluator.EvaluateGuess(...)` und Erstellen eines `WordleGuess`.
4. **Sieg-/Niederlageprüfung.** Sieg, sobald `guess.IsWinning` true ist; Niederlage,
   sobald `guesses.Count == attemptsAllowed` ohne Sieg.
5. **`FinishRound(...)`:** berechnet Punkte über `ScoreCalculator`, bucht das Ergebnis
   über `LeaderboardService.RecordResult(...)`, zeigt den Abschlussbildschirm und
   wartet auf `Enter` (neue Runde) oder `Escape` (zurück ins Menü).

**Hard-Mode-Regelprüfung (`FindHintViolation`)** ist ein gutes Beispiel für die
Trennung von Spielregeln und Darstellung: Sie iteriert über alle bisherigen Guesses
und prüft für jede zuvor als `Correct` markierte Position, ob der neue Kandidat dort
denselben Buchstaben hat, sowie für jede als `Present` markierte Position, ob der
Buchstabe *irgendwo* im neuen Kandidaten vorkommt. Bei Verstoß wird ein übersetzter
Hinweistext (`game.hardPosition` / `game.hardContains`) zurückgegeben.

**Wichtiger Bugfix-relevanter Punkt:** `validationWasActive` merkt sich den Zustand
der Validierung *vor* dem Submit-Versuch. Fällt die Validierung während des Submits
aufgrund wiederholter Timeouts weg (siehe `DictionaryApiService.IsValidationActive`),
wird dem Spieler genau **einmal** die Meldung `game.validationOff` angezeigt.

### 7.3 `ScoreCalculator.cs` – Punkteberechnung

Reine, zustandslose Berechnung. Drei Komponenten fließen in die Punktzahl ein, in
dieser Priorität:

1. **Grundpunkte fürs Gewinnen** (`WinBasePoints = 1000`) – nur bei Sieg, sonst `0`.
2. **Bonus für ungenutzte Versuche** (`PointsPerUnusedGuess = 150` pro übrig
   gebliebenem Versuch).
3. **Geschwindigkeitsbonus**, der über die ersten 120 Sekunden (`SpeedBonusSeconds`)
   linear von `MaximumSpeedBonus = 600` auf `0` abfällt:

```csharp
private static int CalculateSpeedBonus(TimeSpan duration)
{
    var seconds = Math.Max(0, duration.TotalSeconds);
    if (seconds >= SpeedBonusSeconds) return 0;

    var remaining = 1 - seconds / SpeedBonusSeconds;
    return (int)Math.Round(MaximumSpeedBonus * remaining);
}
```

Am Ende wird die Summe mit dem `ScoreMultiplier` des gewählten Schwierigkeitsgrads
multipliziert (Easy 0,75×, Normal 1,0×, Hard 1,5×) und gerundet. Eine verlorene Runde
ist **immer** null Punkte wert, unabhängig von investierter Zeit.

### 7.4 `AppShell.cs` – Die Anwendungshülle

Steuert **alles außerhalb einer laufenden Runde**: Ladebildschirm, Namensabfrage,
Hauptmenü, Schwierigkeitsauswahl und die Randbildschirme (Hilfe, Bestenliste,
Einstellungen). Jeder Bildschirm folgt demselben wiederkehrenden Muster:

```
zeichnen → auf Eingabe warten → bei Resize neu zeichnen → auf Taste reagieren
```

Dieses Muster taucht in nahezu jeder privaten Methode auf (`RunMainMenu`,
`ChooseDifficulty`, `AskForName`, `RunSettingsEditor`, `ShowStaticScreen`) – ein
`needsRedraw`-Flag plus eine `while (true)`-Schleife.

**`Run()` – Startsequenz:**

```csharp
public void Run()
{
    if (Console.IsInputRedirected)     // kein Keyboard (z. B. Pipe/CI)
    {
        renderer.DrawHomeScreen(...);  // einmal zeichnen, dann beenden
        return;
    }

    if (!WaitForUsableWindow()) return;   // Terminal zu klein? → warten oder ESC

    words.WarmUp(config.GameSettings.WordLength, config.Language);  // Wörter vorladen
    renderer.DrawLoadingScreen();
    input.SyncWindowSize();

    if (config.PlayerName.Length == 0)   // allererster Start
        AskForName(isFirstRun: true);

    RunMainMenu();
}
```

**Menüsteuerung:** Die Menüeinträge werden über Übersetzungsschlüssel referenziert
(`MenuKeys` – ein statisches Array von Keys wie `"menu.play"`), nicht über
Klartext-Strings. So bleibt das Menü sprachunabhängig, und ein Sprachwechsel in den
Einstellungen wirkt sich sofort aus, weil `BuildMenuLabels()` bei jedem Frame neu
übersetzt (`MenuKeys.Select(localizer.Get)`).

**Schwierigkeitsauswahl (`ChooseDifficulty`)** merkt sich die zuletzt gewählte Stufe
in `config.Difficulty` und persistiert sie sofort über `configService.Save(config)`,
sodass der Picker beim nächsten Mal auf der zuletzt gewählten Stufe startet.

**Namensabfrage (`AskForName`)**: Ein leerer Name wird nicht akzeptiert (der Bildschirm
bleibt einfach offen), weil ein leerer Name die Bestenliste unbrauchbar machen würde.
Ändert der Spieler seinen Namen später über die Einstellungen, wird
`leaderboard.Rename(oldName, newName)` aufgerufen, damit bestehende Punkte am neuen
Namen "kleben bleiben".

**Fenstergrößen-Gate (`WaitForUsableWindow` / `DrawGuarded`)**: Bevor überhaupt etwas
gezeichnet wird, prüft `renderer.IsTerminalLargeEnough()`. Ist das Fenster zu klein,
erscheint ein Hinweisbildschirm statt eines kaputt umgebrochenen Boards. `DrawGuarded`
kapselt dieses Muster, damit jede Bildschirmzeichnung automatisch dagegen abgesichert
ist.

### 7.5 `SettingsEditor.cs` – Der Einstellungs-Editor

Wichtig zu verstehen: Diese Klasse **bearbeitet das `GameConfig`-Objekt direkt**
(„live"), nicht eine Kopie. Das bedeutet: Ändert der Spieler die Sprache, wird der
gesamte Einstellungsbildschirm sofort in der neuen Sprache angezeigt – noch bevor
gespeichert wurde. Erst beim Verlassen entscheidet sich, ob die Änderung dauerhaft
wird:

- **`Escape`** → `Cancel()` → `config.CopyFrom(snapshot)`: Der beim Öffnen
  angelegte `snapshot` (`config.Clone()`) wird zurückgespielt, alle Live-Änderungen
  werden verworfen (inkl. erneutem `localizer.SetLanguage(...)`, falls die Sprache
  zwischendurch geändert wurde).
- **`Enter` auf "Speichern"** → `Save()` → `config.Clamp()` +
  `configService.Save(config)`: Werte werden nochmal in gültige Grenzen gezwungen und
  auf die Festplatte geschrieben.

**Zeilenmodell (`SettingItem` – privates `record`):** Jede Zeile besteht aus einem
Übersetzungsschlüssel, einer Funktion zum Auslesen des aktuellen Werts, optional einer
Farbvorschau-Funktion, sowie entweder einer `Change`-Aktion (Wert per Links/Rechts
zyklisch verändern) oder einer `Activate`-Aktion (z. B. "Name ändern" öffnet einen
anderen Bildschirm). Das ist ein kleines **Strategie-Muster**: `BuildRows()` und
`HandleKey(...)` müssen nichts über die konkrete Einstellung wissen, sie rufen nur die
hinterlegten Delegates auf.

**Farbzyklus (`ThemePalette` + `CycleColor`):** Eine feste Liste von 21
Spectre.Console-kompatiblen Farbnamen. Pfeiltasten links/rechts wandern zyklisch durch
diese Liste. Steht in `config.json` eine Farbe, die *nicht* in der Palette enthalten
ist (z. B. von Hand eingetragen), springt der erste Klick auf den jeweils ersten bzw.
letzten Paletten-Eintrag.

**Sprachzyklus (`CycleLanguage`)**: Iteriert über `localizer.AvailableLanguages`
(automatisch aus vorhandenen `*.json`-Dateien ermittelt) und ruft sofort
`localizer.SetLanguage(...)` auf.

### 7.6 `InputReader.cs` – Tastatureingabe & Resize-Erkennung

Diese kleine Klasse ist der Trick, der das gesamte UI **responsiv gegenüber
Fenstergrößenänderungen** macht, obwohl .NET keine eingebaute "Resize"-Konsolen-Event
kennt.

```csharp
public virtual ConsoleKeyInfo? WaitForInput()
{
    while (true)
    {
        if (Console.KeyAvailable)
            return Console.ReadKey(intercept: true);

        if (WindowSizeChanged())
            return null;              // "kein Tastendruck, aber bitte neu zeichnen"

        Thread.Sleep(PollIntervalMilliseconds);   // 40 ms
    }
}
```

Statt blockierend auf `Console.ReadKey()` zu warten (was bei einer Fenstergrößenänderung
nie zurückkehren würde), wird alle 40 ms geprüft: *Liegt eine Taste an? Hat sich die
Fenstergröße geändert?* Ein `null`-Rückgabewert bedeutet für den Aufrufer immer
"kein echter Tastendruck, aber bitte neu zeichnen" – dieses Muster (`if (key is null)
{ needsRedraw = true; continue; }`) zieht sich durch **alle** Bildschirmschleifen in
`AppShell` und `WordleGame`.

`SyncWindowSize()` wird gezielt nach Zeichenoperationen aufgerufen, die *außerhalb*
der normalen Schleife stattfinden (z. B. nach der Ladeanimation oder einem API-Aufruf
mit eigener Statusanzeige), damit der nächste `WaitForInput()`-Aufruf nicht sofort
fälschlich einen "Resize" meldet, nur weil sich die zuletzt bekannte Fenstergröße seit
dem letzten regulären Frame geändert hat.

---

## 8. Services (`Services/`)

### 8.1 `ConfigService.cs`

Lädt und speichert `config.json`, das **neben der ausführbaren Datei** liegt
(`AppContext.BaseDirectory`). Nutzt `Microsoft.Extensions.Configuration` zum Einlesen
(bindet die JSON-Struktur direkt auf `GameConfig`) und `System.Text.Json` zum
Schreiben (mit `JsonStringEnumConverter`, damit z. B. `Difficulty` als lesbares
`"Normal"` statt als Zahl `1` gespeichert wird).

Fehlertoleranz: Eine fehlende oder kaputte Datei führt zu einem frischen
`GameConfig()` mit Standardwerten statt zu einem Absturz. Ein fehlgeschlagenes
Speichern (z. B. schreibgeschütztes Verzeichnis) wird über `LastError` gemeldet, ohne
das Spiel zu beenden.

### 8.2 `LocalizationService.cs`

Lädt alle `*.json`-Dateien aus `Resources/Languages/` und stellt Übersetzungen über
Schlüssel bereit.

**Fallback-Kette:** Gesuchter Key in aktiver Sprache → falls fehlt: Englisch
(`FallbackLanguage = "en"`) → falls auch dort fehlt: der Schlüssel selbst wird
zurückgegeben (nie eine leere Anzeige).

```csharp
public string Get(string key)
{
    if (texts.TryGetValue(key, out var text)) return text;
    return fallbackTexts.TryGetValue(key, out var fallback) ? fallback : key;
}
```

**Sprachenerkennung ist dateibasiert:** `DiscoverLanguages()` listet einfach alle
`.json`-Dateinamen im Ordner auf. Das heißt: **eine neue Sprache hinzufügen erfordert
keine Codeänderung** – siehe [13.4](#134-wie-eine-neue-sprache-hinzugefügt-wird).

**`Format(key, args)`** nutzt `string.Format` auf den übersetzten Text, sodass
Platzhalter wie `{0}`, `{1}` (z. B. in `"app.tagline": "{0} guesses. {1} letters..."`)
befüllt werden können.

**`GetLanguageName(language)`** liest den Anzeigenamen einer Sprache aus deren
*eigener* Datei (`"language.name"`), damit z. B. Deutsch als "Deutsch" (nicht als
"German") im Sprachumschalter erscheint.

### 8.3 `DictionaryApiService.cs`

Kapselt zwei externe HTTP-Aufrufe:

1. **Zufallswort abrufen** (`FetchWordsAsync`) – Endpoint aus `config.json` →
   `ApiEndpoints.RandomWord`. Standard: `https://random-word-api.herokuapp.com/word`.
2. **Wort im Wörterbuch validieren** (`IsValidWordAsync`) – Endpoint aus
   `ApiEndpoints.DictionaryValidation`. Standard:
   `https://api.dictionaryapi.dev/api/v2/entries`.

**Wichtige Design-Entscheidung, dokumentiert im Kopfkommentar der Klasse:**

> Die beiden Aufrufe werden absichtlich als *unzuverlässig und langsam* behandelt:
> - Jede Anfrage hat ihr **eigenes kurzes Timeout**. Das Wörterbuch braucht für ein
>   bekanntes Wort ca. 0,1s, aber für ein *unbekanntes* Wort teils **20 Sekunden**,
>   bis es das meldet. Ein derart langes Warten nach jedem Rateversuch würde das
>   Spiel unspielbar machen – deshalb ist die Validierungs-Timeout-Grenze viel enger
>   als die Timeout-Grenze für den Wort-Download.
> - Validierung **"fails open"**: Nur eine *explizite* "nicht gefunden"-Antwort
>   (HTTP 404) lehnt einen Rateversuch ab. Timeouts, Serverfehler etc. lassen den
>   Versuch durch.
> - Nach zwei aufeinanderfolgenden Fehlschlägen wird die Validierung für den Rest der
>   Sitzung **komplett abgeschaltet** (`FailuresBeforeGivingUp = 2`), damit ein totes
>   Backend das Spiel nicht dauerhaft ausbremst.
> - Antworten werden **gecacht** (`validationCache`), sodass dasselbe Wort nie zweimal
>   nachgefragt wird.

```csharp
public bool IsValidationActive => endpoints.ValidateGuesses
    && consecutiveValidationFailures < FailuresBeforeGivingUp;
```

**URL-Aufbau ist defensiv gegen unterschiedliche Konfigurationsformen:**
`BuildValidationUrl` erkennt, ob die konfigurierte Basis-URL bereits ein
Sprachkürzel als letztes Segment enthält (z. B. `.../entries/en`) und entfernt es in
dem Fall, um Duplikate wie `.../entries/en/en/apple` zu vermeiden.

**Statuscode-Interpretation:**
```csharp
if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound)
{
    consecutiveValidationFailures = 0;
    var isValid = response.StatusCode == HttpStatusCode.OK;
    validationCache[key] = isValid;
    return isValid;
}
RegisterValidationFailure(...);   // andere Statuscodes zählen als "Dienst hat ein Problem"
return true;                      // → Guess wird trotzdem akzeptiert
```

### 8.4 `WordPool.cs`

Ein **In-Memory-Puffer** für Zielwörter, organisiert nach Schlüssel
`"<language>:<length>"` (z. B. `"de:5"`). Zweck: Der Beginn einer neuen Runde soll
nicht auf einen Netzwerkaufruf warten müssen.

**Funktionsweise:**
- `BatchSize = 10`: Eine Anfrage holt gleich 10 Wörter statt eines einzelnen.
- `RefillThreshold = 3`: Sobald der Puffer auf 3 oder weniger Wörter sinkt, wird
  bereits im Hintergrund nachgeladen (`StartRefill` via `Task.Run`), noch bevor der
  Puffer komplett leer ist.
- `refillsInFlight` (ein `HashSet<string>`) verhindert doppelte parallele
  Nachlade-Anfragen für denselben Schlüssel.
- Alles ist mit einem einzigen `lock (gate)`-Objekt gegen gleichzeitigen Zugriff
  aus dem Hintergrund-Task und dem UI-Thread abgesichert.

**`TryTake` vs. `Take`:** `TryTake` gibt sofort `false` zurück, wenn nichts im Puffer
ist (nicht-blockierend). `Take` fällt in diesem Fall auf einen **synchronen**
API-Aufruf zurück (`GetAwaiter().GetResult()`) – das ist der einzige Fall, in dem das
Spiel tatsächlich auf das Netzwerk wartet, und genau dieser Fall wird in `WordleGame`
mit einer Ladeanimation kaschiert.

Wichtig: **Der Pool ist rein sitzungsgebunden** – nichts wird auf die Festplatte
geschrieben, bei jedem Neustart beginnt das Nachladen von vorn.

### 8.5 `LeaderboardService.cs`

Verwaltet `leaderboard.json` (liegt wie `config.json` neben der `.exe`). Eine Liste
von `LeaderboardEntry` – **ein Eintrag pro Spielername**, keine Liste einzelner
Runden.

**`RecordResult(name, result)`** – findet oder erstellt den Eintrag des Spielers und
aktualisiert ihn:
- `GamesPlayed`, `TotalPoints`, `BestPoints`, `LastPlayed` werden immer aktualisiert.
- Bei Sieg zusätzlich: `Wins`, `TotalWinGuesses`, `CurrentStreak` (+1),
  `BestStreak` (Max), und ggf. ein neuer `BestTimeSeconds`-Rekord (der erste Sieg
  setzt automatisch den Rekord, da `BestTimeSeconds == 0` als "noch kein Rekord"
  interpretiert wird).
- Bei Niederlage: `CurrentStreak` wird auf `0` zurückgesetzt.

**`Rename(oldName, newName)`**: Wird von `AppShell.AskForName` aufgerufen, wenn der
Spieler seinen Namen ändert. Schutzmechanismus: Existiert bereits ein Eintrag mit dem
neuen Namen, wird **nicht** zusammengeführt (das würde stillschweigend die Statistiken
zweier unterschiedlicher Personen vermischen) – der alte Eintrag bleibt unter seinem
alten Namen bestehen.

**`GetRanked()`** – Sortierreihenfolge: `TotalPoints` absteigend → `Wins` absteigend
→ `AverageWinGuesses` aufsteigend (Spieler ohne Sieg werden ans Ende sortiert, indem
`double.MaxValue` statt `0` verwendet wird) → Name alphabetisch als letzter
Tie-Breaker.

---

## 9. UI-Schicht (`UI/`)

### 9.1 `ConsoleSession.cs`

Ein `IDisposable`, das den **Lebenszyklus des Terminals selbst** kapselt:

- Beim Start (`Start()`): UTF-8-Ausgabe setzen (für Rahmenzeichen), ursprüngliche
  Cursor-Sichtbarkeit merken, Hintergrund schwarz + Vordergrund weiß setzen, Bildschirm
  löschen, Cursor ausblenden, Fenstertitel auf "WORDLE" setzen.
- Beim Beenden (`Dispose()`): Cursor-Sichtbarkeit wiederherstellen, Konsolenfarben
  zurücksetzen (`Console.ResetColor()`).

**Absicherung gegen abnormale Beendigung:** Sowohl `AppDomain.CurrentDomain.ProcessExit`
als auch `Console.CancelKeyPress` (Strg+C) sind mit `RestoreCursor()` verdrahtet.
Damit bleibt der Cursor auch dann sichtbar, wenn der Prozess abstürzt oder der Nutzer
das Fenster hart schließt – ohne diese Absicherung könnte ein Nutzer nach einem Crash
mit einem dauerhaft unsichtbaren Cursor in seinem Terminal zurückbleiben.

Alle plattformspezifischen Aufrufe (`Console.CursorVisible`, `Console.Title`,
`Console.OutputEncoding`) sind in `try/catch` gekapselt, weil sie auf manchen
Terminals/Betriebssystemen (insbesondere bei umgeleiteter Ausgabe) eine Exception
werfen können, ohne dass das das Spiel beenden sollte.

### 9.2 `FrontendRenderer.cs` – Das Herzstück der Darstellung

Die **einzige** Klasse im gesamten Projekt, die `AnsiConsole` bzw. `Spectre.Console`
direkt verwendet (über 1100 Zeilen, mit Abstand die größte Datei). Sie bekommt reine
Daten (`BoardView`, `RoundResult`, Listen von `string`/`SettingsRow`) und produziert
daraus vollständige Bildschirme.

#### 9.2.1 Adaptive Kachelgröße

```csharp
private static readonly TileScale[] TileScales =
{
    new TileScale(11, 5), new TileScale(9, 5), new TileScale(7, 3),
    new TileScale(5, 3),  new TileScale(5, 1), new TileScale(3, 1)
};
```

`TileScale(Width, Height)` beschreibt, wie viele Zeichen breit/hoch eine einzelne
Buchstaben-Kachel ist. Die Liste ist von **groß nach klein** sortiert.
`MeasureLayout(...)` probiert der Reihe nach mehrere "Qualitätsstufen" durch
(Figlet-Banner an/aus, Zeilenabstand an/aus, Bildschirmtastatur an/aus) und wählt
innerhalb jeder Stufe die größtmögliche Kachelgröße, die noch ins aktuelle
Fenster passt:

```csharp
var preferences = new[]
{
    (ShowFiglet: true,  UseRowSpacing: true,  ShowKeyboard: true),
    (ShowFiglet: false, UseRowSpacing: true,  ShowKeyboard: true),
    (ShowFiglet: false, UseRowSpacing: true,  ShowKeyboard: false),
    (ShowFiglet: false, UseRowSpacing: false, ShowKeyboard: false)
};
```

**Wichtig für das Verständnis:** Qualität wird in fester Reihenfolge geopfert – zuerst
das große ASCII-Banner, dann die Bildschirmtastatur, dann der Zeilenabstand zwischen
den Reihen. Erst wenn *gar keine* Kombination passt, greift
`renderer.IsTerminalLargeEnough()` in `AppShell`/`WordleGame`, um stattdessen den
"Fenster zu klein"-Bildschirm zu zeigen.

#### 9.2.2 Live-Neuberechnung bei Fenstergrößenänderung

```csharp
private static void SyncProfileWithWindow()
{
    if (Console.IsOutputRedirected) return;
    AnsiConsole.Profile.Width = Math.Max(1, Console.WindowWidth);
    AnsiConsole.Profile.Height = Math.Max(1, Console.WindowHeight);
}
```

Spectre.Console cached die Terminalgröße intern in `AnsiConsole.Profile`. Diese
Methode wird bei **jedem** `BeginFrame()`-Aufruf ausgeführt und sorgt dafür, dass
Spectre die tatsächliche, aktuelle Fenstergröße kennt – das ist die Grundlage dafür,
dass sich das Layout überhaupt live anpassen kann.

#### 9.2.3 Kachel-Rendering ohne echte Grafik

Eine Kachel ist keine Grafik, sondern ein Textblock aus mehreren Zeilen mit
Hintergrundfarbe:

```csharp
private static IRenderable BuildBlockTile(char letter, string background, TileScale scale)
{
    var blankLine = new string(' ', scale.Width);
    var letterLine = CenterInTile(letter, scale.Width);
    var lines = new string[scale.Height];
    for (var line = 0; line < scale.Height; line++)
        lines[line] = line == scale.Height / 2 ? letterLine : blankLine;

    var markup = string.Join(Environment.NewLine,
        lines.Select(line => $"[bold white on {background}]{line}[/]"));
    return new Markup(markup);
}
```

Nur die **mittlere Zeile** des Blocks trägt den Buchstaben, alle anderen Zeilen sind
leer, aber mit derselben Hintergrundfarbe – dadurch entsteht optisch ein solider,
mehrzeiliger Farbblock mit dem Buchstaben genau in der Mitte.

**Fallback ohne Farbunterstützung** (`SupportsColor()` prüft
`AnsiConsole.Profile.Capabilities.ColorSystem`): Statt eines unsichtbaren
Hintergrundblocks wird der Zustand über Klammern kodiert:

```csharp
private static string FormatPlainTile(char letter, LetterState state) => state switch
{
    LetterState.Correct => $"[[{visibleLetter}]]",   // [[W]]
    LetterState.Present => $"({visibleLetter})",      // (O)
    _                    => $" {visibleLetter} "      //  X
};
```

#### 9.2.4 Die "Shimmer"-Welle beim Prüfen eines Wortes

Siehe ausführlich [Abschnitt 13.3](#133-die-shimmer-animation-beim-prüfen-eines-wortes).
Kurz zusammengefasst: `RunWhileCheckingGuess<T>` startet den asynchronen
Wörterbuch-Aufruf, zeigt aber währenddessen das Board über `AnsiConsole.Live(...)`
mit fortlaufend erhöhtem `ShimmerFrame` an, wodurch eine graue Welle einmal über die
aktive Zeile zu laufen scheint.

#### 9.2.5 Escaping von Übersetzungstexten

```csharp
private static string Escape(string text) => Markup.Escape(text);
```

**Kritischer Punkt:** Übersetzte/benutzereingegebene Texte (Spielername!) werden **immer**
über `Escape(...)` geleitet, bevor sie in einen Markup-String eingebettet werden. Ohne
das könnte z. B. ein Spielername mit eckigen Klammern (`[bold red]`) als
Spectre-Markup-Tag interpretiert werden und die Darstellung durcheinanderbringen oder
unerwartete Formatierung einschleusen.

#### 9.2.6 Größenvoraussetzungen und "Fenster zu klein"

`MinimumTerminalSize` berechnet, wie groß das Fenster mindestens sein muss (basierend
auf der *kleinsten* Kachelgröße und der konfigurierten Rundenlänge +1 Zeile Puffer für
den Easy-Modus). `IsTerminalLargeEnough()` vergleicht das mit
`AnsiConsole.Profile.Width/Height`. Erst wenn diese Prüfung fehlschlägt, erscheint
`DrawTerminalTooSmall()`.

#### 9.2.7 Weitere Bildschirme (Kurzüberblick)

| Methode | Bildschirm |
|---|---|
| `DrawLoadingScreen()` | Fake-"Boot"-Animation mit vier `AnsiConsole.Progress()`-Schritten |
| `DrawHomeScreen(...)` | Hauptmenü mit Titel, Tagline, Begrüßung, Auswahlliste |
| `DrawDifficultyScreen(...)` | Schwierigkeitsauswahl mit Beschreibungstext |
| `DrawBoard(view)` | Das laufende Spielbrett |
| `DrawFinishScreen(...)` | Rundenergebnis + Zusammenfassung (Wort, Zeit, Punkte, Gesamtpunkte) |
| `DrawLeaderboard(...)` | Rangliste als `Table` (Details wie Siege/Streak nur ab Fensterbreite 72) |
| `DrawNamePrompt(...)` | Namenseingabe mit blinkendem Cursor-Ersatzzeichen `█` |
| `DrawHelpScreen()` | Spielanleitung mit Beispielkacheln |
| `DrawSettingsScreen(...)` | Editierbare Einstellungsliste |
| `DrawMessageScreen(...)` | Generischer Fehler-/Hinweisbildschirm |
| `DrawTerminalTooSmall()` | Hinweis bei zu kleinem Fenster |

---

## 10. Konfigurationsdatei `config.json`

Liegt im Projekt unter `WordleApp/config.json` und wird beim Build automatisch neben
die `.exe` kopiert. Struktur (Standardwerte):

```json
{
  "Language": "de",
  "PlayerName": "",
  "Difficulty": "Normal",
  "GameSettings": {
    "MaxAttempts": 6,
    "WordLength": 5
  },
  "Theme": {
    "ColorCorrect": "green",
    "ColorPresent": "orange3",
    "ColorAbsent": "grey37",
    "ColorEmpty": "grey15",
    "AsciiTitleColor": "orange3"
  },
  "ApiEndpoints": {
    "RandomWord": "https://random-word-api.herokuapp.com/word",
    "DictionaryValidation": "https://api.dictionaryapi.dev/api/v2/entries",
    "ValidateGuesses": true,
    "RequestTimeoutSeconds": 6,
    "ValidationTimeoutSeconds": 1.5
  }
}
```

| Feld | Bedeutung | Gültige Grenzen |
|---|---|---|
| `Language` | Sprachcode, muss zu einer Datei in `Resources/Languages/` passen | – |
| `PlayerName` | Wird beim ersten Start abgefragt | max. 16 Zeichen (`AppShell.MaximumNameLength`) |
| `Difficulty` | Zuletzt gewählte Schwierigkeit | `Easy` / `Normal` / `Hard` |
| `GameSettings.MaxAttempts` | Basisanzahl Versuche (vor Schwierigkeits-Modifikator) | 3–10 |
| `GameSettings.WordLength` | Wortlänge / Anzahl Spalten | 3–8 |
| `Theme.*` | Spectre.Console-Farbnamen (z. B. `"green"`, `"orange3"`, `"grey37"`) | jede von `Style.Parse(...)` erkannte Farbe |
| `ApiEndpoints.ValidateGuesses` | Wörterbuch-Prüfung an/aus | `true`/`false` |
| `ApiEndpoints.RequestTimeoutSeconds` | Timeout für Wort-Download | wird auf 0,5–30s geklemmt |
| `ApiEndpoints.ValidationTimeoutSeconds` | Timeout für Wörterbuch-Lookup | wird auf 0,2–10s geklemmt |

Die Datei kann **von Hand editiert** werden – lädt `ConfigService.Load()` einen Wert
außerhalb der gültigen Grenzen, wird er beim Start automatisch über `config.Clamp()`
zurechtgestutzt.

---

## 11. Übersetzungen (`Resources/Languages/`)

Ein flaches JSON-Objekt aus `"schlüssel.mitpunkten": "Übersetzter Text"` pro Datei
(`de.json`, `en.json`). Der Dateiname (ohne `.json`) *ist* der Sprachcode.

Wichtige Konventionen:

- **Platzhalter** in geschweiften Klammern (`{0}`, `{1}`, …) werden via
  `LocalizationService.Format(key, args)` mit `string.Format` befüllt – z. B.
  `"app.tagline": "{0} guesses. {1} letters. One word."`.
- **`keyboard.rows`** ist ein Sonderfall: Ein einzelner String, bei dem Zeilen mit
  `|` getrennt sind (z. B. `"QWERTYUIOP|ASDFGHJKL|ZXCVBNM"`). Dadurch kann die
  deutsche Datei ein QWERTZ-Layout hinterlegen, während Englisch QWERTY zeigt – ohne
  Codeänderung.
- **`language.name`** ist der Anzeigename der Sprache in ihrer *eigenen* Sprache
  (z. B. in `de.json`: `"Deutsch"`), damit der Sprachumschalter in den Einstellungen
  native Bezeichnungen zeigt.
- Fehlt ein Schlüssel in einer nicht-englischen Datei, greift automatisch die
  englische Übersetzung (siehe `LocalizationService.Get`).

---

## 12. Persistente Dateien zur Laufzeit

Beide Dateien liegen **immer im selben Verzeichnis wie die ausführbare Datei**
(`AppContext.BaseDirectory`), nicht in einem Benutzerprofilordner:

| Datei | Erzeugt/verwaltet von | Inhalt |
|---|---|---|
| `config.json` | `ConfigService` | Spielereinstellungen, Theme, API-Konfiguration |
| `leaderboard.json` | `LeaderboardService` | Liste kumulierter `LeaderboardEntry`-Objekte, ein Eintrag pro Spielername |

`leaderboard.json` existiert nicht von Anfang an im Repository – sie wird beim ersten
`RecordResult(...)`-Aufruf (also nach der ersten abgeschlossenen Runde) automatisch
angelegt.

---

## 13. Wichtige Workflows im Detail

### 13.1 Ablauf einer Runde

```
AppShell.StartGame()
   → ChooseDifficulty()                     Spieler wählt Easy/Normal/Hard
   → config.Difficulty = level; Save()
   → new WordleGame(...).Run(level)
        │
        └─ PlayRound(level)  (wiederholt sich, solange der Spieler weiterspielt)
             │
             ├─ WordPool.TryTake(...)  ──(leer?)──▶ RunWithStatus(...) → WordPool.Take(...)
             │
             ├─ Eingabeschleife:
             │    Buchstabe   → currentInput += Buchstabe (max. wordLength)
             │    Backspace   → letztes Zeichen entfernen
             │    Escape      → Runde abbrechen (kein Loss-Eintrag)
             │    Enter       → TrySubmit(...)
             │                    ├─ zu kurz?              → Meldung, weiter tippen
             │                    ├─ Hard-Mode-Verstoß?    → Meldung, weiter tippen
             │                    ├─ Wörterbuch-Check       (mit Shimmer-Animation)
             │                    │     ungültig?          → Meldung, weiter tippen
             │                    └─ WordEvaluator.EvaluateGuess(...) → WordleGuess
             │
             ├─ Sieg?  → FinishRound(..., won: true)
             └─ Letzter Versuch ohne Sieg? → FinishRound(..., won: false)
                    │
                    ├─ ScoreCalculator.Calculate(...)
                    ├─ LeaderboardService.RecordResult(...)
                    └─ DrawFinishScreen(...) → Enter = neue Runde, Escape = Menü
```

### 13.2 Resize-Handling

Da .NET keine native "Konsolenfenster wurde verändert"-Benachrichtigung bietet, wird
das Verhalten über **Polling** simuliert:

1. `InputReader` merkt sich bei jedem Aufruf `WaitForInput()` die zuletzt bekannte
   Fenstergröße.
2. Alle 40 ms (`PollIntervalMilliseconds`) wird geprüft: Liegt eine Taste an? Hat sich
   die Größe geändert?
3. Bei einer Größenänderung wird `null` zurückgegeben statt eines `ConsoleKeyInfo`.
4. **Jede** aufrufende Schleife (in `AppShell` und `WordleGame`) behandelt `null`
   identisch: `needsRedraw = true; continue;` – das führt zu einem Neuzeichnen des
   *aktuellen* Bildschirms mit dem *aktuellen* Zustand, aber angepasst an die neue
   Fenstergröße (weil `FrontendRenderer.BeginFrame()` bei jedem Zeichnen erneut
   `SyncProfileWithWindow()` aufruft und `MeasureLayout(...)` die Kachelgröße neu
   berechnet).

### 13.3 Die "Shimmer"-Animation beim Prüfen eines Wortes

Wenn ein Rateversuch abgegeben wird und die Wörterbuch-Validierung aktiv ist, soll das
Spiel *nicht* auf einen separaten Ladebildschirm wechseln (das würde bei jedem
einzelnen Guess unschön wirken), sondern das Board bleibt sichtbar und die gerade
geprüfte Zeile "schimmert" als graue Welle.

**Ablauf (`FrontendRenderer.RunWhileCheckingGuess<T>`):**

```csharp
public T RunWhileCheckingGuess<T>(BoardView view, Func<Task<T>> work)
{
    var pending = work();                     // API-Aufruf sofort starten (async)

    if (Console.IsOutputRedirected)
        return pending.GetAwaiter().GetResult();   // kein Terminal → keine Animation

    var frame = 0;
    AnsiConsole.Live(BuildBoardScreen(view with { ShimmerFrame = frame }))
        .AutoClear(false)
        .Start(context =>
        {
            while (!pending.IsCompleted)
            {
                context.UpdateTarget(BuildBoardScreen(view with { ShimmerFrame = ++frame }));
                context.Refresh();
                Thread.Sleep(ShimmerFrameMilliseconds);   // 80 ms
            }
        });

    return pending.GetAwaiter().GetResult();
}
```

`AnsiConsole.Live(...)` ist eine Spectre.Console-Funktion, die ein Renderable
**in-place** aktualisiert, ohne den gesamten Bildschirm zu löschen und neu
aufzubauen – das verhindert das Flackern, das ein vollständiges `AnsiConsole.Clear()`
bei jedem Frame verursachen würde.

Die eigentliche Wellenoptik entsteht in `BuildShimmerTile`:

```csharp
var offset = ((column - frame) % ShimmerShades.Length + ShimmerShades.Length) % ShimmerShades.Length;
return BuildBlockTile(letter, ShimmerShades[offset], scale);
```

`ShimmerShades` ist ein Array von acht Grautönen, von hell nach dunkel sortiert. Der
Ausdruck `(column - frame) mod Length` sorgt dafür, dass mit steigendem `frame` die
"helle Kante" der Welle von links nach rechts über die Spalten der aktiven Zeile
wandert. Die doppelte Modulo-Berechnung (`(x % n + n) % n`) ist nötig, weil `column -
frame` in C# auch negativ werden kann und der `%`-Operator bei negativen Zahlen in C#
(anders als z. B. in Python) ein negatives Ergebnis liefert – ohne diese Korrektur
würde ein negativer Index zu einer `IndexOutOfRangeException` führen.

### 13.4 Wie eine neue Sprache hinzugefügt wird

1. Neue Datei `Resources/Languages/<code>.json` anlegen (z. B. `fr.json`), Code =
   Dateiname ohne Endung.
2. Alle Schlüssel aus `en.json` übersetzen (mindestens die wichtigsten – fehlende
   Schlüssel fallen automatisch auf Englisch zurück).
3. `"language.name"` mit dem nativen Namen der Sprache setzen (z. B. `"Français"`).
4. `"keyboard.rows"` an das gewünschte Tastaturlayout anpassen (Format:
   `"ZEILE1|ZEILE2|ZEILE3"`).
5. Sicherstellen, dass die Datei in `WordleApp.csproj` mit kopiert wird – das ist
   bereits über den Wildcard-Eintrag `Resources\Languages\*.json` abgedeckt, es ist
   also **keine Anpassung der `.csproj` nötig**.

Die neue Sprache erscheint automatisch im Sprachumschalter der Einstellungen, weil
`LocalizationService.DiscoverLanguages()` den Ordner zur Laufzeit scannt.

### 13.5 Wie eine neue Einstellung hinzugefügt wird

Am Beispiel einer neuen Konfigurationsoption:

1. **Model erweitern** – Feld in `GameConfig` (bzw. einer der Unterklassen wie
   `GameSettings`/`ThemeSettings`/`ApiSettings`) in `Models/GameConfig.cs` ergänzen,
   inkl. Standardwert.
2. Falls relevant: Feld in `GameConfig.Clone()` und `GameConfig.CopyFrom(...)`
   ergänzen (**leicht zu vergessen!** – ohne diesen Schritt funktioniert "Abbrechen"
   im Einstellungsmenü für das neue Feld nicht korrekt).
3. Falls es Grenzen braucht: in `GameConfig.Clamp()` berücksichtigen.
4. **Zeile im Editor ergänzen** – neues `SettingItem` in
   `SettingsEditor.BuildItems()` mit Übersetzungsschlüssel, Lese-Funktion und
   Änderungs-/Aktivierungs-Logik.
5. **Übersetzungsschlüssel** in `de.json` **und** `en.json` ergänzen (Konvention:
   `"settings.<name>"`).
6. Der Renderer (`DrawSettingsScreen` / `BuildSettingsGrid`) benötigt **keine
   Änderung**, da er generisch über `IReadOnlyList<SettingsRow>` arbeitet.

---

## 14. Fehlerbehandlung & Robustheit

Ein durchgängiges Muster im gesamten Code: **Nichts darf das Spiel zum Absturz
bringen, wenn eine externe Ressource (Datei, Netzwerk, Terminal-Feature) fehlt oder
fehlerhaft ist.**

| Situation | Verhalten |
|---|---|
| `config.json` fehlt/kaputt | Standardwerte werden verwendet (`ConfigService.Load`) |
| `config.json` nicht schreibbar | Änderung bleibt nur im Arbeitsspeicher, `LastError` gesetzt |
| `leaderboard.json` fehlt/kaputt | Leere Bestenliste (`LeaderboardService.Load`) |
| Sprachdatei fehlt/kaputt | Leeres Wörterbuch → alles fällt auf Englisch zurück |
| Zufallswort-API nicht erreichbar | Fehlermeldungsbildschirm (`error.noWord`), Runde startet nicht |
| Wörterbuch-API nicht erreichbar/langsam | Validierung wird nach 2 Fehlversuchen automatisch deaktiviert, Guess wird akzeptiert |
| Terminal zu klein | Eigener Hinweisbildschirm statt kaputtem Layout |
| Keine Farbunterstützung im Terminal | Klammer-Notation statt Farbblöcken |
| Ausgabe umgeleitet (Pipe/CI) | Kein Blockieren auf Tastatureingabe; ein Frame wird gezeichnet und das Programm beendet sich |
| Absturz/Strg+C während der Ausführung | `ConsoleSession` stellt Cursor & Farben trotzdem wieder her |

---

## 15. Bekannte Grenzen / Was fehlt

- **Keine automatisierten Tests** im Repository, obwohl `InputReader.WaitForInput()`
  und die Methoden von `DictionaryApiService` bewusst als `virtual` markiert sind, um
  Testbarkeit (z. B. via Mocking-Framework oder handgeschriebener Testdoubles) zu
  ermöglichen.
- **Kein Multiplayer**, kein Online-Abgleich der Bestenliste – alles ist rein lokal
  (Single-Player, lokale JSON-Dateien).
- **Kein "täglicher" Wordle-Modus** mit für alle Spieler identischem Tageswort – jedes
  Zielwort kommt zufällig aus der Random-Word-API.
- **Keine Unit-of-Work/Datenbank** – reine Flatfile-Persistenz (JSON neben der `.exe`),
  was bei sehr vielen Spielern/Einträgen nicht skaliert, für den Anwendungsfall (lokales
  Konsolenspiel) aber ausreichend ist.
- `WordleApp.sln` ist eine reine Visual-Studio-Solution-Datei ohne besonderen
  Konfigurationsinhalt – für Entwicklung reicht auch direkt `dotnet run`/`dotnet build`
  im Ordner `WordleApp/`.

---

## 16. Glossar

| Begriff | Erklärung |
|---|---|
| **TUI** | Terminal User Interface – grafisch aufbereitete Konsolenoberfläche |
| **Figlet** | ASCII-Art-Textschrift, hier via `Spectre.Console`s `FigletText`-Klasse für den "WORDLE"-Titel |
| **Renderable** (`IRenderable`) | Spectre.Console-Schnittstelle für alles, was gezeichnet werden kann (Text, Panel, Grid, Tabelle, …) |
| **Record / record struct** | C#-Sprachfeature für unveränderliche Datenobjekte mit automatisch generierter Gleichheitsprüfung; hier für Zustands-Schnappschüsse wie `BoardView` oder `RoundResult` verwendet |
| **Guess** | Ein abgegebener Rateversuch (Wort) des Spielers |
| **Target Word / Zielwort** | Das gesuchte, geheime Wort einer Runde |
| **Shimmer** | Die graue Wellenanimation, die während der Wörterbuch-Prüfung über die aktive Zeile läuft |
| **Fails open** | Design-Prinzip bei der Validierung: Im Zweifel (Fehler, Timeout) wird der Rateversuch akzeptiert statt abgelehnt |
| **Warm-up / Vorladen** | Das Nachladen von Zielwörtern im Hintergrund, bevor sie tatsächlich gebraucht werden (`WordPool.WarmUp`) |
| **Snapshot / Schnappschuss** | Eine zu einem Zeitpunkt eingefrorene Kopie eines veränderlichen Objekts, z. B. `GameConfig.Clone()` vor dem Bearbeiten in den Einstellungen |
| **Clamp** | Einen Wert auf einen erlaubten Wertebereich zurechtstutzen (`Math.Clamp`) |
