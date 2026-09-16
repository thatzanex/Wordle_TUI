using System.Net;
using System.Text.Json;
using WordleApp.Models;

namespace WordleApp.Services;

// === DICTIONARY API SERVICE ===
// Talks to the word and dictionary endpoints configured in config.json: one hands
// out target words, the other says whether a guess is a real word.
//
// Both calls are treated as unreliable, and slow, on purpose:
//
//  * Every request carries its own short timeout. The dictionary needs about
//    0.1s for a word it knows, but roughly twenty seconds to admit that it does
//    not know one - waiting that long after every guess is what made the game
//    feel broken, so the wait is capped at a fraction of a second.
//  * Validation fails open: only an explicit "not found" rejects a guess.
//  * When the configured dictionary does not give a clear answer at all (down,
//    erroring, timing out), the guess is checked against Wiktionary instead of
//    being accepted outright - it runs on Wikimedia's infrastructure, which is
//    far more reliable than the small free dictionary APIs this game otherwise
//    depends on.
//  * Only when that fallback also fails to answer, twice in a row, is validation
//    dropped for the rest of the session - so a genuinely dead network stalls the
//    game exactly twice and never again.
//  * Answers are remembered, so the same word is never looked up twice.

/// <summary>
/// Fetches target words and validates guesses against an online dictionary.
/// </summary>
public class DictionaryApiService
{
    // How often a badly shaped random word is retried before giving up.
    private const int MaximumWordAttempts = 4;

    // Timeouts a slow endpoint is never allowed to exceed.
    private static readonly TimeSpan HardTimeout = TimeSpan.FromSeconds(30);

    // Consecutive dictionary failures after which validation is switched off.
    private const int FailuresBeforeGivingUp = 2;

    private static readonly HttpClient Client = new() { Timeout = HardTimeout };

    private readonly ApiSettings endpoints;

    // Words already looked up in this session, mapped to what the dictionary said.
    private readonly Dictionary<string, bool> validationCache = new(StringComparer.OrdinalIgnoreCase);

    private int consecutiveValidationFailures;

    /// <summary>
    /// Creates the service from the endpoint section of the configuration.
    /// </summary>
    /// <param name="endpoints">Endpoint addresses and timeouts loaded from config.json.</param>
    public DictionaryApiService(ApiSettings endpoints)
    {
        this.endpoints = endpoints;
    }

    /// <summary>Message describing the last failed request, or null when all is well.</summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// False when guesses are currently accepted without asking the dictionary,
    /// either because the player turned validation off or because the endpoint
    /// stopped answering.
    /// </summary>
    public bool IsValidationActive => endpoints.ValidateGuesses
        && consecutiveValidationFailures < FailuresBeforeGivingUp;

    // === TARGET WORDS ===

    /// <summary>
    /// Fetches several playable words in a single request.
    /// </summary>
    /// <param name="count">How many words to ask for.</param>
    /// <param name="length">Number of letters each word must have.</param>
    /// <param name="language">Language code of the word list, for example "en".</param>
    /// <returns>The usable words in uppercase; empty when the endpoint failed.</returns>
    /// <remarks>Virtual so a test can play a round against a fixed word.</remarks>
    public virtual async Task<IReadOnlyList<string>> FetchWordsAsync(int count, int length, string language)
    {
        LastError = null;

        // The endpoint sometimes answers with words that cannot be typed on the
        // board, so a batch that comes back empty is asked for again.
        for (var attempt = 0; attempt < MaximumWordAttempts; attempt++)
        {
            var batch = await RequestWordsAsync(count, length, language).ConfigureAwait(false);

            var usable = batch
                .Where(word => IsUsableWord(word, length))
                .Select(word => word.ToUpperInvariant())
                .Distinct()
                .ToArray();

            if (usable.Length > 0)
            {
                return usable;
            }
        }

        LastError ??= "The word service did not return a usable word.";

        return Array.Empty<string>();
    }

    // Performs a single call to the random word endpoint.
    private async Task<IReadOnlyList<string>> RequestWordsAsync(int count, int length, string language)
    {
        try
        {
            using var timeout = new CancellationTokenSource(endpoints.ResolveRequestTimeout());

            var url = BuildRandomWordUrl(count, length, language);
            var json = await Client.GetStringAsync(url, timeout.Token).ConfigureAwait(false);

            // The endpoint answers with a JSON array of words.
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
        {
            LastError = DescribeFailure(exception);

            return Array.Empty<string>();
        }
    }

    // Appends the batch size, length and language to the configured base address,
    // replacing any query string the configuration may already carry.
    private string BuildRandomWordUrl(int count, int length, string language)
    {
        var baseUrl = endpoints.RandomWord.Split('?')[0].TrimEnd('/');

        return $"{baseUrl}?length={length}&number={Math.Max(1, count)}&lang={language}";
    }

    // Only plain A-Z words of the requested length can be played.
    private static bool IsUsableWord(string word, int length)
    {
        return word.Length == length && word.All(letter => letter is >= 'a' and <= 'z' or >= 'A' and <= 'Z');
    }

    // === GUESS VALIDATION ===

    /// <summary>
    /// Checks whether a guess exists in the dictionary.
    /// </summary>
    /// <param name="word">The word to look up.</param>
    /// <param name="language">Language code of the dictionary, for example "en".</param>
    /// <returns>
    /// False only when a dictionary explicitly reports the word as unknown.
    /// A disabled, unreachable or slow dictionary returns true, so nothing ever
    /// blocks the player for longer than the configured validation timeout.
    /// </returns>
    /// <remarks>Virtual so a test can run without touching the network.</remarks>
    public virtual async Task<bool> IsValidWordAsync(string word, string language)
    {
        if (!IsValidationActive)
        {
            return true;
        }

        var key = $"{language}:{word}";
        if (validationCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var primary = await CheckPrimaryDictionaryAsync(word, language).ConfigureAwait(false);
        if (primary is { } primaryResult)
        {
            consecutiveValidationFailures = 0;
            validationCache[key] = primaryResult;

            return primaryResult;
        }

        // The configured dictionary did not give a clear answer (down, erroring or
        // timed out). Wiktionary runs on Wikimedia's infrastructure and rarely has
        // the kind of outage the primary dictionary just had, so it gets a shot
        // before the lookup counts as a failure.
        var fallback = await CheckWiktionaryAsync(word, language).ConfigureAwait(false);
        if (fallback is { } fallbackResult)
        {
            consecutiveValidationFailures = 0;
            validationCache[key] = fallbackResult;

            return fallbackResult;
        }

        RegisterValidationFailure(LastError ?? "Neither dictionary answered.");

        return true;
    }

    // Asks the endpoint configured in config.json. Returns null - instead of
    // failing open itself - when it could not give a clear answer, so the caller
    // can try the Wiktionary fallback first.
    private async Task<bool?> CheckPrimaryDictionaryAsync(string word, string language)
    {
        try
        {
            using var timeout = new CancellationTokenSource(endpoints.ResolveValidationTimeout());

            using var response = await Client
                .GetAsync(BuildValidationUrl(word, language), HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            // Only a clear "no such word" counts as a rejection. Server errors mean
            // the dictionary is having a bad day, not that the guess is wrong.
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound)
            {
                LastError = null;

                return response.StatusCode == HttpStatusCode.OK;
            }

            LastError = $"The dictionary answered {(int)response.StatusCode}.";

            return null;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            LastError = DescribeFailure(exception);

            return null;
        }
    }

    // Falls back to the language's Wiktionary, asking whether a page for the word
    // exists. German capitalises nouns but not other words, so both the given
    // spelling and a capitalised variant are tried before giving up.
    private async Task<bool?> CheckWiktionaryAsync(string word, string language)
    {
        var lowered = word.ToLowerInvariant();
        var capitalised = char.ToUpperInvariant(lowered[0]) + lowered[1..];

        foreach (var candidate in new[] { lowered, capitalised }.Distinct())
        {
            var exists = await CheckWiktionaryPageAsync(candidate, language).ConfigureAwait(false);
            if (exists is null)
            {
                // Wiktionary itself did not answer; a second spelling would not either.
                return null;
            }

            if (exists.Value)
            {
                return true;
            }
        }

        return false;
    }

    // Looks up a single page title on <language>.wiktionary.org.
    private async Task<bool?> CheckWiktionaryPageAsync(string title, string language)
    {
        try
        {
            using var timeout = new CancellationTokenSource(endpoints.ResolveValidationTimeout());

            var url = "https://" + language + ".wiktionary.org/w/api.php" +
                "?action=query&format=json&formatversion=2&titles=" + Uri.EscapeDataString(title);

            var json = await Client.GetStringAsync(url, timeout.Token).ConfigureAwait(false);

            using var document = JsonDocument.Parse(json);
            var pages = document.RootElement.GetProperty("query").GetProperty("pages");

            if (pages.GetArrayLength() == 0)
            {
                return false;
            }

            var page = pages[0];

            return !(page.TryGetProperty("missing", out var missing) && missing.GetBoolean());
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
        {
            return null;
        }
    }

    // Counts a failed lookup. Once the dictionary has failed twice in a row it is
    // left alone for the rest of the session instead of stalling every guess.
    private void RegisterValidationFailure(string message)
    {
        consecutiveValidationFailures++;
        LastError = message;
    }

    // Builds "<base>/<language>/<word>". Older configuration files ended the base
    // address with a language segment of their own, which is dropped here.
    private string BuildValidationUrl(string word, string language)
    {
        var baseUrl = endpoints.DictionaryValidation.Split('?')[0].TrimEnd('/');
        var lastSegment = baseUrl[(baseUrl.LastIndexOf('/') + 1)..];

        if (lastSegment.Length == 2 && lastSegment.All(char.IsLetter))
        {
            baseUrl = baseUrl[..baseUrl.LastIndexOf('/')];
        }

        return $"{baseUrl}/{language}/{Uri.EscapeDataString(word.ToLowerInvariant())}";
    }

    // A cancelled request is a timeout of ours, not a fault of the caller.
    private static string DescribeFailure(Exception exception)
    {
        return exception is OperationCanceledException
            ? "The service did not answer in time."
            : exception.Message;
    }
}
