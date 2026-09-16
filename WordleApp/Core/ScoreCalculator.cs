using WordleApp.Models;

namespace WordleApp.Core;

// === SCORING ===
// Points reward three things, in this order: finding the word at all, needing few
// guesses, and being quick about it. The difficulty multiplier is applied last, so
// a hard round is always worth more than the same round on easy.

/// <summary>
/// Turns the outcome of a round into a point score.
/// </summary>
public class ScoreCalculator
{
    // Awarded for finding the word, regardless of how long it took.
    private const int WinBasePoints = 1000;

    // Awarded per guess the player did not need.
    private const int PointsPerUnusedGuess = 150;

    // Speed bonus at zero seconds; it drops away linearly and is gone after
    // SpeedBonusSeconds, so there is no reason to rush a hopeless round.
    private const int MaximumSpeedBonus = 600;
    private const int SpeedBonusSeconds = 120;

    /// <summary>
    /// Calculates the points for a finished round.
    /// </summary>
    /// <param name="won">Whether the word was found.</param>
    /// <param name="guessesUsed">Guesses the player submitted.</param>
    /// <param name="attemptsAllowed">Guesses the difficulty granted.</param>
    /// <param name="duration">How long the round took.</param>
    /// <param name="level">The difficulty played.</param>
    /// <returns>The points earned; zero for a lost round.</returns>
    public int Calculate(bool won, int guessesUsed, int attemptsAllowed, TimeSpan duration, Difficulty level)
    {
        // A lost round is worth nothing, no matter how fast it was lost.
        if (!won)
        {
            return 0;
        }

        var guessBonus = Math.Max(0, attemptsAllowed - guessesUsed) * PointsPerUnusedGuess;
        var speedBonus = CalculateSpeedBonus(duration);
        var multiplier = DifficultyRules.For(level).ScoreMultiplier;

        return (int)Math.Round((WinBasePoints + guessBonus + speedBonus) * multiplier);
    }

    // The speed bonus fades out linearly over the first two minutes of a round.
    private static int CalculateSpeedBonus(TimeSpan duration)
    {
        var seconds = Math.Max(0, duration.TotalSeconds);
        if (seconds >= SpeedBonusSeconds)
        {
            return 0;
        }

        var remaining = 1 - seconds / SpeedBonusSeconds;

        return (int)Math.Round(MaximumSpeedBonus * remaining);
    }
}
