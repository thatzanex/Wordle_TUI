using WordleApp.Core;
using WordleApp.Services;
using WordleApp.UI;

namespace WordleApp;

// === ENTRY POINT ===
// Loads the configuration and its language, wires up the services, takes over the
// terminal and hands control to the shell. The console session is disposed no
// matter how the application ends, so the cursor and the original colours always
// come back.

/// <summary>
/// Application entry point.
/// </summary>
public static class Program
{
    /// <summary>
    /// Starts the application: reads config.json, prepares the console and runs the shell.
    /// </summary>
    public static void Main()
    {
        var configService = new ConfigService();
        var config = configService.Load();
        var localizer = new LocalizationService(config.Language);

        // The language may have been corrected while loading (unknown code in
        // config.json), so the configuration is kept in sync with what is active.
        config.Language = localizer.CurrentLanguage;

        using var session = ConsoleSession.Start();

        var renderer = new FrontendRenderer(config, localizer);
        var leaderboard = new LeaderboardService();
        var api = new DictionaryApiService(config.ApiEndpoints);
        var words = new WordPool(api);
        var input = new InputReader();

        var shell = new AppShell(config, renderer, localizer, configService, leaderboard, api, words, input);

        shell.Run();
    }
}
