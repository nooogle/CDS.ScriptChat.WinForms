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
    /// Command-line arguments. <c>--trace</c> is the only one recognised: it opts into
    /// content-bearing logging (see <see cref="LoggerFactory"/> setup below). Without it, this
    /// run records no prompt, reply, or script content anywhere (D3, D17).
    /// </param>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var traceRequested = args.Contains("--trace", StringComparer.OrdinalIgnoreCase);

        // Constructed here, not inside the builder: neither the logger factory nor the DI
        // container disposes a provider it did not create, so this scope is what closes the file.
        using var csvProvider = new CsvLoggerProvider(CsvLoggerProvider.BuildRunLogPath(ApplicationName));

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            // Information by default: at that level the log carries only structure — no prompt,
            // reply, or script content (D16). Trace is diagnostic-only and carries all of that
            // content, so per D17 it must never be the default here or in any consuming host —
            // only an explicit, deliberate --trace on the command line turns it on.
            builder.SetMinimumLevel(traceRequested ? LogLevel.Trace : LogLevel.Information);
            builder.AddProvider(csvProvider);
            builder.AddDebug();
        });

        Application.Run(new MainForm(loggerFactory, csvProvider.FilePath, traceRequested));
    }
}
