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
            // Trace, because this host exists to diagnose the panel: at Trace the log carries
            // the prompts, the replies, and the proposed scripts as well as the structure. A
            // shipping host would stop at Information and record no user content at all (D16).
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(csvProvider);
            builder.AddDebug();
        });

        Application.Run(new MainForm(loggerFactory, csvProvider.FilePath));
    }
}
