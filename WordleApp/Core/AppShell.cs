using WordleApp.Models;
using WordleApp.Services;
using WordleApp.UI;

namespace WordleApp.Core;

// === APPLICATION SHELL ===
// Owns everything around a round of Wordle: the first-run name prompt, the loading
// screen, the home menu, the difficulty picker and the auxiliary screens. Every
// screen follows the same pattern: draw, wait for input, redraw on resize.
// All drawing is delegated to FrontendRenderer; no console output happens here.

/// <summary>
/// Runs the outer application flow: loading screen, main menu and screen navigation.
/// </summary>
public class AppShell
{
    // Longest name the player may enter, so the panels keep their shape.
    private const int MaximumNameLength = 16;

    // Translation keys of the main menu entries, in display order. The labels
    // themselves are resolved on every frame so a language change is immediate.
    private static readonly string[] MenuKeys =
    {
        "menu.play",
        "menu.leaderboard",
        "menu.howToPlay",
        "menu.settings",
        "menu.quit"
    };

    private readonly GameConfig config;
    private readonly FrontendRenderer renderer;
    private readonly LocalizationService localizer;
    private readonly ConfigService configService;
    private readonly LeaderboardService leaderboard;
    private readonly DictionaryApiService api;
    private readonly WordPool words;
    private readonly InputReader input;

    private int selectedIndex;

    /// <summary>
    /// Creates the shell around the loaded configuration and its services.
    /// </summary>
    /// <param name="config">The live configuration; the settings screen edits it in place.</param>
    /// <param name="renderer">The renderer responsible for all console output.</param>
    /// <param name="localizer">Supplies the translated menu labels.</param>
    /// <param name="configService">Persists changes made in the settings screen.</param>
    /// <param name="leaderboard">Stores and ranks the player scores.</param>
    /// <param name="api">Validates guesses against the dictionary.</param>
    /// <param name="words">Buffers target words so a round starts without waiting.</param>
    /// <param name="input">Shared keyboard loop.</param>
    public AppShell(
        GameConfig config,
        FrontendRenderer renderer,
        LocalizationService localizer,
        ConfigService configService,
        LeaderboardService leaderboard,
        DictionaryApiService api,
        WordPool words,
        InputReader input)
    {
        this.config = config;
        this.renderer = renderer;
        this.localizer = localizer;
        this.configService = configService;
        this.leaderboard = leaderboard;
        this.api = api;
        this.words = words;
        this.input = input;
    }

    // === MAIN FLOW ===

    /// <summary>
    /// Starts the application: boot animation, name prompt on the first start, then
    /// the main menu until the user quits.
    /// </summary>
    public void Run()
    {
        // Without a keyboard attached (piped output, CI) the shell just draws one
        // frame so the layout can still be inspected, and then exits.
        if (Console.IsInputRedirected)
        {
            renderer.DrawHomeScreen(BuildMenuLabels(), selectedIndex, config.PlayerName);
            return;
        }

        // The boot animation would be clipped in a window that is too small, so the
        // size gate runs first.
        if (!WaitForUsableWindow())
        {
            return;
        }

        // The first words are downloaded while the boot animation plays, so the
        // first round does not have to wait for them either.
        words.WarmUp(config.GameSettings.WordLength, config.Language);

        renderer.DrawLoadingScreen();
        input.SyncWindowSize();

        // The very first start asks who is playing; the name is stored with the
        // settings, so this happens exactly once.
        if (config.PlayerName.Length == 0)
        {
            AskForName(isFirstRun: true);
        }

        RunMainMenu();
    }

    // === MAIN MENU ===

    // Draws the menu and reacts to keystrokes until the user picks "Quit" or presses ESC.
    private void RunMainMenu()
    {
        var needsRedraw = true;

        while (true)
        {
            if (needsRedraw)
            {
                DrawGuarded(() => renderer.DrawHomeScreen(BuildMenuLabels(), selectedIndex, config.PlayerName));
                needsRedraw = false;
            }

            var key = input.WaitForInput();

            // A null key means the window was resized: redraw at the new size.
            if (key is null)
            {
                needsRedraw = true;
                continue;
            }

            switch (key.Value.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                    selectedIndex = (selectedIndex - 1 + MenuKeys.Length) % MenuKeys.Length;
                    needsRedraw = true;
                    break;

                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                    selectedIndex = (selectedIndex + 1) % MenuKeys.Length;
                    needsRedraw = true;
                    break;

                case ConsoleKey.Enter:
                case ConsoleKey.Spacebar:
                    if (!ActivateSelection())
                    {
                        return;
                    }

                    needsRedraw = true;
                    break;

                case ConsoleKey.Escape:
                    return;
            }
        }
    }

    // Resolves the menu labels in the language that is active right now.
    private string[] BuildMenuLabels()
    {
        return MenuKeys.Select(localizer.Get).ToArray();
    }

    // Runs the screen behind the highlighted menu entry.
    // Returns false when the application should shut down.
    private bool ActivateSelection()
    {
        switch (MenuKeys[selectedIndex])
        {
            case "menu.play":
                StartGame();
                return true;

            case "menu.leaderboard":
                ShowStaticScreen(() => renderer.DrawLeaderboard(leaderboard.GetRanked(), config.PlayerName));
                return true;

            case "menu.howToPlay":
                ShowStaticScreen(renderer.DrawHelpScreen);
                return true;

            case "menu.settings":
                RunSettingsEditor();

                // Word length or language may have changed, which needs a new batch.
                words.WarmUp(config.GameSettings.WordLength, config.Language);

                return true;

            default:
                return false;
        }
    }

    // === PLAYING ===

    // Asks for the difficulty and hands control to the game engine.
    private void StartGame()
    {
        var level = ChooseDifficulty();
        if (level is null)
        {
            return;
        }

        // The choice is remembered so the picker opens on it next time.
        config.Difficulty = level.Value;
        configService.Save(config);

        var game = new WordleGame(config, renderer, localizer, api, words, leaderboard, input);
        game.Run(level.Value);
    }

    // The difficulty picker. Returns null when the player backed out with ESC.
    private Difficulty? ChooseDifficulty()
    {
        var levels = DifficultyRules.Levels;
        var index = Math.Max(0, levels.ToList().IndexOf(config.Difficulty));
        var needsRedraw = true;

        while (true)
        {
            if (needsRedraw)
            {
                var current = index;
                DrawGuarded(() => renderer.DrawDifficultyScreen(
                    levels.Select(level => localizer.Get(DifficultyRules.For(level).NameKey)).ToArray(),
                    current,
                    BuildDifficultyDescription(levels[current])));

                needsRedraw = false;
            }

            var key = input.WaitForInput();
            if (key is null)
            {
                needsRedraw = true;
                continue;
            }

            switch (key.Value.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                    index = (index - 1 + levels.Count) % levels.Count;
                    needsRedraw = true;
                    break;

                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                    index = (index + 1) % levels.Count;
                    needsRedraw = true;
                    break;

                case ConsoleKey.Enter:
                case ConsoleKey.Spacebar:
                    return levels[index];

                case ConsoleKey.Escape:
                    return null;
            }
        }
    }

    // Explains what a difficulty changes: guesses, extra rule and score multiplier.
    private string BuildDifficultyDescription(Difficulty level)
    {
        var rules = DifficultyRules.For(level);

        return localizer.Format(
            rules.DescriptionKey,
            rules.ResolveAttempts(config.GameSettings.MaxAttempts),
            rules.ScoreMultiplier.ToString("0.##"));
    }

    // === NAME ===

    // Runs the name screen. The name is part of the settings file, so it is saved
    // right away, and existing scores follow the player to the new name.
    private void AskForName(bool isFirstRun)
    {
        var typed = config.PlayerName;
        var needsRedraw = true;

        while (true)
        {
            if (needsRedraw)
            {
                var current = typed;
                DrawGuarded(() => renderer.DrawNamePrompt(current, isFirstRun));
                needsRedraw = false;
            }

            var key = input.WaitForInput();
            if (key is null)
            {
                needsRedraw = true;
                continue;
            }

            needsRedraw = true;

            switch (key.Value.Key)
            {
                case ConsoleKey.Enter:
                    var name = typed.Trim();
                    if (name.Length == 0)
                    {
                        // An empty name would leave the leaderboard without a label,
                        // so the screen simply stays open until something is typed.
                        continue;
                    }

                    leaderboard.Rename(config.PlayerName, name);
                    config.PlayerName = name;
                    configService.Save(config);

                    return;

                case ConsoleKey.Escape:
                    // Backing out is only allowed once a name exists.
                    if (config.PlayerName.Length > 0)
                    {
                        return;
                    }

                    continue;

                case ConsoleKey.Backspace:
                    if (typed.Length > 0)
                    {
                        typed = typed[..^1];
                    }

                    continue;

                default:
                    // Letters, digits and spaces make a name; control keys do not.
                    var character = key.Value.KeyChar;
                    if (typed.Length < MaximumNameLength && (char.IsLetterOrDigit(character) || character == ' '))
                    {
                        typed += character;
                    }

                    continue;
            }
        }
    }

    // === SETTINGS ===

    // Runs the settings screen until the player saves or cancels. The editor works
    // on the live configuration, so every keystroke is visible on the next frame:
    // a new language retranslates the screen, a new colour repaints it.
    private void RunSettingsEditor()
    {
        var editor = new SettingsEditor(config, localizer, configService);
        var needsRedraw = true;

        while (true)
        {
            if (needsRedraw)
            {
                DrawGuarded(() => renderer.DrawSettingsScreen(editor.BuildRows(), editor.SelectedIndex));
                needsRedraw = false;
            }

            var key = input.WaitForInput();
            if (key is null)
            {
                needsRedraw = true;
                continue;
            }

            var result = editor.HandleKey(key.Value);
            needsRedraw = true;

            switch (result)
            {
                case SettingsEditor.EditorResult.Continue:
                    continue;

                case SettingsEditor.EditorResult.EditName:
                    AskForName(isFirstRun: false);
                    continue;

                default:
                    return;
            }
        }
    }

    // === SCREEN HELPERS ===

    // Displays a screen until any key is pressed, redrawing it whenever the window changes.
    private void ShowStaticScreen(Action draw)
    {
        var needsRedraw = true;

        while (true)
        {
            if (needsRedraw)
            {
                DrawGuarded(draw);
                needsRedraw = false;
            }

            if (input.WaitForInput() is null)
            {
                needsRedraw = true;
                continue;
            }

            return;
        }
    }

    // Blocks the size gate until the window is big enough, or the user gives up.
    // Returns false when the user pressed ESC instead of resizing.
    private bool WaitForUsableWindow()
    {
        while (!renderer.IsTerminalLargeEnough())
        {
            renderer.DrawTerminalTooSmall();

            var key = input.WaitForInput();
            if (key?.Key == ConsoleKey.Escape)
            {
                return false;
            }
        }

        return true;
    }

    // Draws a screen, or the "window too small" notice when it would not fit.
    private void DrawGuarded(Action draw)
    {
        if (renderer.IsTerminalLargeEnough())
        {
            draw();
            return;
        }

        renderer.DrawTerminalTooSmall();
    }
}
