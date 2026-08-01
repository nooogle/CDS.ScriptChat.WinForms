using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.TestHost.Logging;

/// <summary>
/// Writes one category's messages into the shared CSV file.
/// </summary>
internal sealed class CsvLogger : ILogger
{
    private readonly string _category;
    private readonly CsvLogWriter _writer;
    private readonly Func<IExternalScopeProvider?> _scopeProvider;

    public CsvLogger(string category, CsvLogWriter writer, Func<IExternalScopeProvider?> scopeProvider)
    {
        _category = category;
        _writer = writer;
        _scopeProvider = scopeProvider;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return _scopeProvider()?.Push(state);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Always true: which levels reach the provider is the logging builder's decision, expressed
    /// as filters, and duplicating that here would let the two disagree.
    /// </remarks>
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!IsEnabled(logLevel))
        {
            return;
        }

        _writer.WriteRow(
        [
            CsvLogWriter.FormatTimestamp(DateTimeOffset.Now),
            logLevel.ToString(),
            _category,
            eventId.Id.ToString(CultureInfo.InvariantCulture),
            eventId.Name,
            Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture),
            FormatScopes(),
            formatter(state, exception),
            exception?.ToString(),
        ]);
    }

    /// <summary>
    /// Flattens the active scopes into one cell, so a row can be traced back to whatever the
    /// host had in scope when it was written.
    /// </summary>
    private string? FormatScopes()
    {
        var provider = _scopeProvider();
        if (provider is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        provider.ForEachScope(
            static (scope, state) =>
            {
                if (state.Length > 0)
                {
                    state.Append(" => ");
                }

                state.Append(scope);
            },
            builder);

        return builder.Length == 0 ? null : builder.ToString();
    }
}
