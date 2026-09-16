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
