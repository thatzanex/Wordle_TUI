using Spectre.Console;
using Spectre.Console.Rendering;
using WordleApp.Models;
using WordleApp.Services;

namespace WordleApp.UI;

// === FRONTEND RENDERER ===
// The single place in the application that is allowed to talk to the console.
// Game logic hands this class plain data (a BoardView, a RoundResult, menu
// entries) and gets a fully drawn screen in return. Nothing here mutates game
// state, and no user-visible string is hardcoded: every text comes from the
// localizer.

/// <summary>
/// Draws every screen of the game with Spectre.Console: loading, home, difficulty,
/// board, finish, leaderboard, help and settings.
/// </summary>
public class FrontendRenderer
{
    // --- Tile scaling ---
    // A tile is a solid colour block whose size adapts to the terminal window.
    // The steps are ordered from largest to smallest; the first one that fits into
    // the current window is used, which makes the board grow and shrink on resize.
    // The first entry doubles as the maximum size, the last one as the minimum.
    private static readonly TileScale[] TileScales =
    {
        new TileScale(11, 5),
        new TileScale(9, 5),
        new TileScale(7, 3),
        new TileScale(5, 3),
        new TileScale(5, 1),
        new TileScale(3, 1)
    };

    // --- Checking animation ---
    // Shades of grey used for the wave that runs through the active row while a
    // guess is being checked. Brightest first: the crest of the wave moves one
    // column further to the right with every frame.
    private static readonly string[] ShimmerShades =
    {
        "grey58", "grey46", "grey37", "grey30", "grey23", "grey19", "grey15", "grey11"
    };

    // Milliseconds between two frames of that wave.
    private const int ShimmerFrameMilliseconds = 80;

    // Horizontal gap between two tiles, in characters.
    private const int ColumnGap = 1;

    // Panel chrome: left and right border plus the horizontal padding inside it.
    private const int PanelHorizontalChrome = 6;

    // Panel chrome: top and bottom border plus the vertical padding inside it.
    private const int PanelVerticalChrome = 4;

    // Lines used outside the panel on a plain screen: one leading blank line, one
    // blank line above the footer, and the footer itself.
    private const int SurroundingChromeHeight = 3;

    // The board screen additionally reserves a status line and a message line, so
    // the layout does not jump when a warning appears.
    private const int BoardChromeHeight = 5;

    // Lines used by the on-screen keyboard: a blank line plus its three rows.
    private const int KeyboardHeight = 4;

    // Narrowest window that still fits the widest keyboard row.
    private const int MinimumWidthForKeyboard = 42;

    // Height of the Figlet banner including the blank line underneath it.
    private const int FigletBannerHeight = 7;

    // Height of the one-line fallback banner including its blank line.
    private const int CompactBannerHeight = 2;

    // Minimum terminal width required for the Figlet banner.
    private const int MinimumWidthForFiglet = 60;

    private readonly GameConfig config;
    private readonly LocalizationService localizer;

    /// <summary>
    /// Creates a renderer bound to the configuration and the translation service.
    /// </summary>
    /// <param name="config">The live configuration; theme changes are picked up on the next frame.</param>
    /// <param name="localizer">Supplies every user-visible string.</param>
    public FrontendRenderer(GameConfig config, LocalizationService localizer)
    {
        this.config = config;
        this.localizer = localizer;
    }

    // === TERMINAL SIZE ===

    /// <summary>
    /// Smallest terminal size the board still fits into, in characters. Easy mode
    /// grants one extra row, so that row is included in the requirement.
    /// </summary>
    public (int Width, int Height) MinimumTerminalSize
    {
        get
        {
            var smallest = new BoardLayout(false, false, false, TileScales[TileScales.Length - 1]);
            var rows = config.GameSettings.MaxAttempts + 1;

            return (RequiredWidth(smallest), RequiredHeight(smallest, rows, 0));
        }
    }

    /// <summary>
    /// True when the current terminal window is large enough to draw the board.
    /// </summary>
    public bool IsTerminalLargeEnough()
    {
        SyncProfileWithWindow();

        var minimum = MinimumTerminalSize;

        return AnsiConsole.Profile.Width >= minimum.Width
            && AnsiConsole.Profile.Height >= minimum.Height;
    }

    // === BACKGROUND WORK ===

    /// <summary>
    /// Runs a slow piece of work behind a spinner, for example an API call.
    /// </summary>
    /// <typeparam name="T">Type the work returns.</typeparam>
    /// <param name="messageKey">Translation key of the line shown next to the spinner.</param>
    /// <param name="work">The work to run.</param>
    /// <returns>Whatever the work returned.</returns>
    public T RunWithStatus<T>(string messageKey, Func<T> work)
    {
        // Without a terminal there is nothing to animate, so the work just runs.
        if (Console.IsOutputRedirected)
        {
            return work();
        }

        BeginFrame();
        AnsiConsole.Write(BuildBannerForContent(2));

        var result = default(T)!;

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(new Style(ParseColor(config.Theme.ColorPresent)))
            .Start(Dim(localizer.Get(messageKey)), _ => result = work());

        return result;
    }

    // === PUBLIC DRAWING API ===

    /// <summary>
    /// Plays a short "booting" animation before the first real screen appears.
    /// </summary>
    public void DrawLoadingScreen()
    {
        // Lines below the banner: one row per boot step plus the READY line.
        BeginFrame();
        AnsiConsole.Write(BuildBannerForContent(6));

        // Each entry is a fake unit of work; together they fill roughly one second.
        var steps = new[]
        {
            "loading.step.config",
            "loading.step.palette",
            "loading.step.words",
            "loading.step.engine"
        };

        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new SpinnerColumn(Spinner.Known.Dots) { Style = new Style(ParseColor(config.Theme.ColorPresent)) },
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new ProgressBarColumn
                {
                    CompletedStyle = new Style(ParseColor(config.Theme.ColorPresent)),
                    FinishedStyle = new Style(ParseColor(config.Theme.ColorCorrect)),
                    RemainingStyle = new Style(ParseColor(config.Theme.ColorEmpty))
                },
                new PercentageColumn())
            .Start(context =>
            {
                foreach (var step in steps)
                {
                    var task = context.AddTask(Dim(localizer.Get(step)));
                    while (!task.IsFinished)
                    {
                        task.Increment(12.5);
                        Thread.Sleep(30);
                    }
                }
            });

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Markup(
            $"[bold {config.Theme.ColorCorrect}]{Escape(localizer.Get("loading.ready"))}[/]").Centered());
        AnsiConsole.WriteLine();
        Thread.Sleep(350);
    }

    /// <summary>
    /// Draws the title screen with the main menu.
    /// </summary>
    /// <param name="menuItems">The translated entries, top to bottom.</param>
    /// <param name="selectedIndex">Index of the currently highlighted entry.</param>
    /// <param name="playerName">Name of the current player, shown above the menu.</param>
    public void DrawHomeScreen(IReadOnlyList<string> menuItems, int selectedIndex, string playerName)
    {
        // Lines below the banner: tagline, greeting, two blank lines, menu panel.
        var contentHeight = 4 + menuItems.Count + PanelVerticalChrome;

        BeginFrame();
        AnsiConsole.Write(BuildBannerForContent(contentHeight));

        AnsiConsole.Write(Align.Center(new Markup(Dim(localizer.Format(
            "app.tagline",
            config.GameSettings.MaxAttempts,
            config.GameSettings.WordLength)))));
        AnsiConsole.WriteLine();

        if (playerName.Length > 0)
        {
            AnsiConsole.Write(Align.Center(new Markup(
                $"[{config.Theme.ColorAbsent}]{Escape(localizer.Get("home.greeting"))} [/]" +
                $"[bold {config.Theme.ColorPresent}]{Escape(playerName)}[/]")));
            AnsiConsole.WriteLine();
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(Align.Center(BuildSelectionPanel(menuItems, selectedIndex, localizer.Get("menu.header"))));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildHint("hint.home.long", "hint.home.short"));
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Draws the difficulty picker shown before a round starts.
    /// </summary>
    /// <param name="levelNames">The translated difficulty names.</param>
    /// <param name="selectedIndex">Index of the highlighted difficulty.</param>
    /// <param name="description">Description of the highlighted difficulty.</param>
    public void DrawDifficultyScreen(IReadOnlyList<string> levelNames, int selectedIndex, string description)
    {
        var contentHeight = 4 + levelNames.Count + PanelVerticalChrome;

        BeginFrame();
        AnsiConsole.Write(BuildBannerForContent(contentHeight));
        AnsiConsole.Write(Align.Center(BuildSelectionPanel(
            levelNames, selectedIndex, localizer.Get("difficulty.header"))));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(Align.Center(new Markup(Dim(description))));
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildHint("hint.difficulty.long", "hint.difficulty.short"));
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Draws the running round: banner, tile grid, status line and keyboard.
    /// </summary>
    /// <param name="view">Snapshot of the round to draw.</param>
    public void DrawBoard(BoardView view)
    {
        BeginFrame();
        AnsiConsole.Write(BuildBoardScreen(view));
    }

    /// <summary>
    /// Keeps the board on screen while a guess is checked, animating the active row
    /// as a grey wave instead of switching to a separate waiting screen.
    /// </summary>
    /// <typeparam name="T">Type the check returns.</typeparam>
    /// <param name="view">The board as it looks while the guess is pending.</param>
    /// <param name="work">Starts the check and returns its task.</param>
    /// <returns>Whatever the check returned.</returns>
    public T RunWhileCheckingGuess<T>(BoardView view, Func<Task<T>> work)
    {
        var pending = work();

        // Without a terminal there is nothing to animate, so the check just runs.
        if (Console.IsOutputRedirected)
        {
            return pending.GetAwaiter().GetResult();
        }

        BeginFrame();

        var frame = 0;

        // Live redraws in place, so the wave animates without the flicker that a
        // full clear on every frame would cause.
        AnsiConsole.Live(BuildBoardScreen(view with { ShimmerFrame = frame }))
            .AutoClear(false)
            .Start(context =>
            {
                while (!pending.IsCompleted)
                {
                    context.UpdateTarget(BuildBoardScreen(view with { ShimmerFrame = ++frame }));
                    context.Refresh();
                    Thread.Sleep(ShimmerFrameMilliseconds);
                }
            });

        return pending.GetAwaiter().GetResult();
    }

    // The whole board screen as a single renderable: banner, grid, status line,
    // message line, keyboard and hint. Building it in one piece is what lets the
    // checking animation refresh it in place.
    private IRenderable BuildBoardScreen(BoardView view)
    {
        var layout = MeasureLayout(view.AttemptsAllowed, 0);

        var parts = new List<IRenderable>
        {
            BuildBanner(layout.ShowFiglet),
            Align.Center(BuildFramedPanel(
                BuildGrid(view.Guesses, view.CurrentInput, view.AttemptsAllowed, layout, view),
                localizer.Get("board.header"))),

            // Status and message keep a line of their own even when the message is
            // empty, so the board does not jump around while playing.
            BuildStatusLine(view),
            BuildMessageLine(view.Message)
        };

        if (layout.ShowKeyboard)
        {
            parts.Add(Text.Empty);
            parts.Add(BuildKeyboard(view.KeyboardStates));
        }

        parts.Add(Text.Empty);
        parts.Add(BuildHint("hint.board.long", "hint.board.short"));

        return new Rows(parts);
    }

    /// <summary>
    /// Draws the end of a round: the outcome, the word, the time and the points.
    /// </summary>
    /// <param name="result">The finished round.</param>
    /// <param name="guesses">The guesses that were made, shown above the summary when there is room.</param>
    /// <param name="entry">The player's leaderboard entry after booking the round.</param>
    public void DrawFinishScreen(RoundResult result, IReadOnlyList<WordleGuess> guesses, LeaderboardEntry entry)
    {
        // The summary panel is what matters here, so the grid above it is only kept
        // while the window can fit both.
        var summaryHeight = 7 + PanelVerticalChrome;
        var layout = MeasureLayout(result.AttemptsAllowed, summaryHeight);
        var showBoard = FitsWithSummary(layout, result.AttemptsAllowed, summaryHeight);

        BeginFrame();
        AnsiConsole.Write(BuildBanner(layout.ShowFiglet && showBoard));

        if (showBoard)
        {
            AnsiConsole.Write(Align.Center(BuildFramedPanel(
                BuildGrid(guesses, string.Empty, result.AttemptsAllowed, layout),
                localizer.Get("board.header"))));
            AnsiConsole.WriteLine();
        }

        AnsiConsole.Write(Align.Center(BuildFramedPanel(
            BuildSummary(result, entry),
            localizer.Get(result.Won ? "finish.won.header" : "finish.lost.header"))));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildHint("hint.finish.long", "hint.finish.short"));
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Draws the ranked leaderboard.
    /// </summary>
    /// <param name="entries">The entries, best first.</param>
    /// <param name="currentPlayer">Name of the current player, highlighted in the table.</param>
    public void DrawLeaderboard(IReadOnlyList<LeaderboardEntry> entries, string currentPlayer)
    {
        var contentHeight = Math.Max(3, entries.Count + 3) + PanelVerticalChrome;

        BeginFrame();
        AnsiConsole.Write(BuildBannerForContent(contentHeight));

        if (entries.Count == 0)
        {
            var empty = new Grid();
            empty.AddColumn(new GridColumn().NoWrap());
            empty.AddRow(new Markup(Dim(localizer.Get("leaderboard.empty"))));

            AnsiConsole.Write(Align.Center(BuildFramedPanel(empty, localizer.Get("leaderboard.header"))));
        }
        else
        {
            AnsiConsole.Write(Align.Center(BuildLeaderboardTable(entries, currentPlayer)));
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildHint("hint.any.long", "hint.any.short"));
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Draws the name entry screen.
    /// </summary>
    /// <param name="currentInput">The name typed so far.</param>
    /// <param name="isFirstRun">True on the very first start, which shows a welcome line.</param>
    public void DrawNamePrompt(string currentInput, bool isFirstRun)
    {
        BeginFrame();
        AnsiConsole.Write(BuildBannerForContent(5 + PanelVerticalChrome));

        var body = new Grid();
        body.AddColumn(new GridColumn().NoWrap());
        body.AddRow(new Markup(Dim(localizer.Get(isFirstRun ? "name.welcome" : "name.prompt"))));
        body.AddEmptyRow();

        // The trailing block is a stand-in cursor: the real one stays hidden.
        var typed = currentInput.Length > 0 ? Escape(currentInput) : string.Empty;
        body.AddRow(new Markup($"[bold white on {config.Theme.ColorEmpty}]  {typed}█  [/]"));

        AnsiConsole.Write(Align.Center(BuildFramedPanel(body, localizer.Get("name.header"))));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildHint("hint.name.long", "hint.name.short"));
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Draws the rules screen, including colour-coded example tiles.
    /// </summary>
    public void DrawHelpScreen()
    {
        // The examples use flat one-line tiles so the screen stays compact.
        var exampleScale = new TileScale(MeasureLayout(config.GameSettings.MaxAttempts, 0).Scale.Width, 1);

        // Lines below the banner: intro, a blank line, three examples separated by
        // blank lines, the scoring note, and the panel chrome around them.
        var contentHeight = 9 + PanelVerticalChrome;

        BeginFrame();
        AnsiConsole.Write(BuildBannerForContent(contentHeight));

        var examples = new Grid();
        examples.AddColumn(new GridColumn().NoWrap().PadRight(2));
        examples.AddColumn(new GridColumn());
        examples.AddRow(
            BuildTile('W', LetterState.Correct, exampleScale),
            new Markup(Dim(localizer.Get("help.correct"))));
        examples.AddEmptyRow();
        examples.AddRow(
            BuildTile('O', LetterState.Present, exampleScale),
            new Markup(Dim(localizer.Get("help.present"))));
        examples.AddEmptyRow();
        examples.AddRow(
            BuildTile('X', LetterState.Absent, exampleScale),
            new Markup(Dim(localizer.Get("help.absent"))));

        var body = new Grid();
        body.AddColumn(new GridColumn());
        body.AddRow(new Markup(Dim(localizer.Format(
            "help.intro",
            config.GameSettings.WordLength,
            config.GameSettings.MaxAttempts))));
        body.AddEmptyRow();
        body.AddRow(examples);
        body.AddEmptyRow();
        body.AddRow(new Markup(Dim(localizer.Get("help.scoring"))));

        AnsiConsole.Write(Align.Center(BuildFramedPanel(body, localizer.Get("help.header"))));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildHint("hint.any.long", "hint.any.short"));
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Draws the editable settings screen.
    /// </summary>
    /// <param name="rows">The rows to display, already translated.</param>
    /// <param name="selectedIndex">Index of the row the player is editing.</param>
    public void DrawSettingsScreen(IReadOnlyList<SettingsRow> rows, int selectedIndex)
    {
        var contentHeight = rows.Count + PanelVerticalChrome;

        BeginFrame();
        AnsiConsole.Write(BuildBannerForContent(contentHeight));
        AnsiConsole.Write(Align.Center(BuildFramedPanel(
            BuildSettingsGrid(rows, selectedIndex),
            localizer.Get("settings.header"))));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildHint("hint.settings.long", "hint.settings.short"));
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Draws a standalone message, used when something went wrong.
    /// </summary>
    /// <param name="headerKey">Translation key of the panel header.</param>
    /// <param name="messageKey">Translation key of the message itself.</param>
    /// <param name="detail">Optional technical detail, shown dimmed underneath.</param>
    public void DrawMessageScreen(string headerKey, string messageKey, string? detail = null)
    {
        BeginFrame();
        AnsiConsole.Write(BuildBannerForContent(4 + PanelVerticalChrome));

        var body = new Grid();
        body.AddColumn(new GridColumn().NoWrap());
        body.AddRow(new Markup($"[bold {config.Theme.ColorPresent}]{Escape(localizer.Get(messageKey))}[/]"));

        if (!string.IsNullOrWhiteSpace(detail))
        {
            body.AddEmptyRow();
            body.AddRow(new Markup(Dim(Shorten(detail, 60))));
        }

        AnsiConsole.Write(Align.Center(BuildFramedPanel(body, localizer.Get(headerKey))));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildHint("hint.any.long", "hint.any.short"));
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Draws the notice shown while the terminal window is too small for the board.
    /// </summary>
    public void DrawTerminalTooSmall()
    {
        var minimum = MinimumTerminalSize;

        BeginFrame();

        var message = new Grid();
        message.AddColumn(new GridColumn().NoWrap());
        message.AddRow(new Markup(
            $"[bold {config.Theme.ColorPresent}]{Escape(localizer.Get("size.tooSmall"))}[/]"));
        message.AddEmptyRow();
        message.AddRow(new Markup(
            $"{Dim(localizer.Get("size.needed"))} [white]{minimum.Width} x {minimum.Height}[/]"));
        message.AddRow(new Markup(
            $"{Dim(localizer.Get("size.current"))} [white]{AnsiConsole.Profile.Width} x {AnsiConsole.Profile.Height}[/]"));
        message.AddEmptyRow();
        message.AddRow(new Markup(Dim(localizer.Get("size.resize"))));

        AnsiConsole.Write(Align.Center(BuildFramedPanel(message, "WORDLE")));
    }

    // === FRAME SETUP ===

    // Prepares the console for a fresh frame: picks up the current window size,
    // wipes the previous frame and keeps the cursor hidden.
    private void BeginFrame()
    {
        SyncProfileWithWindow();

        // Clearing needs a real terminal buffer; when the output is piped we simply
        // append the frame instead of wiping the screen.
        if (!Console.IsOutputRedirected)
        {
            AnsiConsole.Clear();
            AnsiConsole.Cursor.Hide();
        }

        AnsiConsole.WriteLine();
    }

    // Spectre caches the terminal size in its profile, so it is refreshed on every
    // frame. This is what makes the layout follow the window while it is resized.
    private static void SyncProfileWithWindow()
    {
        if (Console.IsOutputRedirected)
        {
            return;
        }

        try
        {
            AnsiConsole.Profile.Width = Math.Max(1, Console.WindowWidth);
            AnsiConsole.Profile.Height = Math.Max(1, Console.WindowHeight);
        }
        catch (IOException)
        {
            // No real window attached; keep whatever Spectre detected at startup.
        }
    }

    // === LAYOUT ===

    // A single tile size: how many characters a tile spans horizontally and how
    // many lines it spans vertically.
    private readonly record struct TileScale(int Width, int Height);

    // The layout chosen for the current window size.
    private readonly record struct BoardLayout(bool ShowFiglet, bool UseRowSpacing, bool ShowKeyboard, TileScale Scale);

    // Picks the richest layout that still fits the current window. Quality is given
    // up in a fixed order: first the ASCII banner, then the keyboard, then the blank
    // lines between the rows. Within each step the largest tiles that fit are used.
    private BoardLayout MeasureLayout(int rows, int extraContentHeight)
    {
        var preferences = new[]
        {
            (ShowFiglet: true, UseRowSpacing: true, ShowKeyboard: true),
            (ShowFiglet: false, UseRowSpacing: true, ShowKeyboard: true),
            (ShowFiglet: false, UseRowSpacing: true, ShowKeyboard: false),
            (ShowFiglet: false, UseRowSpacing: false, ShowKeyboard: false)
        };

        foreach (var preference in preferences)
        {
            foreach (var scale in TileScales)
            {
                var candidate = new BoardLayout(
                    preference.ShowFiglet, preference.UseRowSpacing, preference.ShowKeyboard, scale);

                if (Fits(candidate, rows, extraContentHeight))
                {
                    return candidate;
                }
            }
        }

        // Nothing fits; fall back to the smallest layout. In practice the caller
        // shows the "window too small" notice long before this point is reached.
        return new BoardLayout(false, false, false, TileScales[TileScales.Length - 1]);
    }

    // True when a layout fits into the current window, banner and keyboard included.
    private bool Fits(BoardLayout layout, int rows, int extraContentHeight)
    {
        if (layout.ShowFiglet && AnsiConsole.Profile.Width < MinimumWidthForFiglet)
        {
            return false;
        }

        if (layout.ShowKeyboard && AnsiConsole.Profile.Width < MinimumWidthForKeyboard)
        {
            return false;
        }

        return RequiredWidth(layout) <= AnsiConsole.Profile.Width
            && RequiredHeight(layout, rows, extraContentHeight) <= AnsiConsole.Profile.Height;
    }

    // True when the grid and the summary panel can be shown together.
    private bool FitsWithSummary(BoardLayout layout, int rows, int summaryHeight)
    {
        return RequiredHeight(layout, rows, summaryHeight) <= AnsiConsole.Profile.Height;
    }

    // Characters a layout needs horizontally, panel border and padding included.
    private int RequiredWidth(BoardLayout layout)
    {
        var wordLength = config.GameSettings.WordLength;
        var boardWidth = wordLength * layout.Scale.Width + (wordLength - 1) * ColumnGap;

        return boardWidth + PanelHorizontalChrome;
    }

    // Lines a layout needs vertically: banner, grid, panel chrome, status and
    // message line, keyboard, and whatever the screen wants to add underneath.
    private int RequiredHeight(BoardLayout layout, int rows, int extraContentHeight)
    {
        var boardHeight = rows * layout.Scale.Height + (layout.UseRowSpacing ? rows - 1 : 0);
        var bannerHeight = layout.ShowFiglet ? FigletBannerHeight : CompactBannerHeight;
        var keyboardHeight = layout.ShowKeyboard ? KeyboardHeight : 0;

        return bannerHeight + boardHeight + PanelVerticalChrome + BoardChromeHeight
            + keyboardHeight + extraContentHeight;
    }

    // === LAYOUT BUILDING BLOCKS ===

    // The title at the top of every screen: the large ASCII banner when there is
    // room for it, a single centred line when there is not.
    private IRenderable BuildBanner(bool useFiglet)
    {
        if (useFiglet)
        {
            var banner = new FigletText("WORDLE")
                .Centered()
                .Color(ParseColor(config.Theme.AsciiTitleColor));

            return new Rows(banner, Text.Empty);
        }

        var compact = new Markup($"[bold {config.Theme.AsciiTitleColor}]W O R D L E[/]").Centered();

        return new Rows(compact, Text.Empty);
    }

    // Chooses the banner for a screen that is not the board. Those screens are much
    // shorter than the grid, so they get the large banner whenever their own content
    // leaves room for it instead of inheriting the board's requirements.
    private IRenderable BuildBannerForContent(int contentHeight)
    {
        var fitsFiglet = AnsiConsole.Profile.Width >= MinimumWidthForFiglet
            && FigletBannerHeight + contentHeight + SurroundingChromeHeight <= AnsiConsole.Profile.Height;

        return BuildBanner(fitsFiglet);
    }

    // A list of choices with a highlighted bar on the selected one, used by the main
    // menu and the difficulty picker.
    private IRenderable BuildSelectionPanel(IReadOnlyList<string> entries, int selectedIndex, string header)
    {
        // Every entry is padded to the same width so the highlight forms a clean bar.
        // The panel also has to stay wide enough for its own header, which Spectre
        // silently drops when it does not fit between the corners.
        var itemWidth = Math.Max(entries.Max(entry => entry.Length) + 6, header.Length + 4);

        var list = new Grid();
        list.AddColumn(new GridColumn().NoWrap());

        for (var index = 0; index < entries.Count; index++)
        {
            var isSelected = index == selectedIndex;
            var label = (isSelected ? "> " : "  ") + entries[index].ToUpperInvariant();
            var padded = Escape(" " + label.PadRight(itemWidth - 1));

            var markup = isSelected
                ? $"[bold black on {config.Theme.ColorPresent}]{padded}[/]"
                : $"[{config.Theme.ColorAbsent}]{padded}[/]";

            list.AddRow(new Markup(markup));
        }

        return BuildFramedPanel(list, header);
    }

    // The settings list: caption on the left, optional colour swatch, value on the
    // right. The selected row is marked and brightened rather than filled, so the
    // colour swatches stay readable.
    private Grid BuildSettingsGrid(IReadOnlyList<SettingsRow> rows, int selectedIndex)
    {
        var labelWidth = rows.Max(row => row.Label.Length) + 2;

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn(new GridColumn().NoWrap().PadRight(1));
        grid.AddColumn(new GridColumn().NoWrap());

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var isSelected = index == selectedIndex;

            var marker = isSelected ? "> " : "  ";
            var labelColor = isSelected ? $"bold {config.Theme.ColorPresent}" : config.Theme.ColorAbsent;
            var valueColor = isSelected ? "bold white" : "white";

            var label = new Markup($"[{labelColor}]{Escape(marker + row.Label.PadRight(labelWidth))}[/]");
            var swatch = row.SwatchColor is null
                ? new Markup(string.Empty)
                : new Markup($"[on {row.SwatchColor}]   [/]");
            var value = new Markup($"[{valueColor}]{Escape(row.Value)}[/]");

            grid.AddRow(label, swatch, value);
        }

        return grid;
    }

    // The summary shown when a round is over.
    private Grid BuildSummary(RoundResult result, LeaderboardEntry entry)
    {
        var outcomeColor = result.Won ? config.Theme.ColorCorrect : config.Theme.ColorPresent;
        var outcome = result.Won
            ? localizer.Format("finish.won", result.GuessesUsed, result.AttemptsAllowed)
            : localizer.Get("finish.lost");

        var summary = new Grid();
        summary.AddColumn(new GridColumn().NoWrap().PadRight(3));
        summary.AddColumn(new GridColumn().NoWrap());

        summary.AddRow(
            new Markup($"[bold {outcomeColor}]{Escape(outcome)}[/]"),
            new Markup(string.Empty));
        summary.AddEmptyRow();
        summary.AddRow(SummaryLabel("finish.word"), new Markup(
            $"[bold {config.Theme.ColorCorrect}]{Escape(result.TargetWord)}[/]"));
        summary.AddRow(SummaryLabel("finish.time"), SummaryValue(FormatDuration(result.Duration)));
        summary.AddRow(SummaryLabel("finish.guesses"), SummaryValue($"{result.GuessesUsed}/{result.AttemptsAllowed}"));
        summary.AddRow(SummaryLabel("finish.difficulty"), SummaryValue(
            localizer.Get(DifficultyRules.For(result.Level).NameKey)));
        summary.AddRow(SummaryLabel("finish.points"), new Markup(
            $"[bold {config.Theme.ColorPresent}]+{result.Points}[/]"));
        summary.AddRow(SummaryLabel("finish.total"), SummaryValue(entry.TotalPoints.ToString()));

        return summary;
    }

    // The ranked table of players.
    private IRenderable BuildLeaderboardTable(IReadOnlyList<LeaderboardEntry> entries, string currentPlayer)
    {
        // Narrow windows only get the columns that matter most.
        var showDetails = AnsiConsole.Profile.Width >= 72;

        var table = new Table
        {
            Border = TableBorder.Rounded,
            BorderStyle = new Style(ParseColor(config.Theme.AsciiTitleColor))
        };

        table.Title = new TableTitle(
            $"[bold {config.Theme.AsciiTitleColor}]{Escape(localizer.Get("leaderboard.header"))}[/]");

        table.AddColumn(new TableColumn(Header("leaderboard.rank")).RightAligned());
        table.AddColumn(new TableColumn(Header("leaderboard.player")));
        table.AddColumn(new TableColumn(Header("leaderboard.points")).RightAligned());

        if (showDetails)
        {
            table.AddColumn(new TableColumn(Header("leaderboard.games")).RightAligned());
            table.AddColumn(new TableColumn(Header("leaderboard.wins")).RightAligned());
            table.AddColumn(new TableColumn(Header("leaderboard.streak")).RightAligned());
            table.AddColumn(new TableColumn(Header("leaderboard.bestTime")).RightAligned());
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var isCurrent = entry.Name.Equals(currentPlayer, StringComparison.OrdinalIgnoreCase);

            // The current player is highlighted so their own row is easy to find.
            var style = isCurrent ? $"bold {config.Theme.ColorPresent}" : "white";

            var cells = new List<IRenderable>
            {
                new Markup($"[{config.Theme.ColorAbsent}]{index + 1}[/]"),
                new Markup($"[{style}]{Escape(entry.Name)}[/]"),
                new Markup($"[{style}]{entry.TotalPoints}[/]")
            };

            if (showDetails)
            {
                cells.Add(new Markup($"[{config.Theme.ColorAbsent}]{entry.GamesPlayed}[/]"));
                cells.Add(new Markup($"[{config.Theme.ColorCorrect}]{entry.Wins}[/]"));
                cells.Add(new Markup($"[{config.Theme.ColorAbsent}]{entry.BestStreak}[/]"));
                cells.Add(new Markup($"[{config.Theme.ColorAbsent}]" +
                    $"{(entry.BestTimeSeconds == 0 ? "-" : FormatDuration(TimeSpan.FromSeconds(entry.BestTimeSeconds)))}[/]"));
            }

            table.AddRow(cells);
        }

        return table;
    }

    // The line under the board: difficulty, elapsed time, guesses used and the
    // language of the word being guessed.
    private IRenderable BuildStatusLine(BoardView view)
    {
        var difficulty = localizer.Get(DifficultyRules.For(view.Level).NameKey);
        var separator = $"[{config.Theme.ColorEmpty}]  -  [/]";
        var wordLanguage = Escape(view.WordLanguage.ToUpperInvariant());

        var status =
            $"[{config.Theme.ColorAbsent}]{Escape(difficulty)}[/]{separator}" +
            $"[{config.Theme.ColorAbsent}]{FormatDuration(view.Elapsed)}[/]{separator}" +
            $"[{config.Theme.ColorAbsent}]{view.Guesses.Count}/{view.AttemptsAllowed}[/]{separator}" +
            $"[bold {config.Theme.ColorPresent}]{wordLanguage}[/]";

        return new Markup(status).Centered();
    }

    // The warning line under the status line. It is always drawn, even when empty,
    // so the board does not jump up and down while playing.
    private IRenderable BuildMessageLine(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return Text.Empty;
        }

        return new Markup($"[bold {config.Theme.ColorPresent}]{Escape(message)}[/]").Centered();
    }

    // The on-screen keyboard, coloured with the best state known for each letter.
    private IRenderable BuildKeyboard(IReadOnlyDictionary<char, LetterState> keyboardStates)
    {
        // The row layout comes from the translation file, so a German keyboard can
        // show QWERTZ while an English one shows QWERTY.
        var rows = localizer.Get("keyboard.rows").Split('|', StringSplitOptions.RemoveEmptyEntries);

        var keyboard = new Grid();
        keyboard.AddColumn(new GridColumn().NoWrap());

        foreach (var row in rows)
        {
            var keys = row.Select(letter =>
            {
                var known = keyboardStates.TryGetValue(letter, out var state);

                // Untouched letters stay dim; used ones take the colour of their state.
                return known
                    ? $"[bold white on {ColorForState(state)}] {letter} [/]"
                    : $"[{config.Theme.ColorAbsent} on {config.Theme.ColorEmpty}] {letter} [/]";
            });

            keyboard.AddRow(new Markup(string.Join(" ", keys)));
        }

        return Align.Center(keyboard);
    }

    // Wraps any content in the accent-coloured panel used across all screens.
    private Panel BuildFramedPanel(IRenderable content, string header)
    {
        var panel = new Panel(content)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(ParseColor(config.Theme.AsciiTitleColor)),
            Padding = new Padding(2, 1, 2, 1)
        };

        panel.Header = new PanelHeader(
            $"[bold {config.Theme.AsciiTitleColor}] {Escape(header)} [/]", Justify.Center);

        return panel;
    }

    // Builds the tile grid itself: one grid row per attempt, one column per letter.
    // While a guess is being checked the active row is drawn as a moving wave
    // instead of as plain empty tiles.
    private Grid BuildGrid(
        IReadOnlyList<WordleGuess> guesses,
        string currentInput,
        int rows,
        BoardLayout layout,
        BoardView? pending = null)
    {
        var wordLength = config.GameSettings.WordLength;

        var grid = new Grid();
        for (var column = 0; column < wordLength; column++)
        {
            grid.AddColumn(new GridColumn().NoWrap().PadLeft(0).PadRight(ColumnGap));
        }

        for (var row = 0; row < rows; row++)
        {
            var cells = new IRenderable[wordLength];
            for (var column = 0; column < wordLength; column++)
            {
                var tile = ResolveTile(guesses, currentInput, row, column);

                // Only the row that is currently being submitted shimmers.
                cells[column] = pending is { IsChecking: true } checking && row == guesses.Count
                    ? BuildShimmerTile(tile.Letter, column, checking.ShimmerFrame, layout.Scale)
                    : BuildTile(tile.Letter, tile.State, layout.Scale);
            }

            grid.AddRow(cells);

            // A blank line between the rows keeps neighbouring tiles from merging
            // into one solid block of colour.
            if (layout.UseRowSpacing && row < rows - 1)
            {
                grid.AddEmptyRow();
            }
        }

        return grid;
    }

    // Decides what a single tile shows. Rows above the active one come from
    // submitted guesses, the active row mirrors the current input buffer, and
    // everything below it stays blank.
    private (char Letter, LetterState State) ResolveTile(
        IReadOnlyList<WordleGuess> guesses,
        string currentInput,
        int row,
        int column)
    {
        // Already submitted: show the evaluated letter and its colour.
        if (row < guesses.Count)
        {
            var guess = guesses[row];
            return (guess.Word[column], guess.States[column]);
        }

        // The active row: show typed letters without any evaluation colour yet.
        if (row == guesses.Count && column < currentInput.Length)
        {
            return (currentInput[column], LetterState.Empty);
        }

        // Untouched tile.
        return (' ', LetterState.Empty);
    }

    // Renders one tile as a solid colour block with the letter centred inside it.
    private IRenderable BuildTile(char letter, LetterState state, TileScale scale)
    {
        // Without colour support a background-only tile would be invisible, so the
        // letter is drawn between brackets instead.
        if (!SupportsColor())
        {
            return new Markup($"[bold]{FormatPlainTile(letter, state)}[/]");
        }

        return BuildBlockTile(letter, ColorForState(state), scale);
    }

    // Draws a tile as a solid block of one colour with the letter centred in it.
    private static IRenderable BuildBlockTile(char letter, string background, TileScale scale)
    {
        var blankLine = new string(' ', scale.Width);

        // The middle line carries the letter; the lines above and below give the
        // block its height so the tile reads as a square rather than a text run.
        var letterLine = CenterInTile(letter, scale.Width);
        var lines = new string[scale.Height];
        for (var line = 0; line < scale.Height; line++)
        {
            lines[line] = line == scale.Height / 2 ? letterLine : blankLine;
        }

        var markup = string.Join(
            Environment.NewLine,
            lines.Select(line => $"[bold white on {background}]{line}[/]"));

        return new Markup(markup);
    }

    // One tile of the row that is being checked. The shade depends on how far the
    // column sits behind the crest of the wave, so raising the frame number moves
    // the bright band one column to the right.
    private IRenderable BuildShimmerTile(char letter, int column, int frame, TileScale scale)
    {
        if (!SupportsColor())
        {
            return new Markup($"[bold]{FormatPlainTile(letter, LetterState.Empty)}[/]");
        }

        var offset = ((column - frame) % ShimmerShades.Length + ShimmerShades.Length) % ShimmerShades.Length;

        return BuildBlockTile(letter, ShimmerShades[offset], scale);
    }

    // Colourless fallback: the evaluation is encoded in the brackets around the letter.
    private static string FormatPlainTile(char letter, LetterState state)
    {
        var visibleLetter = letter == ' ' ? '_' : letter;

        return state switch
        {
            LetterState.Correct => $"[[{visibleLetter}]]",
            LetterState.Present => $"({visibleLetter})",
            _ => $" {visibleLetter} "
        };
    }

    // Pads a single character to the tile width so it sits in the middle of the block.
    private static string CenterInTile(char letter, int tileWidth)
    {
        var leftPadding = (tileWidth - 1) / 2;
        var rightPadding = tileWidth - 1 - leftPadding;

        return new string(' ', leftPadding) + letter + new string(' ', rightPadding);
    }

    // Maps an evaluation state onto the colour configured for it in config.json.
    private string ColorForState(LetterState state) => state switch
    {
        LetterState.Correct => config.Theme.ColorCorrect,
        LetterState.Present => config.Theme.ColorPresent,
        LetterState.Absent => config.Theme.ColorAbsent,
        _ => config.Theme.ColorEmpty
    };

    // True when the terminal can render the coloured tiles the theme relies on.
    private static bool SupportsColor()
    {
        return AnsiConsole.Profile.Capabilities.ColorSystem != ColorSystem.NoColors;
    }

    // Turns a colour name from config.json into a Spectre.Console colour.
    // Style.Parse understands every named colour the markup syntax accepts, so the
    // config file and the markup strings stay in sync. Unknown names fall back to white.
    private static Color ParseColor(string colorName)
    {
        try
        {
            return Style.Parse(colorName).Foreground;
        }
        catch (InvalidOperationException)
        {
            return Color.White;
        }
    }

    // === TEXT HELPERS ===

    // The hint line at the bottom of the screen. Translations differ wildly in
    // length, so the long variant is only used while it actually fits on one line.
    private IRenderable BuildHint(string longKey, string shortKey)
    {
        var longHint = localizer.Get(longKey);
        var hint = longHint.Length + 4 <= AnsiConsole.Profile.Width ? longHint : localizer.Get(shortKey);

        return new Markup(Dim(hint)).Centered();
    }

    // A dimmed caption on the finish screen.
    private IRenderable SummaryLabel(string key)
    {
        return new Markup(Dim(localizer.Get(key)));
    }

    // A highlighted value on the finish screen.
    private static IRenderable SummaryValue(string text)
    {
        return new Markup($"[white]{Escape(text)}[/]");
    }

    // A translated, accent-coloured table header.
    private IRenderable Header(string key)
    {
        return new Markup($"[bold {config.Theme.AsciiTitleColor}]{Escape(localizer.Get(key))}[/]");
    }

    // Wraps a text in the muted colour used for captions and hints.
    private string Dim(string text)
    {
        return $"[{config.Theme.ColorAbsent}]{Escape(text)}[/]";
    }

    // Durations are always shown as mm:ss, which stays readable past an hour.
    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}";
    }

    // Keeps a technical detail from blowing up the panel width.
    private static string Shorten(string text, int maximumLength)
    {
        return text.Length <= maximumLength ? text : text[..(maximumLength - 1)] + "…";
    }

    // Translated text is data, not markup: square brackets in it must not be
    // interpreted as Spectre styling tags.
    private static string Escape(string text)
    {
        return Markup.Escape(text);
    }
}
