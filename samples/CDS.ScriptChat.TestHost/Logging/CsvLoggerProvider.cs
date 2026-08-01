using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.TestHost.Logging;

/// <summary>
/// A deliberately small <see cref="ILoggerProvider"/> that writes every message to one CSV file,
/// so a run of the test host can be opened in a spreadsheet and read as a table of calls,
/// results, timings, and errors.
/// </summary>
/// <remarks>
/// <para>
/// This lives in the sample rather than the library on purpose: nothing use-case-specific ships
/// in <c>CDS.ScriptChat</c> (D15), and a consuming app will already have its own logging setup.
/// It is here as the worked example of how to see what the panel is doing — the same role
/// <see cref="KestrelSymbolLookupProvider"/> plays for symbol lookup.
/// </para>
/// <para>
/// <b>The caller owns disposal.</b> Construct it, hand it to
/// <see cref="LoggingBuilderExtensions.AddProvider"/>, and dispose it yourself. Neither the
/// logger factory nor the DI container disposes a provider it did not create, so a provider left
/// to them keeps its file handle open for the life of the process.
/// </para>
/// <para>
/// <b>The file can contain the user's script and the model's replies</b>, because the test host
/// runs at <see cref="LogLevel.Trace"/> (D16). Treat it as you would the script itself: it is
/// written under the current user's local application data, and is not sent anywhere. API keys
/// are never written to it at any level (D3).
/// </para>
/// </remarks>
internal sealed class CsvLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentDictionary<string, CsvLogger> _loggers = new(StringComparer.Ordinal);
    private readonly CsvLogWriter _writer;
    private IExternalScopeProvider? _scopeProvider;

    /// <summary>
    /// Creates a provider writing to <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Where to write. Missing directories are created.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or whitespace.</exception>
    public CsvLoggerProvider(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        FilePath = Path.GetFullPath(filePath);
        _writer = CsvLogWriter.Create(FilePath);
    }

    /// <summary>Gets the absolute path of the file being written, for the host to show the user.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Builds the path one run of a host should log to: a file per run, named for the moment it
    /// started, under the current user's local application data.
    /// </summary>
    /// <param name="applicationName">The host application's name, used as the folder name.</param>
    /// <returns>An absolute path that no other run will collide with.</returns>
    /// <exception cref="ArgumentException"><paramref name="applicationName"/> is empty or whitespace.</exception>
    /// <remarks>
    /// A file per run rather than a rolling one: runs are short, and comparing "the run that
    /// worked" against "the run that did not" is most of the value of having the log at all.
    /// </remarks>
    public static string BuildRunLogPath(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            applicationName,
            "logs",
            $"scriptchat-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.csv");
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(
            categoryName,
            static (name, provider) => new CsvLogger(name, provider._writer, () => provider._scopeProvider),
            this);
    }

    /// <inheritdoc />
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _loggers.Clear();
        _writer.Dispose();
    }
}
