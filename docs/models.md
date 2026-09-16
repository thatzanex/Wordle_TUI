## 6. Models (`Models/`)

Die Models sind bewusst "dumm": keine I/O, keine Geschäftslogik außer einfachen
Ableitungen (z. B. Prozentwerte). Viele sind `readonly record struct`, also
unveränderliche Wertetypen mit automatisch generierter Gleichheit – ideal für
Schnappschüsse, die pro Frame neu gebaut werden.

### 6.1 `GameConfig.cs`

Bildet `config.json` 1:1 als C#-Objekt ab (siehe [Konfiguration](configuration.md)).
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
[Die "Shimmer"-Animation](workflows.md#133-die-shimmer-animation-beim-prufen-eines-wortes)).

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

