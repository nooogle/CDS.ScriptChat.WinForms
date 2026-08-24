using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Wires a host's API types into a working symbol lookup and orientation blurb — the batteries
/// behind <see cref="ScriptChatSessionOptions.ForHostApi(Type, Type[])"/>.
/// </summary>
/// <remarks>
/// Separate from the options record so the two halves can be used on their own by a host that
/// wants one and not the other, and so neither is buried in a property initialiser.
/// </remarks>
internal static class HostApiLookup
{
    /// <summary>
    /// Builds a symbol lookup over the host's own assemblies.
    /// </summary>
    /// <remarks>
    /// The compilation is built on the first lookup rather than here. Walking the loaded
    /// assemblies is not work to do while a host is still building its main form, and a session
    /// may never be asked a symbol question at all.
    /// </remarks>
    public static ISymbolLookupProvider Create(Type api, Type[] additionalTypes)
    {
        var apiTypes = new[] { api }.Concat(additionalTypes).ToArray();
        var compilation = new Lazy<Compilation>(() => MetadataCompilation.FromTypes(apiTypes));

        // Bare type names in a script resolve through the namespaces it imports. A host that has
        // not said what those are almost certainly imports the namespaces its own API lives in,
        // which is the difference between a bare name resolving and answering "not found".
        var resolver = new RoslynSymbolResolver(NamespacesOf(apiTypes), api, additionalTypes);

        return new RoslynSymbolLookupProvider(
            _ => Task.FromResult<Compilation?>(compilation.Value),
            resolver);
    }

    /// <summary>
    /// Composes the orientation blurb: the host's own prose, then a generated index of what a
    /// script can reach.
    /// </summary>
    /// <remarks>
    /// Names only in the index, deliberately. Front-loading signatures would bloat every system
    /// prompt with detail the model mostly does not need, and <c>lookup_symbol</c> answers more
    /// accurately on demand than a snapshot can.
    /// </remarks>
    public static string BuildOrientation(
        Type api,
        Type[] additionalTypes,
        string? scriptName,
        ILoggerFactory? loggerFactory)
    {
        var logger = loggerFactory?.CreateLogger(typeof(HostOrientationResolver));

        // A named script looks for its own file first and falls back to the shared one, so a
        // host with several scripts that share a description still writes only one file.
        var prose = scriptName is null
            ? HostOrientationResolver.Resolve(hostContext: null, searchDirectory: null, logger)
            : HostOrientationResolver.ResolveForScript(scriptName, searchDirectory: null, logger);

        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(prose))
        {
            builder.AppendLine(prose.Trim());
            builder.AppendLine();
        }

        builder.AppendLine(
            """
            What a script can reach — member names only. Call `lookup_symbol` for the real
            signature of anything you are about to use; these lists say what exists, not how to
            call it.
            """);
        builder.AppendLine();
        builder.Append(HostApiIndex.Describe(api, additionalTypes));

        return builder.ToString();
    }

    private static string[] NamespacesOf(Type[] types) =>
        [.. types
            .Select(type => type.Namespace)
            .Where(space => !string.IsNullOrEmpty(space))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()];
}
