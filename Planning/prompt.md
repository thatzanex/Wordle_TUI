# Project Plan: C# .NET Wordle TUI Clone

## 1. Project Overview
This project is a highly polished, aesthetic Terminal User Interface (TUI) clone of the game Wordle. It is built in C# using modern .NET (8.0+) and relies heavily on `Spectre.Console` for rendering. The architecture strictly separates game logic, visual rendering, configuration, and API data fetching.

## 2. Core Principles & AI Instructions
As the AI agent building this project, you must adhere to the following rules:

* **Aesthetics First:** The console app must look beautiful. Use a black background, with Orange (accent/present), Green (correct), and Dark Grey (absent/empty) as the core palette. Make heavy use of `Spectre.Console` features: `FigletText` for ASCII headers, `Panel` for borders, `Table` for grids, and `Progress` for loading screens.
* **Separation of Concerns:** Never mix console output (`AnsiConsole.Write`) with game logic. All drawing must happen in `FrontendRenderer.cs`.
* **Customizability:** Hardcode as little as possible. Game rules (max attempts, colors, API URLs) must be loaded from a `config.json` file.
* **Code Commenting Standards:** 
  * Use "Title Headers" for distinct code sections (e.g., `// === INITIALIZATION ===`).
  * Provide descriptive comments above complex logic (especially the Wordle double-letter evaluation rule).
  * Use standard XML docstrings (`///`) for public methods.
* **Simple Naming:** Use clear, unambiguous, C#-standard PascalCase for classes/methods and camelCase for variables.

### Example of required commenting style:
```csharp
// === GAME LOOP ===
public void Run() 
{
    // ...
}

// Evaluates the guess against the target word.
// Note: Handles the double-letter edge case by tracking which target letters have already been matched.
private LetterState[] EvaluateGuess(string guess, string target) 
{
    // ...
}

```

## 3. Architecture & File Structure

The project should be organized into the following distinct files:

1. `Program.cs` - The entry point. Loads the config, initializes the loading screen, and starts the game loop.
2. `Models/GameConfig.cs` - C# class representing the JSON configuration.
3. `Models/WordleGuess.cs` - Data structure holding a submitted word and its `LetterState` array.
4. `Core/WordleGame.cs` - The game engine. Handles state, input tracking, and win/loss conditions.
5. `Core/WordEvaluator.cs` - Pure logic class. Compares a guess to the target word and returns the states.
6. `UI/FrontendRenderer.cs` - Exclusively uses `Spectre.Console` to draw grids, text, panels, and ASCII art.
7. `Services/DictionaryApiService.cs` - Handles `HttpClient` calls to fetch the daily word and validate guesses.
8. `config.json` - The customizable settings file.

## 4. Configuration Schema (`config.json`)

The application must parse this file on startup:

```json
{
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
    "RandomWord": "[https://random-word-api.herokuapp.com/word?length=5](https://random-word-api.herokuapp.com/word?length=5)",
    "DictionaryValidation": "[https://api.dictionaryapi.dev/api/v2/entries/en/](https://api.dictionaryapi.dev/api/v2/entries/en/)"
  }
}

```

## 5. Development Timeline (Month-by-Month)

Even though this is a contained project, development is split into 4 distinct phases (Months) to ensure quality and testability at every step. Do not skip ahead.

### Month 1: Foundation & TUI Skeleton

* **Goal:** Setup the project, config system, and draw the static UI.
* **Tasks:**
* Initialize the .NET Console project and install `Spectre.Console` and `Microsoft.Extensions.Configuration.Json`.
* Create the folder structure and empty class files.
* Implement `GameConfig.cs` and the logic to read `config.json` on startup.
* Build `FrontendRenderer.cs` to draw a dummy 6x5 grid inside an orange Panel with an ASCII `FigletText` header.



### Month 2: Game Logic & Input Handling

* **Goal:** Make the game playable with hardcoded words.
* **Tasks:**
* Implement the keystroke listener in `WordleGame.cs` (capturing A-Z, Backspace, Enter). Force all input to uppercase.
* Wire the input buffer to `FrontendRenderer.cs` so the user sees their letters appearing on the active row.
* Implement `WordEvaluator.cs`. **Crucial:** Ensure the logic correctly handles double letters (e.g., if target is "GHOST" and guess is "SASSY", only the first 'S' is yellow, the second is grey).
* Wire up the Win/Loss conditions and the end screen prompt to play again.



### Month 3: API Integration & Validation

* **Goal:** Connect the game to the real world.
* **Tasks:**
* Set up `DictionaryApiService.cs` using `HttpClient`.
* Replace the hardcoded target word with a fetch call to the Random Word API.
* Implement guess validation: Before allowing the user to submit a row, call the Dictionary API. If it returns a 404, the word is invalid.
* Add a transient visual warning in the TUI (e.g., a red text flash saying "Not in word list") if validation fails.



### Month 4: Polish, Aesthetics & Animations

* **Goal:** Elevate the console app to a premium experience.
* **Tasks:**
* Implement a fake "Booting Engine" loading screen using `AnsiConsole.Progress()`.
* Refine the ASCII art and borders. Ensure the terminal looks good regardless of the user's terminal window size.
* Add full code documentation (XML docstrings) to all public methods.
* Perform a final test pass to ensure no terminal output leakage (cursor should be hidden, raw keystrokes shouldn't print outside the grid).



## 6. Execution Instructions for AI

To begin, acknowledge this plan. Then, start by executing **Month 1**. Do not proceed to Month 2 until Month 1 is fully coded, functional, and approved by the user. Ensure all code output strictly follows the commenting and aesthetic guidelines established in Section 2.