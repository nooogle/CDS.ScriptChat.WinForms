using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Resolves the host app's orientation blurb from the two sources D12 allows: a conventional
/// markdown file, checked first, then a host-supplied property.
/// </summary>
/// <remarks>
/// The file is read verbatim rather than parsed. D12 only ever needs one string out, so a
/// structured format would buy a parser dependency and a schema to version for no gain — and
/// the content is prose destined for a system prompt.
/// </remarks>
public static class HostOrientationResolver
{
    /// <summary>
    /// The conventional file name a host app can drop beside its executable to supply the
    /// orientation blurb without writing any code.
    /// </summary>
    public const string ConventionalFileName = "scriptchat.context.md";

    /// <summary>
    /// The conventional file name for one named script, for a host with more than one.
    /// </summary>
    /// <param name="scriptName">The script's name, as passed to <c>AddScript</c>.</param>
    /// <returns>
    /// <c>scriptchat.&lt;name&gt;.context.md</c>, lowercased with spaces removed — so a script
    /// called <c>"Processing"</c> gets <c>scriptchat.processing.context.md</c>.
    /// </returns>
    /// <remarks>
    /// A host with two scripts needs two blurbs, and the single
    /// <see cref="ConventionalFileName"/> could not express that — which is why the first real
    /// adopter hand-rolled its own file loading instead of using this class.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="scriptName"/> is empty or whitespace.</exception>
    public static string FileNameFor(string scriptName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptName);

        var slug = scriptName.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return $"scriptchat.{slug}.context.md";
    }

    /// <summary>
    /// Resolves the orientation blurb for one named script: its own file first, then the
    /// shared <see cref="ConventionalFileName"/>.
    /// </summary>
    /// <param name="scriptName">The script's name, as passed to <c>AddScript</c>.</param>
    /// <param name="searchDirectory">
    /// Directory to look in. Defaults to <see cref="AppContext.BaseDirectory"/>.
    /// </param>
    /// <param name="logger">Where to record which source won. Never receives the blurb's text (D17).</param>
    /// <returns>The blurb, or <see langword="null"/> when neither file supplies one.</returns>
    /// <remarks>
    /// The fallback matters: a host with several scripts that share one description writes one
    /// file, and only splits it when a script actually needs its own.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="scriptName"/> is empty or whitespace.</exception>
    /// <exception cref="IOException">A file exists but could not be read.</exception>
    public static string? ResolveForScript(string scriptName, string? searchDirectory, ILogger? logger)
    {
        var perScript = ReadFile(
            Path.Combine(searchDirectory ?? AppContext.BaseDirectory, FileNameFor(scriptName)),
            logger);

        return perScript ?? Resolve(hostContext: null, searchDirectory, logger);
    }

    /// <summary>
    /// Resolves the orientation blurb: file first, then <paramref name="hostContext"/>.
    /// </summary>
    /// <param name="hostContext">
    /// The fallback source, used when no file is present. May be <see langword="null"/>.
    /// </param>
    /// <param name="searchDirectory">
    /// Directory to look for <see cref="ConventionalFileName"/> in. Defaults to
    /// <see cref="AppContext.BaseDirectory"/> — where a deployed host app's own files sit.
    /// </param>
    /// <returns>
    /// The blurb, or <see langword="null"/> when neither source supplies one. A blank result
    /// from either source counts as "not supplied".
    /// </returns>
    /// <exception cref="IOException">The conventional file exists but could not be read.</exception>
    public static string? Resolve(IScriptChatHostContext? hostContext, string? searchDirectory = null)
    {
        return Resolve(hostContext, searchDirectory, logger: null);
    }

    /// <summary>
    /// Resolves the orientation blurb: file first, then <paramref name="hostContext"/>, logging
    /// which source won.
    /// </summary>
    /// <param name="hostContext">
    /// The fallback source, used when no file is present. May be <see langword="null"/>.
    /// </param>
    /// <param name="searchDirectory">
    /// Directory to look for <see cref="ConventionalFileName"/> in. Defaults to
    /// <see cref="AppContext.BaseDirectory"/> — where a deployed host app's own files sit.
    /// </param>
    /// <param name="logger">
    /// Where to record which source supplied the blurb, and the path probed when neither did.
    /// This is worth logging because "the file was not deployed beside the executable" is the
    /// commonest reason a host's orientation silently fails to reach the model. The blurb's own
    /// text is never logged, at any level (D17).
    /// </param>
    /// <returns>
    /// The blurb, or <see langword="null"/> when neither source supplies one. A blank result
    /// from either source counts as "not supplied".
    /// </returns>
    /// <exception cref="IOException">
    /// The conventional file exists but could not be read. A file the host meant to supply and
    /// that turns out to be unreadable is a real fault, so it propagates rather than silently
    /// degrading to the fallback.
    /// </exception>
    public static string? Resolve(
        IScriptChatHostContext? hostContext,
        string? searchDirectory,
        ILogger? logger)
    {
        var log = logger ?? NullLogger.Instance;
        var directory = searchDirectory ?? AppContext.BaseDirectory;
        var path = Path.Combine(directory, ConventionalFileName);

        // A missing file is the normal case for hosts that use the property instead, so it
        // falls through rather than throwing. An unreadable one does throw — see the doc above.
        if (ReadFile(path, log) is { } fromFile)
        {
            return fromFile;
        }

        var fromProperty = hostContext?.OrientationBlurb;
        if (string.IsNullOrWhiteSpace(fromProperty))
        {
            log.OrientationNotResolved(path);
            return null;
        }

        var blurb = fromProperty.Trim();
        log.OrientationResolvedFromHostContext(path, blurb.Length);
        return blurb;
    }

    /// <summary>
    /// Reads one orientation file, or <see langword="null"/> if it is absent or blank.
    /// </summary>
    /// <remarks>
    /// A blank file counts as "not supplied" so that touching a placeholder into existence does
    /// not silently replace a blurb the host meant to come from elsewhere.
    /// </remarks>
    private static string? ReadFile(string path, ILogger? logger)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        (logger ?? NullLogger.Instance).OrientationResolvedFromFile(path, trimmed.Length);
        return trimmed;
    }
}
