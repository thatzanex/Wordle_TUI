namespace WordleApp.Models;

// === ROUND RESULT ===

/// <summary>
/// Everything the finish screen and the leaderboard need to know about a
/// completed round.
/// </summary>
/// <param name="Won">True when the player found the word.</param>
/// <param name="TargetWord">The word that had to be guessed, always revealed at the end.</param>
/// <param name="GuessesUsed">How many guesses were submitted.</param>
/// <param name="AttemptsAllowed">How many guesses the difficulty granted.</param>
/// <param name="Duration">How long the round took.</param>
/// <param name="Points">Points earned; zero for a lost round.</param>
/// <param name="Level">The difficulty the round was played on.</param>
public readonly record struct RoundResult(
    bool Won,
    string TargetWord,
    int GuessesUsed,
    int AttemptsAllowed,
    TimeSpan Duration,
    int Points,
    Difficulty Level);
