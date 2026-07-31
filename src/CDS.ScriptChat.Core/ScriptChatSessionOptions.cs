using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Host-supplied configuration for a <see cref="ScriptChatSession"/>.
/// </summary>
public sealed class ScriptChatSessionOptions
{
    /// <summary>
    /// Gets the symbol engine backing the <c>lookup_symbol</c> tool. Defaults to
    /// <see cref="NullSymbolLookupProvider"/>, which resolves nothing but still exercises the
    /// tool-calling path.
    /// </summary>
    public ISymbolLookupProvider SymbolLookup { get; init; } = NullSymbolLookupProvider.Instance;

    /// <summary>
    /// Gets the host app's orientation blurb — two or three sentences on what these scripts are
    /// and the shape of the API. Usually produced by <see cref="HostOrientationResolver.Resolve"/>.
    /// </summary>
    public string? OrientationBlurb { get; init; }

    /// <summary>
    /// Gets the logger. Only tool dispatch and turn structure are logged; prompt and response
    /// content never are (D3).
    /// </summary>
    public ILogger Logger { get; init; } = NullLogger.Instance;
}
