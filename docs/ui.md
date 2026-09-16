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

Siehe ausführlich [Die "Shimmer"-Animation](workflows.md#133-die-shimmer-animation-beim-prufen-eines-wortes).
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

