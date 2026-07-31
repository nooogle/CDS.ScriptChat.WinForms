namespace CDS.ScriptChat.Core;

/// <summary>
/// Supplies the host app's orientation blurb in code, for hosts where a property is more
/// natural than a file (D12).
/// </summary>
public interface IScriptChatHostContext
{
    /// <summary>
    /// Two or three sentences orienting the model: what kind of scripts these are and the
    /// top-level shape of the API. Detail is fetched on demand via <c>lookup_symbol</c>
    /// rather than front-loaded here (D4).
    /// </summary>
    /// <remarks>
    /// Return <see langword="null"/> or whitespace to supply no orientation at all.
    /// </remarks>
    string? OrientationBlurb { get; }
}
