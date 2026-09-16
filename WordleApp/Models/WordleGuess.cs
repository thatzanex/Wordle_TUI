namespace WordleApp.Models;

// === GUESS MODEL ===

/// <summary>
/// A single submitted guess together with the evaluation result of each of its letters.
/// </summary>
public class WordleGuess
{
    /// <summary>The guessed word, always stored in uppercase.</summary>
    public string Word { get; }

    /// <summary>One state per letter of <see cref="Word"/>, in the same order.</summary>
    public LetterState[] States { get; }

    /// <summary>
    /// Creates a guess from a word and its per-letter evaluation.
    /// </summary>
    /// <param name="word">The guessed word; it is normalised to uppercase.</param>
    /// <param name="states">Evaluation states, one per letter of the word.</param>
    /// <exception cref="ArgumentException">The number of states does not match the word length.</exception>
    public WordleGuess(string word, LetterState[] states)
    {
        if (word.Length != states.Length)
        {
            throw new ArgumentException("A guess needs exactly one letter state per letter.", nameof(states));
        }

        Word = word.ToUpperInvariant();
        States = states;
    }

    /// <summary>
    /// True when every letter of the guess is in its correct position, i.e. the round is won.
    /// </summary>
    public bool IsWinning => States.All(state => state == LetterState.Correct);
}
