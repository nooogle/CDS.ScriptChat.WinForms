using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Host-supplied configuration for a <see cref="ScriptChatSession"/>.
/// </summary>
/// <remarks>
/// A record so a caller can derive a variant with <c>with</c> — the WinForms panel uses that to
/// fill in a logger factory the host did not set.
/// </remarks>
public sealed record ScriptChatSessionOptions
{
    /// <summary>
    /// Gets the symbol engine backing the <c>lookup_symbol</c> tool. Defaults to
    /// <see cref="NullSymbolLookupProvider"/>, which resolves nothing but still exercises the
    /// tool-calling path.
    /// </summary>
    public ISymbolLookupProvider SymbolLookup { get; init; } = NullSymbolLookupProvider.Instance;

    /// <summary>
    /// Gets the host app's orientation blurb — two or three sentences on what these scripts are
    /// and the shape of the API. Usually produced by
    /// <see cref="HostOrientationResolver.Resolve(IScriptChatHostContext?, string?, ILogger?)"/>.
    /// </summary>
    public string? OrientationBlurb { get; init; }

    /// <summary>
    /// Gets the factory the session logs through, including the
    /// <see cref="Microsoft.Extensions.AI"/> pipeline it builds around the chat client.
    /// <see langword="null"/> — the default — disables logging entirely.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A factory rather than a bare <see cref="ILogger"/> because the session instruments the
    /// whole chat pipeline, not just its own code: function invocation and the provider
    /// round-trips each get their own logger, and so their own category in the log.
    /// </para>
    /// <para>
    /// <b>At <see cref="LogLevel.Trace"/> this records prompt and response content</b> —
    /// the script, the user's messages, the model's replies, and any proposed edit. That is
    /// deliberate, for diagnosing a session that misbehaves, but it means Trace must not be
    /// enabled in a shipping host (D16). Every other level records structure only. API keys are
    /// never logged at any level (D3).
    /// </para>
    /// </remarks>
    public ILoggerFactory? LoggerFactory { get; init; }
}
