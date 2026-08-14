using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Wraps a host-supplied <see cref="ILoggerFactory"/> so that no logger handed out through it —
/// including to third-party components such as <c>Microsoft.Extensions.AI</c>'s
/// function-invocation and chat-client logging — can ever report <see cref="LogLevel.Trace"/> as
/// enabled, no matter how the host's own logging pipeline is configured (D17).
/// </summary>
/// <remarks>
/// <para>
/// This exists because <c>Trace</c>-level content logging isn't only a risk from code in this
/// repository: <c>Microsoft.Extensions.AI</c>'s own <c>FunctionInvokingChatClient</c> logs full
/// function arguments and results at <c>Trace</c> (which, for <c>propose_script_edit</c>, is the
/// entire proposed script), and its <c>LoggingChatClient</c> logs full message and option content
/// at <c>Trace</c> too — both entirely independent of anything <c>ScriptChatSession</c> logs
/// itself. Removing this library's own content-bearing log messages does not touch either of
/// those. Refusing to ever report <c>Trace</c> as enabled, at the one point every logger this
/// library or its dependencies use passes through, closes both — and any future dependency that
/// adds its own <c>Trace</c>-level content logging, without this needing to change.
/// </para>
/// <para>
/// This is a hard boundary, not a default that can be reconfigured back on. Even if something
/// elsewhere in the host process — a misconfiguration, or code in another library sharing the
/// same logging pipeline — sets the underlying provider's minimum level to <c>Trace</c>, the
/// loggers this library and its dependencies see through this wrapper still never report it as
/// enabled, so no prompt, script, or response content is ever written.
/// </para>
/// </remarks>
internal sealed class TraceSuppressingLoggerFactory(ILoggerFactory inner) : ILoggerFactory
{
    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider) => inner.AddProvider(provider);

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new TraceSuppressingLogger(inner.CreateLogger(categoryName));

    /// <summary>
    /// Does nothing — this wrapper does not own the inner <see cref="ILoggerFactory"/>. The host
    /// constructed it and is responsible for disposing it.
    /// </summary>
    public void Dispose()
    {
    }

    private sealed class TraceSuppressingLogger(ILogger inner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.Trace && inner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Trace)
            {
                return;
            }

            inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
