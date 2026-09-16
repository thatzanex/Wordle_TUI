using WordleApp.Models;
using WordleApp.Services;

namespace WordleApp.Core;

// === SETTINGS EDITOR ===
// The state behind the in-game settings screen. It edits the live GameConfig, so
// every change is visible immediately: switching the language retranslates the
// interface and picking a colour repaints the tiles on the next frame.
// Leaving with ESC restores the snapshot taken when the editor was opened.

/// <summary>
/// Lets the player change language, board size and theme colours from inside the game.
/// </summary>
public class SettingsEditor
{
    /// <summary>
    /// What the editor wants the caller to do after a keystroke.
    /// </summary>
    public enum EditorResult
    {
        /// <summary>Stay in the settings screen.</summary>
        Continue,

        /// <summary>The configuration was written to disk; close the screen.</summary>
        Saved,

        /// <summary>The changes were discarded; close the screen.</summary>
        Cancelled,

        /// <summary>The player wants to change their name; show the name screen.</summary>
        EditName
    }

    // Colours offered for the theme, in the order the left and right arrows cycle
    // through them. All of them are names Spectre.Console understands.
    private static readonly string[] ThemePalette =
    {
        "green", "lime", "springgreen3", "teal", "cyan", "dodgerblue2", "blue",
        "purple", "magenta3", "red", "maroon", "orange3", "orange1", "gold3",
        "yellow", "white", "silver", "grey37", "grey23", "grey15", "black"
    };

    private readonly GameConfig config;
    private readonly LocalizationService localizer;
    private readonly ConfigService configService;
    private readonly GameConfig snapshot;
    private readonly SettingItem[] items;

    private int selectedIndex;

    /// <summary>
    /// Opens an editing session on the given configuration.
    /// </summary>
    /// <param name="config">The live configuration; it is edited in place.</param>
    /// <param name="localizer">Used to translate labels and to switch languages live.</param>
    /// <param name="configService">Used to write the configuration back to disk.</param>
    public SettingsEditor(GameConfig config, LocalizationService localizer, ConfigService configService)
    {
        this.config = config;
        this.localizer = localizer;
        this.configService = configService;

        snapshot = config.Clone();
        items = BuildItems();
    }

    /// <summary>Index of the highlighted row.</summary>
    public int SelectedIndex => selectedIndex;

    // === ROWS ===

    /// <summary>
    /// Builds the rows shown by the renderer, translated into the active language.
    /// </summary>
    /// <returns>One row per editable setting, followed by the two action rows.</returns>
    public IReadOnlyList<SettingsRow> BuildRows()
    {
        return items
            .Select(item => new SettingsRow(
                localizer.Get(item.LabelKey),
                item.ReadValue(),
                item.ReadSwatch()))
            .ToArray();
    }

    // === INPUT ===

    /// <summary>
    /// Applies a keystroke to the editor.
    /// </summary>
    /// <param name="key">The key the player pressed.</param>
    /// <returns>Whether the screen stays open, was saved, or was cancelled.</returns>
    public EditorResult HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
            case ConsoleKey.W:
                selectedIndex = (selectedIndex - 1 + items.Length) % items.Length;
                return EditorResult.Continue;

            case ConsoleKey.DownArrow:
            case ConsoleKey.S:
                selectedIndex = (selectedIndex + 1) % items.Length;
                return EditorResult.Continue;

            case ConsoleKey.LeftArrow:
            case ConsoleKey.A:
                items[selectedIndex].Change?.Invoke(-1);
                return EditorResult.Continue;

            case ConsoleKey.RightArrow:
            case ConsoleKey.D:
                items[selectedIndex].Change?.Invoke(1);
                return EditorResult.Continue;

            // Enter runs an action row, and cycles a value row forwards.
            case ConsoleKey.Enter:
            case ConsoleKey.Spacebar:
                var selected = items[selectedIndex];
                if (selected.Activate is not null)
                {
                    return selected.Activate();
                }

                selected.Change?.Invoke(1);
                return EditorResult.Continue;

            case ConsoleKey.Escape:
                Cancel();
                return EditorResult.Cancelled;

            default:
                return EditorResult.Continue;
        }
    }

    // === ROW DEFINITIONS ===

    // One entry per line of the settings screen. Value rows carry a Change
    // callback, action rows carry an Activate callback.
    private sealed record SettingItem(
        string LabelKey,
        Func<string> ReadValue,
        Func<string?> ReadSwatch,
        Action<int>? Change,
        Func<EditorResult>? Activate);

    private SettingItem[] BuildItems()
    {
        return new[]
        {
            new SettingItem(
                "settings.playerName",
                () => config.PlayerName,
                () => null,
                null,
                () => EditorResult.EditName),

            new SettingItem(
                "settings.language",
                () => localizer.GetLanguageName(config.Language),
                () => null,
                CycleLanguage,
                null),

            new SettingItem(
                "settings.wordLanguage",
                () => localizer.GetLanguageName(config.WordLanguage),
                () => null,
                CycleWordLanguage,
                null),

            new SettingItem(
                "settings.attempts",
                () => config.GameSettings.MaxAttempts.ToString(),
                () => null,
                direction => config.GameSettings.MaxAttempts = Cycle(
                    config.GameSettings.MaxAttempts,
                    direction,
                    GameSettings.MinimumAttempts,
                    GameSettings.MaximumAttempts),
                null),

            new SettingItem(
                "settings.wordLength",
                () => config.GameSettings.WordLength.ToString(),
                () => null,
                direction => config.GameSettings.WordLength = Cycle(
                    config.GameSettings.WordLength,
                    direction,
                    GameSettings.MinimumWordLength,
                    GameSettings.MaximumWordLength),
                null),

            new SettingItem(
                "settings.validation",
                () => localizer.Get(config.ApiEndpoints.ValidateGuesses ? "common.on" : "common.off"),
                () => null,
                _ => config.ApiEndpoints.ValidateGuesses = !config.ApiEndpoints.ValidateGuesses,
                null),

            BuildColorItem("settings.colorCorrect",
                () => config.Theme.ColorCorrect,
                value => config.Theme.ColorCorrect = value),

            BuildColorItem("settings.colorPresent",
                () => config.Theme.ColorPresent,
                value => config.Theme.ColorPresent = value),

            BuildColorItem("settings.colorAbsent",
                () => config.Theme.ColorAbsent,
                value => config.Theme.ColorAbsent = value),

            BuildColorItem("settings.colorEmpty",
                () => config.Theme.ColorEmpty,
                value => config.Theme.ColorEmpty = value),

            BuildColorItem("settings.colorTitle",
                () => config.Theme.AsciiTitleColor,
                value => config.Theme.AsciiTitleColor = value),

            new SettingItem(
                "settings.reset",
                () => string.Empty,
                () => null,
                null,
                ResetToDefaults),

            new SettingItem(
                "settings.save",
                () => string.Empty,
                () => null,
                null,
                Save)
        };
    }

    // A theme colour row: the value is the colour name, the swatch previews it.
    private SettingItem BuildColorItem(string labelKey, Func<string> read, Action<string> write)
    {
        return new SettingItem(
            labelKey,
            read,
            () => read(),
            direction => write(CycleColor(read(), direction)),
            null);
    }

    // === VALUE CHANGES ===

    // Steps to the next language in the folder and applies it right away, so the
    // settings screen itself is already translated when the next frame is drawn.
    private void CycleLanguage(int direction)
    {
        var languages = localizer.AvailableLanguages;
        if (languages.Count == 0)
        {
            return;
        }

        var current = languages.ToList().IndexOf(localizer.CurrentLanguage);
        var next = ((current < 0 ? 0 : current) + direction + languages.Count) % languages.Count;

        config.Language = languages[next];
        localizer.SetLanguage(config.Language);
    }

    // Steps to the next word language. This only changes which language the target
    // word and the dictionary use - the interface stays in whatever language
    // CycleLanguage set, so the two can differ.
    private void CycleWordLanguage(int direction)
    {
        var languages = localizer.AvailableLanguages;
        if (languages.Count == 0)
        {
            return;
        }

        var current = languages.ToList().IndexOf(config.WordLanguage);
        var next = ((current < 0 ? 0 : current) + direction + languages.Count) % languages.Count;

        config.WordLanguage = languages[next];
    }

    // Steps through the palette, wrapping around at both ends. A colour that is not
    // part of the palette (hand-edited config.json) enters the list at the front.
    private static string CycleColor(string current, int direction)
    {
        var index = Array.IndexOf(ThemePalette, current);
        if (index < 0)
        {
            return direction >= 0 ? ThemePalette[0] : ThemePalette[^1];
        }

        var next = (index + direction + ThemePalette.Length) % ThemePalette.Length;

        return ThemePalette[next];
    }

    // Steps a number within its bounds without wrapping around.
    private static int Cycle(int current, int direction, int minimum, int maximum)
    {
        return Math.Clamp(current + direction, minimum, maximum);
    }

    // === ACTIONS ===

    private EditorResult ResetToDefaults()
    {
        config.CopyFrom(new GameConfig());
        localizer.SetLanguage(config.Language);

        return EditorResult.Continue;
    }

    private EditorResult Save()
    {
        config.Clamp();
        configService.Save(config);

        return EditorResult.Saved;
    }

    private void Cancel()
    {
        config.CopyFrom(snapshot);
        localizer.SetLanguage(config.Language);
    }
}
