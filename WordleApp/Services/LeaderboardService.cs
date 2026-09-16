using System.Text.Json;
using WordleApp.Models;

namespace WordleApp.Services;

// === LEADERBOARD SERVICE ===
// Keeps the scores in leaderboard.json next to config.json, so the player's name,
// settings and results all live in the same place and survive a restart.

/// <summary>
/// Loads, updates and ranks the player scores.
/// </summary>
public class LeaderboardService
{
    private const string FileName = "leaderboard.json";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly List<LeaderboardEntry> entries;

    /// <summary>
    /// Loads the leaderboard that sits next to the application.
    /// </summary>
    public LeaderboardService()
    {
        filePath = Path.Combine(AppContext.BaseDirectory, FileName);
        entries = Load();
    }

    /// <summary>Message describing the last failed save, or null when all is well.</summary>
    public string? LastError { get; private set; }

    // === QUERIES ===

    /// <summary>
    /// Returns the players ordered by total points, best first.
    /// </summary>
    /// <returns>The ranked entries.</returns>
    public IReadOnlyList<LeaderboardEntry> GetRanked()
    {
        return entries
            .OrderByDescending(entry => entry.TotalPoints)
            .ThenByDescending(entry => entry.Wins)
            .ThenBy(entry => entry.AverageWinGuesses == 0 ? double.MaxValue : entry.AverageWinGuesses)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Looks up a single player, for example to show their totals on the finish screen.
    /// </summary>
    /// <param name="name">The player's name.</param>
    /// <returns>The entry, or null when that player has no finished round yet.</returns>
    public LeaderboardEntry? Find(string name)
    {
        return entries.FirstOrDefault(entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    // === UPDATES ===

    /// <summary>
    /// Books a finished round onto the player's entry and writes the file.
    /// </summary>
    /// <param name="name">The player who finished the round.</param>
    /// <param name="result">The outcome of the round.</param>
    /// <returns>The updated entry.</returns>
    public LeaderboardEntry RecordResult(string name, RoundResult result)
    {
        var entry = Find(name);
        if (entry is null)
        {
            entry = new LeaderboardEntry { Name = name };
            entries.Add(entry);
        }

        entry.GamesPlayed++;
        entry.TotalPoints += result.Points;
        entry.BestPoints = Math.Max(entry.BestPoints, result.Points);
        entry.LastPlayed = DateTime.Now.ToString("yyyy-MM-dd");

        if (result.Won)
        {
            entry.Wins++;
            entry.TotalWinGuesses += result.GuessesUsed;
            entry.CurrentStreak++;
            entry.BestStreak = Math.Max(entry.BestStreak, entry.CurrentStreak);

            // The first win has nothing to compare against, so it always sets the record.
            var seconds = (int)Math.Round(result.Duration.TotalSeconds);
            if (entry.BestTimeSeconds == 0 || seconds < entry.BestTimeSeconds)
            {
                entry.BestTimeSeconds = seconds;
            }
        }
        else
        {
            entry.CurrentStreak = 0;
        }

        Save();

        return entry;
    }

    /// <summary>
    /// Moves the scores of a player to a new name, used when the name is changed.
    /// </summary>
    /// <param name="oldName">The name used so far.</param>
    /// <param name="newName">The name to use from now on.</param>
    public void Rename(string oldName, string newName)
    {
        var entry = Find(oldName);
        if (entry is null || oldName.Equals(newName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Merging into an existing entry would silently add up two players'
        // scores, so a name that is already taken simply keeps its own entry.
        if (Find(newName) is not null)
        {
            return;
        }

        entry.Name = newName;
        Save();
    }

    // === FILE HANDLING ===

    // A missing or broken file starts an empty leaderboard rather than failing.
    private List<LeaderboardEntry> Load()
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new List<LeaderboardEntry>();
            }

            var json = File.ReadAllText(filePath);
            var loaded = JsonSerializer.Deserialize<List<LeaderboardEntry>>(json);

            return loaded ?? new List<LeaderboardEntry>();
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            LastError = exception.Message;

            return new List<LeaderboardEntry>();
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(filePath, JsonSerializer.Serialize(entries, WriteOptions));
            LastError = null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A read-only directory must not end the round; the score simply stays
            // in memory and the failure is reported through LastError.
            LastError = exception.Message;
        }
    }
}
