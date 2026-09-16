namespace WordleApp.Models;

// === BOARD VIEW ===

/// <summary>
/// A snapshot of the running round, handed to the renderer so it can draw a frame
/// without knowing anything about the game rules.
/// </summary>
/// <param name="Guesses">Guesses already submitted, oldest first.</param>
/// <param name="CurrentInput">Letters typed into the active row but not yet submitted.</param>
/// <param name="AttemptsAllowed">Number of rows on the board for this round.</param>
/// <param name="Message">Transient warning shown under the board, already translated.</param>
/// <param name="KeyboardStates">Best state seen so far per letter, for the on-screen keyboard.</param>
/// <param name="Elapsed">How long the round has been running.</param>
/// <param name="Level">The difficulty being played.</param>
/// <param name="WordLanguage">Language code of the word being guessed, for example "de".</param>
/// <param name="ShimmerFrame">
/// Animation step while the guess is being checked: the active row is drawn as a
/// grey wave running left to right, one shade further with every frame.
/// Negative means the row is drawn normally.
/// </param>
public readonly record struct BoardView(
    IReadOnlyList<WordleGuess> Guesses,
    string CurrentInput,
    int AttemptsAllowed,
    string? Message,
    IReadOnlyDictionary<char, LetterState> KeyboardStates,
    TimeSpan Elapsed,
    Difficulty Level,
    string WordLanguage,
    int ShimmerFrame = -1)
{
    /// <summary>True while the guess in the active row is being checked.</summary>
    public bool IsChecking => ShimmerFrame >= 0;
}
