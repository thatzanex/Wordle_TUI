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

