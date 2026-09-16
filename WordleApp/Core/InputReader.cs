namespace WordleApp.Core;

// === INPUT READER ===
// One keyboard loop for the whole application. Every screen waits here, which is
// what keeps the interface responsive to window resizing: instead of blocking in
// Console.ReadKey, the loop polls for a keystroke and reports a changed window
// size as "no key, please redraw".

/// <summary>
/// Waits for keystrokes and reports terminal resizes to the caller.
/// </summary>
public class InputReader
{
    // How often the loop looks for a keystroke or a resized window.
    // Short enough to feel instant, long enough to keep the process idle.
    private const int PollIntervalMilliseconds = 40;

    private (int Width, int Height) lastWindowSize;

    /// <summary>
    /// Starts the reader and remembers the current window size as the baseline.
    /// </summary>
    public InputReader()
    {
        lastWindowSize = CurrentWindowSize();
    }

    /// <summary>
    /// Waits for a keystroke.
    /// </summary>
    /// <returns>The key that was pressed, or null when the terminal was resized
    /// and the caller should redraw at the new size.</returns>
    /// <remarks>Virtual so a test can replay a scripted sequence of keystrokes.</remarks>
    public virtual ConsoleKeyInfo? WaitForInput()
    {
        while (true)
        {
            if (Console.KeyAvailable)
            {
                return Console.ReadKey(intercept: true);
            }

            if (WindowSizeChanged())
            {
                return null;
            }

            Thread.Sleep(PollIntervalMilliseconds);
        }
    }

    /// <summary>
    /// Accepts the current window size as the baseline. Called after a screen drew
    /// itself outside the normal loop, so the next wait does not report a stale resize.
    /// </summary>
    public void SyncWindowSize()
    {
        lastWindowSize = CurrentWindowSize();
    }

    // Compares the window size against the previous frame and remembers the new one.
    private bool WindowSizeChanged()
    {
        var size = CurrentWindowSize();
        if (size == lastWindowSize)
        {
            return false;
        }

        lastWindowSize = size;

        return true;
    }

    // The current window size, or (0, 0) when no real console window is attached.
    private static (int Width, int Height) CurrentWindowSize()
    {
        try
        {
            return Console.IsOutputRedirected
                ? (0, 0)
                : (Console.WindowWidth, Console.WindowHeight);
        }
        catch (IOException)
        {
            return (0, 0);
        }
    }
}
