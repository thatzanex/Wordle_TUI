using System.Text.Json;

namespace WordleApp.Services;

// === LOCALIZATION ===
// Every piece of text the user sees comes from a translation file in
// Resources/Languages. English is the source language and therefore the fallback:
// a key missing from another language falls back to the English text, and a key
// missing everywhere falls back to the key itself, so the UI never shows a blank.
// German is the language the game starts in (see config.json).

/// <summary>
/// Loads the translation files and resolves UI text for the active language.
/// </summary>
public class LocalizationService
{
    /// <summary>Language code of the source language, used as the fallback.</summary>
    public const string FallbackLanguage = "en";

    /// <summary>Language the game uses when config.json does not say otherwise.</summary>
    public const string DefaultLanguage = "de";

    private readonly string languageDirectory;
    private readonly Dictionary<string, string> fallbackTexts;
    private Dictionary<string, string> texts;

    /// <summary>
    /// Loads the available languages and activates the requested one.
    /// </summary>
    /// <param name="language">Language code from config.json, for example "de".</param>
    public LocalizationService(string language)
    {
        languageDirectory = Path.Combine(AppContext.BaseDirectory, "Resources", "Languages");

        fallbackTexts = LoadLanguageFile(FallbackLanguage);
        AvailableLanguages = DiscoverLanguages();

        texts = fallbackTexts;
        CurrentLanguage = FallbackLanguage;
        SetLanguage(language);
    }

    /// <summary>Language code currently in use.</summary>
    public string CurrentLanguage { get; private set; }

    /// <summary>All language codes that have a translation file, alphabetically sorted.</summary>
    public IReadOnlyList<string> AvailableLanguages { get; }

    // === PUBLIC API ===

    /// <summary>
    /// Switches the active language. Unknown codes fall back to the default language.
    /// </summary>
    /// <param name="language">Language code, for example "en" or "de".</param>
    public void SetLanguage(string language)
    {
        var normalized = Normalize(language);

        texts = normalized == FallbackLanguage ? fallbackTexts : LoadLanguageFile(normalized);
        CurrentLanguage = normalized;
    }

    /// <summary>
    /// Resolves a text by key in the active language.
    /// </summary>
    /// <param name="key">Translation key, for example "menu.play".</param>
    /// <returns>The translated text, the English text, or the key itself.</returns>
    public string Get(string key)
    {
        if (texts.TryGetValue(key, out var text))
        {
            return text;
        }

        return fallbackTexts.TryGetValue(key, out var fallback) ? fallback : key;
    }

    /// <summary>
    /// Resolves a text by key and fills its numbered placeholders.
    /// </summary>
    /// <param name="key">Translation key of a text containing {0}, {1}, ...</param>
    /// <param name="arguments">Values to insert, in placeholder order.</param>
    /// <returns>The formatted text.</returns>
    public string Format(string key, params object[] arguments)
    {
        return string.Format(Get(key), arguments);
    }

    /// <summary>
    /// Human-readable name of a language, taken from its own translation file
    /// (so German is offered as "Deutsch", not as "German").
    /// </summary>
    /// <param name="language">Language code to describe.</param>
    /// <returns>The display name, or the uppercased code when the file has none.</returns>
    public string GetLanguageName(string language)
    {
        var normalized = Normalize(language);

        if (normalized == CurrentLanguage)
        {
            return Get("language.name");
        }

        var file = LoadLanguageFile(normalized);

        return file.TryGetValue("language.name", out var name) ? name : normalized.ToUpperInvariant();
    }

    // === FILE HANDLING ===

    // Maps an arbitrary input onto a language that actually exists on disk.
    private string Normalize(string? language)
    {
        var candidate = (language ?? string.Empty).Trim().ToLowerInvariant();

        if (LanguageFileExists(candidate))
        {
            return candidate;
        }

        return LanguageFileExists(DefaultLanguage) ? DefaultLanguage : FallbackLanguage;
    }

    private bool LanguageFileExists(string language)
    {
        return language.Length > 0 && File.Exists(BuildPath(language));
    }

    private string BuildPath(string language)
    {
        return Path.Combine(languageDirectory, $"{language}.json");
    }

    // Reads one translation file. A missing or broken file yields an empty
    // dictionary, which simply means every key falls through to English.
    private Dictionary<string, string> LoadLanguageFile(string language)
    {
        try
        {
            var path = BuildPath(language);
            if (!File.Exists(path))
            {
                return new Dictionary<string, string>();
            }

            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            return parsed ?? new Dictionary<string, string>();
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    // Every *.json file in the language folder is offered in the settings screen,
    // so adding a new language means dropping in a new file - no code change.
    private IReadOnlyList<string> DiscoverLanguages()
    {
        try
        {
            if (!Directory.Exists(languageDirectory))
            {
                return new[] { FallbackLanguage };
            }

            var languages = Directory
                .EnumerateFiles(languageDirectory, "*.json")
                .Select(path => Path.GetFileNameWithoutExtension(path).ToLowerInvariant())
                .Where(code => code.Length > 0)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();

            return languages.Length > 0 ? languages : new[] { FallbackLanguage };
        }
        catch (IOException)
        {
            return new[] { FallbackLanguage };
        }
    }
}
