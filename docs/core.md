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
     und die aktive Zeile "schimmert" (siehe [Die "Shimmer"-Animation](workflows.md#133-die-shimmer-animation-beim-prufen-eines-wortes)).
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

