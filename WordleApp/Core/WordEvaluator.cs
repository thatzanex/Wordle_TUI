using WordleApp.Models;

namespace WordleApp.Core;

// === WORD EVALUATOR ===
// Pure comparison logic: no console output, no game state, no side effects.

/// <summary>
/// Compares a guess against the target word and produces one state per letter.
/// </summary>
public class WordEvaluator
{
    /// <summary>
    /// Evaluates the guess against the target word.
    /// Note: handles the double-letter edge case with two passes. The first pass
    /// marks the exact hits and consumes those target letters; the second pass may
    /// then only mark a letter as present while an unconsumed copy of it is left.
    /// That is why "SASSY" against "GHOST" colours only its first S, and why the
    /// second L of "LLAMA" against "LEVEL" stays grey.
    /// </summary>
    /// <param name="guess">The guessed word.</param>
    /// <param name="target">The word the player is trying to find.</param>
    /// <returns>One <see cref="LetterState"/> per letter of the guess.</returns>
    /// <exception cref="ArgumentException">The two words have different lengths.</exception>
    public LetterState[] EvaluateGuess(string guess, string target)
    {
        if (guess.Length != target.Length)
        {
            throw new ArgumentException("A guess must be as long as the target word.", nameof(guess));
        }

        var states = new LetterState[guess.Length];

        // Tracks which target letters have already been matched, so no target
        // letter can be claimed twice.
        var consumed = new bool[target.Length];

        // --- Pass 1: letters that sit in the right position ---
        for (var index = 0; index < guess.Length; index++)
        {
            if (guess[index] != target[index])
            {
                continue;
            }

            states[index] = LetterState.Correct;
            consumed[index] = true;
        }

        // --- Pass 2: letters that occur elsewhere in the target ---
        for (var index = 0; index < guess.Length; index++)
        {
            if (states[index] == LetterState.Correct)
            {
                continue;
            }

            states[index] = LetterState.Absent;

            for (var candidate = 0; candidate < target.Length; candidate++)
            {
                if (consumed[candidate] || target[candidate] != guess[index])
                {
                    continue;
                }

                states[index] = LetterState.Present;
                consumed[candidate] = true;
                break;
            }
        }

        return states;
    }
}
