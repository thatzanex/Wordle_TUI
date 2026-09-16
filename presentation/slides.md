---
marp: true
theme: default
paginate: true
size: 16:9
style: |
  section {
    font-family: 'Segoe UI', 'Helvetica Neue', Arial, sans-serif;
    background: #121213;
    color: #ffffff;
    padding: 60px 70px;
  }
  h1 {
    color: #ffffff;
    font-size: 2.4em;
    letter-spacing: 2px;
    text-transform: uppercase;
  }
  h2 {
    color: #6aaa64;
    font-size: 1.5em;
    border-bottom: 3px solid #6aaa64;
    padding-bottom: 8px;
    display: inline-block;
  }
  h3 { color: #c9b458; }
  strong { color: #6aaa64; }
  code {
    background: #3a3a3c;
    color: #ffffff;
    border-radius: 4px;
    padding: 2px 6px;
  }
  pre {
    background: #1e1e1e;
    border: 1px solid #3a3a3c;
    border-radius: 8px;
  }
  table {
    font-size: 0.75em;
  }
  th {
    background: #3a3a3c;
    color: #ffffff;
  }
  td { color: #d7dadc; }
  a { color: #c9b458; }
  section.lead {
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: flex-start;
  }
  section.center {
    display: flex;
    flex-direction: column;
    justify-content: center;
  }
  ul, ol, p { font-size: 0.85em; line-height: 1.5; }
  .tiles {
    display: flex;
    gap: 8px;
    margin: 20px 0;
  }
  .tile {
    width: 56px; height: 56px;
    display: flex; align-items: center; justify-content: center;
    font-weight: bold; font-size: 1.6em; color: white;
    border-radius: 4px;
  }
  .correct { background: #6aaa64; }
  .present { background: #c9b458; }
  .absent  { background: #3a3a3c; }
footer: 'WordleApp — C# / .NET 8 Terminal UI'
---

<!-- _class: lead -->
<!-- _paginate: false -->

# WordleApp

### Ein aesthetisches Wordle-Klon als Terminal User Interface

**C# · .NET 8 · Spectre.Console**

<div class="tiles">
  <div class="tile correct">W</div>
  <div class="tile present">O</div>
  <div class="tile absent">R</div>
  <div class="tile correct">D</div>
  <div class="tile present">L</div>
</div>

---

## Überblick

**WordleApp** ist ein vollständiger Klon des Spiels *Wordle*, gebaut als
**Terminal User Interface (TUI)** in C# / .NET 8.

- Läuft komplett in der Konsole — dank **Spectre.Console** sieht es trotzdem
  poliert aus: Figlet-Banner, abgerundete Panels, farbige Kacheln
- **Zwei Sprachen** (Deutsch/Englisch), beliebig erweiterbar per JSON
- **Drei Schwierigkeitsgrade** mit eigenen Regeln & Punktemultiplikatoren
- **Externe Wortquelle** über eine Zufallswort-API + optionale
  Wörterbuch-Validierung
- **Persistente Einstellungen & Bestenliste** in JSON-Dateien
- **Adaptives Layout**, das sich live an die Terminalgröße anpasst

---

## Kern-Features

| Feature | Beschreibung |
|---|---|
| 🎮 Spielmodi | Easy / Normal / Hard — unterschiedliche Regeln & Punktemultiplikatoren |
| 🌐 Mehrsprachig | Deutsch & Englisch, neue Sprachen ohne Codeänderung |
| 🏆 Bestenliste | Kumulierte Statistik pro Spieler (Siege, Streak, Bestzeit) |
| 🎨 Adaptive UI | Kachelgröße & Layout passen sich live an Fenstergröße an |
| 🌍 Live-Wortquelle | Zufallswort-API + optionale Wörterbuch-Validierung |
| ⚙️ Konfigurierbar | Farben, Rundenlänge, API-Endpunkte über `config.json` |

---

## Architektur — Strikte Schichtentrennung

| Schicht | Ordner | Zuständigkeit | Konsolen-Zugriff |
|---|---|---|---|
| **Models** | `Models/` | Reine Datenstrukturen, keine Logik | Nein |
| **Core** | `Core/` | Spielregeln, Zustandsmaschinen, Menüsteuerung | Nein |
| **Services** | `Services/` | Dateizugriff, HTTP, Übersetzung | Nein |
| **UI** | `UI/` | **Einzige** Schicht mit `Spectre.Console` | **Ja, exklusiv** |

> *"Never mix console output with game logic. All drawing must happen in
> `FrontendRenderer.cs`."* — Projekt-Leitprinzip

---

## Programmablauf (Lifecycle)

```
Program.Main()
   ├─ ConfigService.Load()            → GameConfig
   ├─ LocalizationService(...)
   ├─ ConsoleSession.Start()          → Terminal vorbereiten
   └─ AppShell.Run()
        ├─ WaitForUsableWindow()
        ├─ WordPool.WarmUp(...)       → Wörter im Hintergrund vorladen
        ├─ DrawLoadingScreen()
        └─ RunMainMenu()  ← Schleife
             ├─ Play        → WordleGame.Run(level)
             ├─ Leaderboard
             ├─ How to Play
             └─ Settings
```

Kein DI-Framework — alle Abhängigkeiten werden von Hand in `Program.cs`
verdrahtet.

---

## Die kniffligste Logik: `WordEvaluator`

Der klassische Wordle-**Doppelbuchstaben-Fall**: Enthält das Zielwort einen
Buchstaben nur einmal, der Guess ihn aber zweimal, darf nur **eine** Stelle
als "vorhanden" markiert werden.

**Lösung: Zwei-Durchlauf-Algorithmus**

1. **Pass 1** — alle exakten Treffer (grün) markieren & Zielbuchstaben "verbrauchen"
2. **Pass 2** — für jeden Rest: kommt der Buchstabe *woanders* im (noch nicht
   verbrauchten) Zielwort vor? → gelb, sonst grau

Zielwort `GHOST`, Guess `SASSY` → nur das **erste** S wird markiert, obwohl
`SASSY` drei S enthält.

---

## Punkteberechnung

Drei Komponenten, in dieser Priorität:

1. **Grundpunkte fürs Gewinnen** — `1000` (nur bei Sieg)
2. **Bonus für ungenutzte Versuche** — `150` pro übrigem Versuch
3. **Geschwindigkeitsbonus** — linear von `600` auf `0` über die ersten `120s`

Die Summe wird mit dem **Schwierigkeits-Multiplikator** skaliert:

| Schwierigkeit | Multiplikator |
|---|---|
| Easy | 0,75× |
| Normal | 1,0× |
| Hard | 1,5× |

Eine verlorene Runde ist **immer 0 Punkte**, unabhängig von investierter Zeit.

---

## Externe Anbindung: `DictionaryApiService`

Zwei HTTP-Aufrufe, behandelt als *unzuverlässig und langsam*:

- **Random-Word-API** — liefert Zielwörter (via `WordPool`, im Hintergrund vorgeladen)
- **Dictionary-API** — validiert Rateversuche

**Robustheitsprinzipien:**

- **Eigene, enge Timeouts** pro Anfrage (Validierung braucht ein enges Zeitlimit)
- **"Fails open"** — nur ein explizites 404 lehnt einen Guess ab; Timeouts/Fehler
  lassen ihn durch
- Nach **2 Fehlschlägen** wird Validierung für die Sitzung deaktiviert
- Antworten werden **gecacht**, kein Wort wird zweimal abgefragt

---

## Adaptive Darstellung

`FrontendRenderer.cs` — die **einzige** Klasse, die `Spectre.Console` direkt
nutzt (>1100 Zeilen).

- **Adaptive Kachelgröße**: 6 Qualitätsstufen, größtmögliche Kachel wird gewählt
- **Live-Neuberechnung** bei Fenstergrößenänderung (Resize-Polling alle 40ms,
  da .NET kein natives Resize-Event kennt)
- **"Shimmer"-Animation**: graue Welle über die aktive Zeile während der
  Wörterbuch-Prüfung (`AnsiConsole.Live`, kein Flackern)
- **Fallback ohne Farbunterstützung**: Klammer-Notation `[[W]]` / `(O)` / ` X `
- **Markup-Escaping**: Spielernamen werden immer escaped, um Spectre-Markup-
  Injection zu verhindern

---

## Konfiguration & Robustheit

**`config.json`** — Sprache, Spielername, Schwierigkeit, Theme-Farben,
API-Endpunkte. Von Hand editierbar, Werte werden beim Laden geklemmt (`Clamp()`).

**Durchgängiges Prinzip: Nichts darf das Spiel zum Absturz bringen.**

| Situation | Verhalten |
|---|---|
| `config.json` fehlt/kaputt | Standardwerte |
| API nicht erreichbar | Fehlermeldung bzw. Validierung deaktiviert |
| Terminal zu klein | Eigener Hinweisbildschirm |
| Keine Farbunterstützung | Klammer-Notation |
| Absturz / Strg+C | Cursor & Farben werden trotzdem wiederhergestellt |

---

## Bekannte Grenzen

- **Keine automatisierten Tests** (obwohl Schlüsselmethoden `virtual` und
  damit testbar designt sind)
- **Kein Multiplayer**, keine Online-Bestenliste — rein lokal
- **Kein täglicher Wordle-Modus** — jedes Zielwort ist zufällig
- **Reine Flatfile-Persistenz** (JSON) statt Datenbank — für den Anwendungsfall
  ausreichend

---

<!-- _class: lead -->
<!-- _paginate: false -->

# Danke

**Vollständige technische Dokumentation:**
Repository → `docs/` (MkDocs-Seite)

**Quick Start:**
```bash
cd WordleApp
dotnet run
```
