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
  `Program.cs` von Hand verdrahtet – siehe [Programmablauf](lifecycle.md)).
- **`virtual`-Methoden für Testbarkeit.** `InputReader.WaitForInput()` und die
  Methoden von `DictionaryApiService` sind `virtual`, damit man sie in Tests mocken
  könnte (aktuell gibt es im Repository jedoch **keine automatisierten Tests**).

---

