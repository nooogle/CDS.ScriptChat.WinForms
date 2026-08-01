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
    /// text is logged at <see cref="LogLevel.Trace"/> only.
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
        if (File.Exists(path))
        {
            var fromFile = File.ReadAllText(path);
            if (!string.IsNullOrWhiteSpace(fromFile))
            {
                var trimmed = fromFile.Trim();
                log.OrientationResolvedFromFile(path, trimmed.Length);
                log.OrientationContent(trimmed);
                return trimmed;
            }
        }

        var fromProperty = hostContext?.OrientationBlurb;
        if (string.IsNullOrWhiteSpace(fromProperty))
        {
            log.OrientationNotResolved(path);
            return null;
        }

        var blurb = fromProperty.Trim();
        log.OrientationResolvedFromHostContext(path, blurb.Length);
        log.OrientationContent(blurb);
        return blurb;
    }
}
