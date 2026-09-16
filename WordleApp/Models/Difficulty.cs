namespace WordleApp.Models;

// === DIFFICULTY ===
// Difficulty is the game mode axis: it changes how many guesses the player gets,
// whether revealed hints have to be reused, and how much a win is worth.

/// <summary>
/// The three difficulty levels offered before a round starts.
/// </summary>
public enum Difficulty
{
    /// <summary>One extra guess, no extra rules, reduced score.</summary>
    Easy,

    /// <summary>The classic rules with the configured number of guesses.</summary>
    Normal,

    /// <summary>One guess less, revealed hints must be reused, bonus score.</summary>
    Hard
}

/// <summary>
/// The concrete rules behind a difficulty level.
/// </summary>
/// <param name="Level">The level these rules belong to.</param>
/// <param name="ExtraAttempts">Guesses added to (or removed from) the configured amount.</param>
/// <param name="EnforceRevealedHints">Whether every revealed hint must appear in the next guess.</param>
/// <param name="ScoreMultiplier">Factor applied to the points of a won round.</param>
public readonly record struct DifficultyRules(
    Difficulty Level,
    int ExtraAttempts,
    bool EnforceRevealedHints,
    double ScoreMultiplier)
{
    // The rule table. Keeping it here means the game loop never switches on the
    // difficulty itself, it just asks for the rules.
    private static readonly DifficultyRules[] All =
    {
        new DifficultyRules(Difficulty.Easy, 1, false, 0.75),
        new DifficultyRules(Difficulty.Normal, 0, false, 1.0),
        new DifficultyRules(Difficulty.Hard, -1, true, 1.5)
    };

    /// <summary>
    /// Looks up the rules of a difficulty level.
    /// </summary>
    /// <param name="level">The level to describe.</param>
    /// <returns>The rules belonging to that level.</returns>
    public static DifficultyRules For(Difficulty level)
    {
        return All.First(rules => rules.Level == level);
    }

    /// <summary>All levels in the order the selection screen shows them.</summary>
    public static IReadOnlyList<Difficulty> Levels { get; } =
        All.Select(rules => rules.Level).ToArray();

    /// <summary>
    /// Number of guesses the player gets, derived from the configured board size.
    /// </summary>
    /// <param name="configuredAttempts">The attempts value from config.json.</param>
    /// <returns>The attempts for this difficulty, never fewer than three.</returns>
    public int ResolveAttempts(int configuredAttempts)
    {
        return Math.Max(3, configuredAttempts + ExtraAttempts);
    }

    /// <summary>Translation key of this level's name.</summary>
    public string NameKey => $"difficulty.{Level.ToString().ToLowerInvariant()}";

    /// <summary>Translation key of this level's one-line description.</summary>
    public string DescriptionKey => $"difficulty.{Level.ToString().ToLowerInvariant()}.description";
}
