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

