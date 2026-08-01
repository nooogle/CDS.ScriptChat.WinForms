using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// An <see cref="ILoggerFactory"/> that keeps every message in memory, so tests can assert on
/// what was logged — and, just as importantly, on what was not.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerFactory, ILoggerProvider
{
    private readonly Lock _gate = new();
    private readonly List<CapturedLogEntry> _entries = [];
    private readonly LogLevel _minimumLevel;

    /// <summary>
    /// Creates a factory capturing everything at or above <paramref name="minimumLevel"/>.
    /// </summary>
    /// <param name="minimumLevel">
    /// The floor to capture from. Defaults to <see cref="LogLevel.Trace"/>; raise it to
    /// reproduce what a host that does not enable Trace would record.
    /// </param>
    public CapturingLoggerProvider(LogLevel minimumLevel = LogLevel.Trace)
    {
        _minimumLevel = minimumLevel;
    }

    /// <summary>Gets a snapshot of everything captured so far, oldest first.</summary>
    public IReadOnlyList<CapturedLogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private void Capture(CapturedLogEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly CapturingLoggerProvider _owner;
        private readonly string _category;

        public CapturingLogger(CapturingLoggerProvider owner, string category)
        {
            _owner = owner;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _owner._minimumLevel && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _owner.Capture(new CapturedLogEntry(
                logLevel,
                _category,
                eventId,
                formatter(state, exception),
                exception));
        }
    }
}

/// <summary>One captured log message.</summary>
/// <param name="Level">The level it was written at.</param>
/// <param name="Category">The category of the logger that wrote it.</param>
/// <param name="EventId">Its event ID and name.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Exception">The exception attached to it, if any.</param>
internal sealed record CapturedLogEntry(
    LogLevel Level,
    string Category,
    EventId EventId,
    string Message,
    Exception? Exception);
