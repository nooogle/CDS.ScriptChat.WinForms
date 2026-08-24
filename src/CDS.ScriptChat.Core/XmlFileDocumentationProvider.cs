using System.Globalization;
using System.Xml;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Supplies documentation to Roslyn from an assembly's <c>.xml</c> file, so a symbol resolved out
/// of metadata carries the prose its author wrote.
/// </summary>
/// <remarks>
/// <para>
/// Roslyn ships <c>XmlDocumentationProvider</c>, but only in
/// <c>Microsoft.CodeAnalysis.Workspaces</c> — a large dependency for one small class, on a
/// package that a host takes purely to answer <c>lookup_symbol</c>. This is the same job in the
/// ~50 lines it actually needs.
/// </para>
/// <para>
/// The file is parsed once, on first use, and only if a symbol is actually asked about — a
/// compilation routinely references hundreds of assemblies whose documentation is never read.
/// </para>
/// </remarks>
internal sealed class XmlFileDocumentationProvider(string xmlFilePath) : DocumentationProvider
{
    private readonly Lock _loadLock = new();
    private Dictionary<string, string>? _members;

    /// <inheritdoc />
    protected override string? GetDocumentationForSymbol(
        string documentationMemberID,
        CultureInfo? preferredCulture = null,
        CancellationToken cancellationToken = default)
    {
        var members = EnsureLoaded();

        return members.TryGetValue(documentationMemberID, out var xml) ? xml : null;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is XmlFileDocumentationProvider other
        && string.Equals(xmlFilePath, other.GetPath(), StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(xmlFilePath);

    private string GetPath() => xmlFilePath;

    /// <summary>
    /// Reads and indexes the documentation file, once. A file that is missing or malformed yields
    /// an empty index rather than throwing: documentation is a nicety, and failing a lookup that
    /// has already produced a correct signature would be the worse outcome.
    /// </summary>
    private Dictionary<string, string> EnsureLoaded()
    {
        if (_members is { } loaded)
        {
            return loaded;
        }

        lock (_loadLock)
        {
            return _members ??= Load(xmlFilePath);
        }
    }

    private static Dictionary<string, string> Load(string path)
    {
        var members = new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            var document = XDocument.Load(path);

            foreach (var member in document.Root?.Element("members")?.Elements("member") ?? [])
            {
                if (member.Attribute("name")?.Value is { Length: > 0 } id)
                {
                    // The whole element, not its contents: callers parse what comes back and look
                    // for a <summary> child, which needs a single root element to hang off.
                    members[id] = member.ToString();
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or XmlException)
        {
            // Leaves the index empty — see the note on this method.
        }

        return members;
    }
}
