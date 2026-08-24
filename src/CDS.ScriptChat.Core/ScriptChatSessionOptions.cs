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
    /// Builds the options for a host that wants the batteries-included path: symbol lookup and
    /// an orientation blurb, both derived from its own API types.
    /// </summary>
    /// <param name="api">
    /// The globals type a script is compiled against, or — for a host with no globals
    /// indirection — its API class. Drives <em>both</em> the orientation index and
    /// <c>lookup_symbol</c>, so what the model is told exists and what it can then ask about
    /// cannot drift apart.
    /// </param>
    /// <param name="additionalTypes">
    /// Types a script works with that <paramref name="api"/> does not itself expose.
    /// </param>
    /// <returns>Options ready to hand to a <see cref="ScriptChatSession"/>.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public static ScriptChatSessionOptions ForHostApi(Type api, params Type[] additionalTypes) =>
        ForHostApi(api, loggerFactory: null, additionalTypes);

    /// <summary>
    /// Builds the batteries-included options, logging where the orientation blurb came from.
    /// </summary>
    /// <param name="api">The globals type, or a flat API class.</param>
    /// <param name="loggerFactory">
    /// Where to record which source supplied the orientation prose. Worth passing: "the context
    /// file was not deployed beside the executable" is the commonest reason a host's orientation
    /// silently fails to reach the model. No blurb text is logged, at any level (D17).
    /// </param>
    /// <param name="additionalTypes">Types a script works with that <paramref name="api"/> does not expose.</param>
    /// <returns>Options ready to hand to a <see cref="ScriptChatSession"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="api"/> or <paramref name="additionalTypes"/> is <see langword="null"/>.</exception>
    public static ScriptChatSessionOptions ForHostApi(
        Type api,
        ILoggerFactory? loggerFactory,
        params Type[] additionalTypes)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(additionalTypes);

        return new ScriptChatSessionOptions
        {
            SymbolLookup = HostApiLookup.Create(api, additionalTypes),
            OrientationBlurb = HostApiLookup.BuildOrientation(api, additionalTypes, loggerFactory),
            LoggerFactory = loggerFactory,
        };
    }

    /// <summary>
    /// Gets the symbol engine backing the <c>lookup_symbol</c> tool. Defaults to
    /// <see cref="NullSymbolLookupProvider"/>, which resolves nothing.
    /// </summary>
    /// <remarks>
    /// While this is <see cref="NullSymbolLookupProvider"/>, <c>lookup_symbol</c> is not offered
    /// to the model at all. Advertising a lookup that answers "not found" to everything is worse
    /// than not having one: the model calls it, disbelieves the answer, and burns turns
    /// concluding the host's API is not real.
    /// </remarks>
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
