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
keine Codeänderung** – siehe [Wie eine neue Sprache hinzugefügt wird](workflows.md#134-wie-eine-neue-sprache-hinzugefugt-wird).

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

