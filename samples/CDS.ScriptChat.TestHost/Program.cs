using CDS.ScriptChat.TestHost.Logging;

using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.TestHost;

/// <summary>Entry point for the test host.</summary>
internal static class Program
{
    /// <summary>The name this host keeps its stored keys and its logs under.</summary>
    public const string ApplicationName = "CDS.ScriptChat.TestHost";

    /// <summary>Runs the test host.</summary>
    /// <param name="args">
    /// Recognises one switch, <c>--demo=markdown</c>: seeds the transcript with a canned
    /// Markdown-bearing turn (see <see cref="Demo.MarkdownDemo"/>) instead of requiring a
    /// configured provider key, for UI-automation coverage against a real window.
    /// </param>
    [STAThread]
    private static void Main(string[] args)
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

        var mainForm = new MainForm(loggerFactory, csvProvider.FilePath);

        if (Array.IndexOf(args, "--demo=markdown") >= 0)
        {
            mainForm.SeedMarkdownDemoAsync().GetAwaiter().GetResult();
        }

        Application.Run(mainForm);
    }
}
