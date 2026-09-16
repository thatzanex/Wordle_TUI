namespace WordleApp.Models;

// === LETTER STATE ===

/// <summary>
/// The result of comparing a single guessed letter against the target word.
/// The renderer maps each value onto a colour from the configured theme.
/// </summary>
public enum LetterState
{
    /// <summary>No letter has been typed into this tile yet.</summary>
    Empty,

    /// <summary>The letter does not occur in the target word.</summary>
    Absent,

    /// <summary>The letter occurs in the target word, but at a different position.</summary>
    Present,

    /// <summary>The letter occurs in the target word at exactly this position.</summary>
    Correct
}
