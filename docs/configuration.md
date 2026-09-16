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

