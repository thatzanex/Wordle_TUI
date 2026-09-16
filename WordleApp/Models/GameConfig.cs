namespace WordleApp.Models;

// === CONFIGURATION MODEL ===
// Mirrors the structure of config.json one-to-one so the file can be bound
// directly onto these classes and serialised straight back after the player
// changed something in the settings screen. Every value carries a sensible
// default, which means a missing or partial config.json degrades gracefully
// instead of crashing.

/// <summary>
/// Root configuration object for the application, loaded from config.json.
/// </summary>
public class GameConfig
{
    /// <summary>Language code of the interface, for example "de" or "en".</summary>
    public string Language { get; set; } = "de";

    /// <summary>
    /// Language code of the target word and the dictionary used to validate it, for
    /// example "de" or "en". Independent of <see cref="Language"/>, so the interface
    /// and the word being guessed do not have to match.
    /// </summary>
    public string WordLanguage { get; set; } = "de";

    /// <summary>Name of the player, asked once on the first start.</summary>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>Difficulty the player last chose, preselected on the next round.</summary>
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;

    /// <summary>Rules that control how a round of Wordle is played.</summary>
    public GameSettings GameSettings { get; set; } = new();

    /// <summary>Colour palette used by the renderer.</summary>
    public ThemeSettings Theme { get; set; } = new();

    /// <summary>Remote endpoints used for the daily word and guess validation.</summary>
    public ApiSettings ApiEndpoints { get; set; } = new();

    /// <summary>
    /// Forces every setting back into its supported range. Called after loading so
    /// a hand-edited config.json can never produce an unplayable board.
    /// </summary>
    public void Clamp()
    {
        GameSettings.MaxAttempts = Math.Clamp(
            GameSettings.MaxAttempts, GameSettings.MinimumAttempts, GameSettings.MaximumAttempts);

        GameSettings.WordLength = Math.Clamp(
            GameSettings.WordLength, GameSettings.MinimumWordLength, GameSettings.MaximumWordLength);
    }

    /// <summary>
    /// Creates an independent copy, used to snapshot the settings before editing them.
    /// </summary>
    /// <returns>A deep copy of this configuration.</returns>
    public GameConfig Clone()
    {
        return new GameConfig
        {
            Language = Language,
            WordLanguage = WordLanguage,
            PlayerName = PlayerName,
            Difficulty = Difficulty,
            GameSettings = new GameSettings
            {
                MaxAttempts = GameSettings.MaxAttempts,
                WordLength = GameSettings.WordLength
            },
            Theme = new ThemeSettings
            {
                ColorCorrect = Theme.ColorCorrect,
                ColorPresent = Theme.ColorPresent,
                ColorAbsent = Theme.ColorAbsent,
                ColorEmpty = Theme.ColorEmpty,
                AsciiTitleColor = Theme.AsciiTitleColor
            },
            ApiEndpoints = new ApiSettings
            {
                RandomWord = ApiEndpoints.RandomWord,
                DictionaryValidation = ApiEndpoints.DictionaryValidation,
                ValidateGuesses = ApiEndpoints.ValidateGuesses,
                RequestTimeoutSeconds = ApiEndpoints.RequestTimeoutSeconds,
                ValidationTimeoutSeconds = ApiEndpoints.ValidationTimeoutSeconds
            }
        };
    }

    /// <summary>
    /// Overwrites this configuration with the values of another one. The renderer
    /// holds a reference to this instance, so restoring a snapshot has to happen
    /// in place rather than by swapping the object.
    /// </summary>
    /// <param name="other">The configuration to copy from.</param>
    public void CopyFrom(GameConfig other)
    {
        Language = other.Language;
        WordLanguage = other.WordLanguage;
        PlayerName = other.PlayerName;
        Difficulty = other.Difficulty;

        GameSettings.MaxAttempts = other.GameSettings.MaxAttempts;
        GameSettings.WordLength = other.GameSettings.WordLength;

        Theme.ColorCorrect = other.Theme.ColorCorrect;
        Theme.ColorPresent = other.Theme.ColorPresent;
        Theme.ColorAbsent = other.Theme.ColorAbsent;
        Theme.ColorEmpty = other.Theme.ColorEmpty;
        Theme.AsciiTitleColor = other.Theme.AsciiTitleColor;

        ApiEndpoints.RandomWord = other.ApiEndpoints.RandomWord;
        ApiEndpoints.DictionaryValidation = other.ApiEndpoints.DictionaryValidation;
        ApiEndpoints.ValidateGuesses = other.ApiEndpoints.ValidateGuesses;
        ApiEndpoints.RequestTimeoutSeconds = other.ApiEndpoints.RequestTimeoutSeconds;
        ApiEndpoints.ValidationTimeoutSeconds = other.ApiEndpoints.ValidationTimeoutSeconds;
    }
}

/// <summary>
/// Adjustable game rules, such as how many guesses the player gets.
/// </summary>
public class GameSettings
{
    // Bounds for the values the settings screen lets the player cycle through.
    // The upper board size is what still fits a reasonably sized terminal.

    /// <summary>Fewest guesses that can be configured.</summary>
    public const int MinimumAttempts = 3;

    /// <summary>Most guesses that can be configured.</summary>
    public const int MaximumAttempts = 10;

    /// <summary>Shortest target word that can be configured.</summary>
    public const int MinimumWordLength = 3;

    /// <summary>Longest target word that can be configured.</summary>
    public const int MaximumWordLength = 8;

    /// <summary>Number of guesses the player is allowed before losing.</summary>
    public int MaxAttempts { get; set; } = 6;

    /// <summary>Length of the target word, i.e. the number of columns in the grid.</summary>
    public int WordLength { get; set; } = 5;
}

/// <summary>
/// Colour names understood by Spectre.Console (e.g. "green", "orange3", "grey37").
/// </summary>
public class ThemeSettings
{
    /// <summary>Letter is in the word and in the right position.</summary>
    public string ColorCorrect { get; set; } = "green";

    /// <summary>Letter is in the word but in the wrong position.</summary>
    public string ColorPresent { get; set; } = "orange3";

    /// <summary>Letter is not in the word at all.</summary>
    public string ColorAbsent { get; set; } = "grey37";

    /// <summary>Tile that has not been filled in yet.</summary>
    public string ColorEmpty { get; set; } = "grey15";

    /// <summary>Colour of the large ASCII title banner and the panel borders.</summary>
    public string AsciiTitleColor { get; set; } = "orange3";
}

/// <summary>
/// Base addresses for the external services the game talks to.
/// </summary>
public class ApiSettings
{
    // Defaults for the two timeouts, in seconds. The dictionary is the slow one:
    // it answers a word it knows in about a tenth of a second, but takes roughly
    // twenty seconds to admit that it does not know one. Waiting that long after
    // every guess ruins the game, so the guess lookup gets a much tighter budget
    // than the word download.
    private const double DefaultRequestTimeout = 6;
    private const double DefaultValidationTimeout = 1.5;

    // Both addresses are base URLs. The service appends what it needs: length,
    // batch size and language for the random word, language and guess for the
    // dictionary lookup.

    /// <summary>Endpoint that hands out random words.</summary>
    public string RandomWord { get; set; } = "https://random-word-api.herokuapp.com/word";

    /// <summary>Base URL of the dictionary used to verify that a guess is a real word.</summary>
    public string DictionaryValidation { get; set; } = "https://api.dictionaryapi.dev/api/v2/entries";

    /// <summary>
    /// Whether guesses are checked against the dictionary at all. Turning this off
    /// makes every guess of the right length count.
    /// </summary>
    public bool ValidateGuesses { get; set; } = true;

    /// <summary>Seconds a word download may take before it is given up on.</summary>
    public double RequestTimeoutSeconds { get; set; } = DefaultRequestTimeout;

    /// <summary>Seconds a guess lookup may take before the guess is simply accepted.</summary>
    public double ValidationTimeoutSeconds { get; set; } = DefaultValidationTimeout;

    /// <summary>Word download timeout, guarded against unusable values in config.json.</summary>
    /// <returns>The timeout to apply.</returns>
    public TimeSpan ResolveRequestTimeout()
    {
        return TimeSpan.FromSeconds(Math.Clamp(RequestTimeoutSeconds, 0.5, 30));
    }

    /// <summary>Guess lookup timeout, guarded against unusable values in config.json.</summary>
    /// <returns>The timeout to apply.</returns>
    public TimeSpan ResolveValidationTimeout()
    {
        return TimeSpan.FromSeconds(Math.Clamp(ValidationTimeoutSeconds, 0.2, 10));
    }
}
