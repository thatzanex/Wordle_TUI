using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using WordleApp.Models;

namespace WordleApp.Services;

// === CONFIG SERVICE ===
// Reads config.json on startup and writes it back when the player changes
// something in the settings screen. The file lives next to the executable, so
// edits made in game survive the next start.

/// <summary>
/// Loads and saves the user-editable configuration file.
/// </summary>
public class ConfigService
{
    private const string ConfigFileName = "config.json";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,

        // The difficulty is written as "Normal" rather than as a number, so the
        // file stays readable and hand-editable.
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string configPath;

    /// <summary>
    /// Creates a service bound to the config.json next to the application.
    /// </summary>
    public ConfigService()
    {
        configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
    }

    /// <summary>Full path of the configuration file, shown on the settings screen.</summary>
    public string ConfigPath => configPath;

    /// <summary>Message describing the last failed save, or null when all is well.</summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Reads config.json and binds it onto a <see cref="GameConfig"/>.
    /// A missing or malformed file falls back to the defaults on the model,
    /// so the game always starts.
    /// </summary>
    /// <returns>The effective configuration.</returns>
    public GameConfig Load()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(ConfigFileName, optional: true, reloadOnChange: false)
                .Build();

            var config = new GameConfig();
            configuration.Bind(config);
            config.Clamp();

            return config;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or FormatException)
        {
            LastError = exception.Message;
            return new GameConfig();
        }
    }

    /// <summary>
    /// Writes the current configuration back to config.json.
    /// </summary>
    /// <param name="config">The configuration to persist.</param>
    /// <returns>True when the file was written successfully.</returns>
    public bool Save(GameConfig config)
    {
        try
        {
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, WriteOptions));
            LastError = null;

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A read-only install directory must not crash the game; the settings
            // screen keeps the change in memory and reports the failure instead.
            LastError = exception.Message;

            return false;
        }
    }
}
