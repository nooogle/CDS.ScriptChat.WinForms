using CDS.ScriptChat.TestHost.Logging;

using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.TestHost;

/// <summary>Entry point for the test host.</summary>
internal static class Program
{
    /// <summary>The name this host keeps its stored keys and its logs under.</summary>
    public const string ApplicationName = "CDS.ScriptChat.TestHost";

    /// <summary>Runs the test host.</summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Constructed here, not inside the builder: neither the logger factory nor the DI
        // container disposes a provider it did not create, so this scope is what closes the file.
        using var csvProvider = new CsvLoggerProvider(CsvLoggerProvider.BuildRunLogPath(ApplicationName));

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            // Information carries only structure — no prompt, reply, or script content (D16).
            // There is no opt-in to raise this to Trace: CDS.ScriptChat has no content-bearing
            // log message left at any level, and ScriptChatSession wraps every ILoggerFactory it
            // is given so that Trace is unreachable even for its dependencies' own logging, no
            // matter how this provider's minimum level is configured (D17). Raising this to
            // Trace would not reveal anything; it would just be a misleading thing to offer.
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(csvProvider);
            builder.AddDebug();
        });

        Application.Run(new MainForm(loggerFactory, csvProvider.FilePath));
    }
}
