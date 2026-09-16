using System.Diagnostics;
using WordleApp.Models;
using WordleApp.Services;
using WordleApp.UI;

namespace WordleApp.Core;

// === GAME ENGINE ===
// Owns one round of Wordle: the input buffer, the submitted guesses, the clock and
// the win/loss decision. It never writes to the console itself - it builds a
// BoardView and hands it to the renderer.

/// <summary>
/// Drives rounds of Wordle: input handling, state tracking and win/loss detection.
/// </summary>
public class WordleGame
{
    private readonly GameConfig config;
    private readonly FrontendRenderer renderer;
    private readonly LocalizationService localizer;
    private readonly DictionaryApiService api;
    private readonly WordPool words;
    private readonly LeaderboardService leaderboard;
    private readonly InputReader input;
    private readonly WordEvaluator evaluator = new();
    private readonly ScoreCalculator scoreCalculator = new();

    /// <summary>
    /// Creates the engine with everything a round needs.
    /// </summary>
    /// <param name="config">The live configuration; supplies word length and player name.</param>
    /// <param name="renderer">Draws every frame of the round.</param>
    /// <param name="localizer">Translates the messages shown to the player.</param>
    /// <param name="api">Validates guesses against the dictionary.</param>
    /// <param name="words">Supplies target words, usually without waiting for the network.</param>
    /// <param name="leaderboard">Records the result of each finished round.</param>
    /// <param name="input">Shared keyboard loop.</param>
    public WordleGame(
        GameConfig config,
        FrontendRenderer renderer,
        LocalizationService localizer,
        DictionaryApiService api,
        WordPool words,
        LeaderboardService leaderboard,
        InputReader input)
    {
        this.config = config;
        this.renderer = renderer;
        this.localizer = localizer;
        this.api = api;
        this.words = words;
        this.leaderboard = leaderboard;
        this.input = input;
    }

    // === GAME LOOP ===

    /// <summary>
    /// Plays rounds on the chosen difficulty until the player returns to the menu.
    /// </summary>
    /// <param name="level">The difficulty selected before the first round.</param>
    public void Run(Difficulty level)
    {
        while (PlayRound(level))
        {
            // The finish screen decides whether another word is drawn.
        }
    }

    // Plays a single round. Returns true when the player asked for another one.
    private bool PlayRound(Difficulty level)
    {
        var rules = DifficultyRules.For(level);
        var attemptsAllowed = rules.ResolveAttempts(config.GameSettings.MaxAttempts);
        var wordLength = config.GameSettings.WordLength;

        // The pool usually has a word ready, so the round starts instantly. Only an
        // empty pool waits for the network, and only that wait gets a spinner.
        if (!words.TryTake(wordLength, config.WordLanguage, out var target))
        {
            target = renderer.RunWithStatus(
                "status.fetchingWord",
                () => words.Take(wordLength, config.WordLanguage));

            input.SyncWindowSize();
        }

        if (target is null)
        {
            ShowMessageUntilKey("error.header", "error.noWord", api.LastError);

            return false;
        }

        var guesses = new List<WordleGuess>();
        var keyboardStates = new Dictionary<char, LetterState>();
        var currentInput = string.Empty;
        string? message = null;

        // Remembered so the player is told once when the dictionary stops answering
        // and guesses start being accepted unchecked.
        var validationWasActive = api.IsValidationActive;

        var clock = Stopwatch.StartNew();
        var needsRedraw = true;

        while (true)
        {
            if (needsRedraw)
            {
                DrawBoard(guesses, currentInput, attemptsAllowed, message, keyboardStates, clock.Elapsed, level);
                needsRedraw = false;
            }

            var key = input.WaitForInput();

            // A null key means the window was resized: redraw at the new size.
            if (key is null)
            {
                needsRedraw = true;
                continue;
            }

            // Any keystroke clears the transient warning from the previous one.
            message = null;
            needsRedraw = true;

            switch (key.Value.Key)
            {
                case ConsoleKey.Escape:
                    // Abandoning a round on purpose does not count as a loss.
                    return false;

                case ConsoleKey.Backspace:
                    if (currentInput.Length > 0)
                    {
                        currentInput = currentInput[..^1];
                    }

                    continue;

                case ConsoleKey.Enter:
                {
                    var pendingView = BuildView(
                        guesses, currentInput, attemptsAllowed, null, keyboardStates, clock.Elapsed, level);

                    var submission = TrySubmit(pendingView, target, rules, wordLength);

                    if (validationWasActive && !api.IsValidationActive)
                    {
                        validationWasActive = false;
                        message = localizer.Get("game.validationOff");
                    }

                    if (submission.Message is not null)
                    {
                        message = submission.Message;
                        continue;
                    }

                    var guess = submission.Guess!;
                    guesses.Add(guess);
                    UpdateKeyboard(keyboardStates, guess);
                    currentInput = string.Empty;

                    var won = guess.IsWinning;
                    if (!won && guesses.Count < attemptsAllowed)
                    {
                        continue;
                    }

                    clock.Stop();

                    return FinishRound(guesses, target, attemptsAllowed, clock.Elapsed, level, won);
                }

                default:
                    // Letters are the only other input the board accepts.
                    if (char.IsLetter(key.Value.KeyChar) && currentInput.Length < wordLength)
                    {
                        currentInput += char.ToUpperInvariant(key.Value.KeyChar);
                    }

                    continue;
            }
        }
    }

    // === SUBMITTING A GUESS ===

    // The outcome of pressing Enter: either a translated complaint, or an evaluated guess.
    private readonly record struct Submission(string? Message, WordleGuess? Guess);

    // Runs every check a guess has to pass, in the cheapest-first order, and
    // evaluates it once it survived all of them.
    private Submission TrySubmit(
        BoardView view,
        string target,
        DifficultyRules rules,
        int wordLength)
    {
        var currentInput = view.CurrentInput;

        if (currentInput.Length < wordLength)
        {
            return new Submission(localizer.Get("game.tooShort"), null);
        }

        // On hard difficulty every hint the board has already revealed has to be
        // reused, so that check runs before the word is sent anywhere.
        if (rules.EnforceRevealedHints)
        {
            var violation = FindHintViolation(currentInput, view.Guesses);
            if (violation is not null)
            {
                return new Submission(violation, null);
            }
        }

        // With validation switched off there is nothing to wait for, so the board is
        // not animated at all. Otherwise the board stays on screen and the row being
        // submitted shimmers until the dictionary answers.
        var isValid = true;
        if (api.IsValidationActive)
        {
            isValid = renderer.RunWhileCheckingGuess(
                view,
                () => api.IsValidWordAsync(currentInput, config.WordLanguage));

            input.SyncWindowSize();
        }

        if (!isValid)
        {
            return new Submission(localizer.Get("game.notInList"), null);
        }

        return new Submission(null, new WordleGuess(currentInput, evaluator.EvaluateGuess(currentInput, target)));
    }

    // Hard difficulty rule: a letter revealed as correct has to stay in its column,
    // and a letter revealed as present has to show up somewhere in the guess.
    private string? FindHintViolation(string candidate, IReadOnlyList<WordleGuess> guesses)
    {
        foreach (var guess in guesses)
        {
            for (var index = 0; index < guess.States.Length; index++)
            {
                var letter = guess.Word[index];

                if (guess.States[index] == LetterState.Correct && candidate[index] != letter)
                {
                    return localizer.Format("game.hardPosition", letter, index + 1);
                }

                if (guess.States[index] == LetterState.Present && !candidate.Contains(letter))
                {
                    return localizer.Format("game.hardContains", letter);
                }
            }
        }

        return null;
    }

    // Remembers the best state seen for each letter, for the on-screen keyboard.
    private static void UpdateKeyboard(IDictionary<char, LetterState> keyboardStates, WordleGuess guess)
    {
        for (var index = 0; index < guess.Word.Length; index++)
        {
            var letter = guess.Word[index];
            var state = guess.States[index];

            // A letter that was green once must never fall back to yellow or grey.
            if (!keyboardStates.TryGetValue(letter, out var known) || state > known)
            {
                keyboardStates[letter] = state;
            }
        }
    }

    // === FINISHING A ROUND ===

    // Scores the round, books it onto the leaderboard and shows the finish screen.
    // Returns true when the player wants another word.
    private bool FinishRound(
        IReadOnlyList<WordleGuess> guesses,
        string target,
        int attemptsAllowed,
        TimeSpan duration,
        Difficulty level,
        bool won)
    {
        var points = scoreCalculator.Calculate(won, guesses.Count, attemptsAllowed, duration, level);
        var result = new RoundResult(won, target, guesses.Count, attemptsAllowed, duration, points, level);
        var entry = leaderboard.RecordResult(config.PlayerName, result);

        var needsRedraw = true;

        while (true)
        {
            if (needsRedraw)
            {
                renderer.DrawFinishScreen(result, guesses, entry);
                needsRedraw = false;
            }

            var key = input.WaitForInput();
            if (key is null)
            {
                needsRedraw = true;
                continue;
            }

            switch (key.Value.Key)
            {
                case ConsoleKey.Enter:
                case ConsoleKey.Spacebar:
                    return true;

                case ConsoleKey.Escape:
                    return false;
            }
        }
    }

    // === HELPERS ===

    private void DrawBoard(
        IReadOnlyList<WordleGuess> guesses,
        string currentInput,
        int attemptsAllowed,
        string? message,
        IReadOnlyDictionary<char, LetterState> keyboardStates,
        TimeSpan elapsed,
        Difficulty level)
    {
        renderer.DrawBoard(BuildView(
            guesses, currentInput, attemptsAllowed, message, keyboardStates, elapsed, level));
    }

    // Packs the round state into the snapshot the renderer works with.
    private BoardView BuildView(
        IReadOnlyList<WordleGuess> guesses,
        string currentInput,
        int attemptsAllowed,
        string? message,
        IReadOnlyDictionary<char, LetterState> keyboardStates,
        TimeSpan elapsed,
        Difficulty level)
    {
        return new BoardView(
            guesses.ToArray(),
            currentInput,
            attemptsAllowed,
            message,
            new Dictionary<char, LetterState>(keyboardStates),
            elapsed,
            level,
            config.WordLanguage);
    }

    // Shows a message until the player acknowledges it, redrawing it on resize.
    private void ShowMessageUntilKey(string headerKey, string messageKey, string? detail)
    {
        var needsRedraw = true;

        while (true)
        {
            if (needsRedraw)
            {
                renderer.DrawMessageScreen(headerKey, messageKey, detail);
                needsRedraw = false;
            }

            if (input.WaitForInput() is null)
            {
                needsRedraw = true;
                continue;
            }

            return;
        }
    }
}
