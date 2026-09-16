namespace WordleApp.UI;

// === CONSOLE SESSION ===
// Owns the terminal itself: the black backdrop, the hidden cursor and the
// restoration of both when the game ends. Wrapping this in an IDisposable means
// the terminal is handed back in a usable state even if the game throws.

/// <summary>
/// Prepares the terminal for the game and restores its original state on disposal.
/// </summary>
public sealed class ConsoleSession : IDisposable
{
    private readonly bool cursorWasVisible;
    private readonly EventHandler processExitHandler;
    private bool isDisposed;

    private ConsoleSession(bool cursorWasVisible)
    {
        this.cursorWasVisible = cursorWasVisible;

        // A crash or a Ctrl+C must never leave the user with an invisible cursor,
        // so the restore is also wired to process shutdown.
        processExitHandler = (_, _) => RestoreCursor();
        AppDomain.CurrentDomain.ProcessExit += processExitHandler;
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    /// <summary>
    /// Switches the terminal into game mode: UTF-8 output, black background, no cursor.
    /// </summary>
    /// <returns>A session that restores the previous terminal state when disposed.</returns>
    public static ConsoleSession Start()
    {
        TrySetOutputEncoding();

        var cursorWasVisible = ReadCursorVisibility();
        var session = new ConsoleSession(cursorWasVisible);

        // Buffer operations only work against a real console window, so they are
        // skipped when the output is piped somewhere else.
        if (!Console.IsOutputRedirected)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Clear();
        }

        SetCursorVisibility(false);
        TrySetTitle("WORDLE");

        return session;
    }

    /// <summary>
    /// Restores the cursor and the default console colours.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        AppDomain.CurrentDomain.ProcessExit -= processExitHandler;
        Console.CancelKeyPress -= OnCancelKeyPress;

        RestoreCursor();
        Console.ResetColor();
    }

    // === TERMINAL PLUMBING ===

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        RestoreCursor();
    }

    private void RestoreCursor()
    {
        SetCursorVisibility(cursorWasVisible);
    }

    // Cursor visibility is a Windows-only property to read, and either call can fail
    // when there is no real console attached, so both directions are defensive.
    private static bool ReadCursorVisibility()
    {
        try
        {
            return !OperatingSystem.IsWindows() || Console.CursorVisible;
        }
        catch (Exception exception) when (exception is IOException or PlatformNotSupportedException)
        {
            return true;
        }
    }

    private static void SetCursorVisibility(bool visible)
    {
        try
        {
            Console.CursorVisible = visible;
        }
        catch (Exception exception) when (exception is IOException or PlatformNotSupportedException)
        {
            // Terminals without cursor control simply keep whatever they had.
        }
    }

    private static void TrySetOutputEncoding()
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
            // Without UTF-8 the box drawing characters degrade, which is not fatal.
        }
    }

    private static void TrySetTitle(string title)
    {
        try
        {
            Console.Title = title;
        }
        catch (Exception exception) when (exception is IOException or PlatformNotSupportedException)
        {
            // Not fatal: the game simply runs without a custom window title.
        }
    }
}
