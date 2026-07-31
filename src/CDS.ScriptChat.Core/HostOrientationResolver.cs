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
    /// <exception cref="IOException">
    /// The conventional file exists but could not be read. A file the host meant to supply and
    /// that turns out to be unreadable is a real fault, so it propagates rather than silently
    /// degrading to the fallback.
    /// </exception>
    public static string? Resolve(IScriptChatHostContext? hostContext, string? searchDirectory = null)
    {
        var directory = searchDirectory ?? AppContext.BaseDirectory;
        var path = Path.Combine(directory, ConventionalFileName);

        // A missing file is the normal case for hosts that use the property instead, so it
        // falls through rather than throwing. An unreadable one does throw — see the doc above.
        if (File.Exists(path))
        {
            var fromFile = File.ReadAllText(path);
            if (!string.IsNullOrWhiteSpace(fromFile))
            {
                return fromFile.Trim();
            }
        }

        var fromProperty = hostContext?.OrientationBlurb;
        return string.IsNullOrWhiteSpace(fromProperty) ? null : fromProperty.Trim();
    }
}
