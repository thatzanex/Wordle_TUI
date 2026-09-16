using WordleApp.Models;

namespace WordleApp.Services;

// === WORD POOL ===
// Keeps a handful of ready-to-play words in memory so a new round starts without
// waiting for the network. Words are fetched in batches - one request returns ten
// of them for the same price as one - and the pool is topped up in the background
// while the player is busy typing.
//
// The pool lives for the session only. Nothing is written to disk, so the words
// still come from the API on every start.

/// <summary>
/// Buffers target words per language and word length, and refills itself in the background.
/// </summary>
public class WordPool
{
    // How many words one request asks for.
    private const int BatchSize = 10;

    // A refill starts as soon as the buffer falls to this size.
    private const int RefillThreshold = 3;

    private readonly DictionaryApiService api;
    private readonly object gate = new();
    private readonly Dictionary<string, Queue<string>> buffers = new();
    private readonly HashSet<string> refillsInFlight = new();

    /// <summary>
    /// Creates a pool that draws its words from the given service.
    /// </summary>
    /// <param name="api">The service used to fetch words.</param>
    public WordPool(DictionaryApiService api)
    {
        this.api = api;
    }

    // === TAKING WORDS ===

    /// <summary>
    /// Takes a buffered word without contacting the network.
    /// </summary>
    /// <param name="length">Word length the round needs.</param>
    /// <param name="language">Language the round is played in.</param>
    /// <param name="word">The word that was taken, when one was available.</param>
    /// <returns>True when a word was available right away.</returns>
    public bool TryTake(int length, string language, out string word)
    {
        var key = BuildKey(length, language);

        lock (gate)
        {
            if (buffers.TryGetValue(key, out var buffer) && buffer.Count > 0)
            {
                word = buffer.Dequeue();

                // Refill early, so the next round finds a word waiting again.
                if (buffer.Count <= RefillThreshold)
                {
                    StartRefill(length, language);
                }

                return true;
            }
        }

        word = string.Empty;

        return false;
    }

    /// <summary>
    /// Fetches a word, waiting for the network only when the buffer is empty.
    /// </summary>
    /// <param name="length">Word length the round needs.</param>
    /// <param name="language">Language the round is played in.</param>
    /// <returns>A word, or null when the service could not deliver one.</returns>
    public string? Take(int length, string language)
    {
        if (TryTake(length, language, out var buffered))
        {
            return buffered;
        }

        var words = api.FetchWordsAsync(BatchSize, length, language).GetAwaiter().GetResult();
        if (words.Count == 0)
        {
            return null;
        }

        Store(length, language, words);

        return TryTake(length, language, out var word) ? word : null;
    }

    // === FILLING ===

    /// <summary>
    /// Starts filling the buffer in the background, for example while the loading
    /// screen is running or after the word length was changed in the settings.
    /// </summary>
    /// <param name="length">Word length to prepare.</param>
    /// <param name="language">Language to prepare.</param>
    public void WarmUp(int length, string language)
    {
        lock (gate)
        {
            var key = BuildKey(length, language);
            var count = buffers.TryGetValue(key, out var buffer) ? buffer.Count : 0;

            if (count <= RefillThreshold)
            {
                StartRefill(length, language);
            }
        }
    }

    // Kicks off one background fetch per bucket. Must be called inside the lock.
    private void StartRefill(int length, string language)
    {
        var key = BuildKey(length, language);

        // Two refills for the same bucket would just waste requests.
        if (!refillsInFlight.Add(key))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var words = await api.FetchWordsAsync(BatchSize, length, language).ConfigureAwait(false);
                Store(length, language, words);
            }
            finally
            {
                lock (gate)
                {
                    refillsInFlight.Remove(key);
                }
            }
        });
    }

    // Adds fetched words to their bucket, skipping ones that are already queued.
    private void Store(int length, string language, IReadOnlyList<string> words)
    {
        if (words.Count == 0)
        {
            return;
        }

        lock (gate)
        {
            var key = BuildKey(length, language);
            if (!buffers.TryGetValue(key, out var buffer))
            {
                buffer = new Queue<string>();
                buffers[key] = buffer;
            }

            foreach (var word in words.Where(word => !buffer.Contains(word)))
            {
                buffer.Enqueue(word);
            }
        }
    }

    private static string BuildKey(int length, string language)
    {
        return $"{language}:{length}";
    }
}
