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
[AppShell.cs](core.md#74-appshellcs-die-anwendungshulle)).

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

