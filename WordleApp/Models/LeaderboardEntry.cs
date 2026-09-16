using System.Text.Json.Serialization;

namespace WordleApp.Models;

// === LEADERBOARD ENTRY ===
// One row of leaderboard.json. The file sits next to config.json, so a player's
// name, settings and scores all travel together with the game.

/// <summary>
/// The accumulated results of a single player.
/// </summary>
public class LeaderboardEntry
{
    /// <summary>The player's name, as entered on the name screen.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Rounds finished, won and lost together.</summary>
    public int GamesPlayed { get; set; }

    /// <summary>Rounds in which the word was found.</summary>
    public int Wins { get; set; }

    /// <summary>Sum of the points of every round.</summary>
    public int TotalPoints { get; set; }

    /// <summary>Highest amount of points earned in a single round.</summary>
    public int BestPoints { get; set; }

    /// <summary>Guesses used across all won rounds, used for the average.</summary>
    public int TotalWinGuesses { get; set; }

    /// <summary>Fastest won round in seconds; zero while nothing has been won.</summary>
    public int BestTimeSeconds { get; set; }

    /// <summary>Wins in a row up to now.</summary>
    public int CurrentStreak { get; set; }

    /// <summary>Longest run of wins ever achieved.</summary>
    public int BestStreak { get; set; }

    /// <summary>Date of the last finished round, as yyyy-MM-dd.</summary>
    public string LastPlayed { get; set; } = string.Empty;

    // Derived values are calculated on demand and must not end up in the file.

    /// <summary>Share of won rounds, between 0 and 1.</summary>
    [JsonIgnore]
    public double WinRate => GamesPlayed == 0 ? 0 : (double)Wins / GamesPlayed;

    /// <summary>Average number of guesses in won rounds; zero without a win.</summary>
    [JsonIgnore]
    public double AverageWinGuesses => Wins == 0 ? 0 : (double)TotalWinGuesses / Wins;
}
