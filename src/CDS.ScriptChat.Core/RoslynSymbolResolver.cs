using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Answers "what does this symbol look like" against a Roslyn <see cref="Compilation"/> — the
/// signatures and documentation the script will actually be compiled against, rather than a
/// curated catalogue that can fall behind the code.
/// </summary>
/// <remarks>
/// <para>
/// Resolving against the script's own compilation is what makes a miss meaningful: not "unknown",
/// but "not reachable from this script's imports and referenced assemblies". A host with two
/// scripts compiled against different globals gives each its own resolver, and neither has to be
/// told what the other can see.
/// </para>
/// <para>
/// This is the engine; <see cref="RoslynSymbolLookupProvider"/> is the
/// <see cref="ISymbolLookupProvider"/> that hands its results to the <c>lookup_symbol</c> tool.
/// It is separate because a documentation pane or a custom tooltip can use the engine equally
/// well, with no chat session in sight.
/// </para>
/// <para>
/// The types searched for a bare member name should come from
/// <see cref="HostApiIndex.ScriptFacingTypes(Type, Type[])"/> — the same set
/// <see cref="HostApiIndex.Describe(Type, Type[])"/> lists — so what a model is told exists is
/// exactly what it can then ask about.
/// </para>
/// </remarks>
public sealed class RoslynSymbolResolver
{
    /// <summary>
    /// Renders a symbol the way it would be written in a script: return type, containing type,
    /// parameter names and types, and any default values.
    /// </summary>
    private static readonly SymbolDisplayFormat s_signatureFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeContainingType
            | SymbolDisplayMemberOptions.IncludeRef,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeDefaultValue
            | SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private readonly IReadOnlyList<string> _scriptNamespaces;
    private readonly IReadOnlyList<Type> _rootTypes;
    private readonly IReadOnlyDictionary<string, Type> _rootProperties;

    /// <summary>
    /// Initialises a resolver for a script compiled against one root type, searching the types
    /// <see cref="HostApiIndex"/> would list for it.
    /// </summary>
    /// <param name="scriptNamespaces">
    /// The namespaces the script imports. These are the candidate prefixes that turn a bare type
    /// name like <c>Mat</c> into something the compilation can be asked for.
    /// </param>
    /// <param name="rootType">The globals type the script is compiled against, or a flat API class.</param>
    /// <param name="additionalTypes">Types the script works with that the root does not expose.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public RoslynSymbolResolver(
        IEnumerable<string> scriptNamespaces,
        Type rootType,
        params Type[] additionalTypes)
        : this(scriptNamespaces, rootType, HostApiIndex.ScriptFacingTypes(rootType, additionalTypes))
    {
    }

    /// <summary>
    /// Initialises a resolver over an explicit set of root types, for a host that decides for
    /// itself which types a bare member name should be looked for on.
    /// </summary>
    /// <param name="scriptNamespaces">The namespaces the script imports.</param>
    /// <param name="rootType">
    /// The globals type. Its property names are what let a model asking about <c>API</c> — a
    /// property, not a type — reach the class behind it.
    /// </param>
    /// <param name="searchTypes">
    /// The types a bare member name is looked for on, in order. Pass the result of
    /// <see cref="HostApiIndex.ScriptFacingTypes(Type, Type[])"/> unless there is a reason not to.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public RoslynSymbolResolver(
        IEnumerable<string> scriptNamespaces,
        Type rootType,
        IReadOnlyList<Type> searchTypes)
    {
        ArgumentNullException.ThrowIfNull(scriptNamespaces);
        ArgumentNullException.ThrowIfNull(rootType);
        ArgumentNullException.ThrowIfNull(searchTypes);

        // The trailing empty entry covers a name that is already qualified, or lives in the
        // global namespace.
        _scriptNamespaces = [.. scriptNamespaces, string.Empty];
        _rootTypes = searchTypes;

        // A script reaches its host through the root type's properties, so a model asking about
        // "API" has named a property, not a type. Mapping each property name to the type behind
        // it answers that without this library having to know a host calls it "API" at all.
        _rootProperties = rootType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().PropertyType, StringComparer.Ordinal);
    }

    /// <summary>
    /// Resolves what a caller asked about and describes it.
    /// </summary>
    /// <param name="compilation">
    /// The script's compilation — from a live editor, or from
    /// <see cref="MetadataCompilation.FromTypes(Type[])"/> for a host that has no editor
    /// compilation of its own.
    /// </param>
    /// <param name="symbolName">The symbol to resolve, e.g. <c>FindContours</c> or <c>ImagePanel</c>.</param>
    /// <param name="containingType">
    /// The type that declares it, when the caller knows one — e.g. <c>Cv2</c> for
    /// <c>Cv2.FindContours</c>. <see langword="null"/> to resolve the name on its own.
    /// </param>
    /// <param name="cancellationToken">Cancels documentation retrieval.</param>
    /// <returns>
    /// The description, or <see langword="null"/> when nothing matches — a miss is an ordinary
    /// outcome, not an error.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="compilation"/> is <see langword="null"/>.</exception>
    public SymbolLookupResult? Resolve(
        Compilation compilation,
        string symbolName,
        string? containingType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        if (string.IsNullOrWhiteSpace(symbolName))
        {
            return null;
        }

        var symbols = Find(compilation, symbolName.Trim(), containingType?.Trim());

        return symbols.Count == 0 ? null : Describe(symbols, cancellationToken);
    }

    /// <summary>
    /// Looks in the order that gets the commonest questions right: a member of the type the
    /// caller named, then the name as a type in its own right, then a member of one of the types
    /// a script normally starts from.
    /// </summary>
    private IReadOnlyList<ISymbol> Find(Compilation compilation, string symbolName, string? containingType)
    {
        if (!string.IsNullOrEmpty(containingType) && FindType(compilation, containingType) is { } declaringType)
        {
            var members = FindMembers(declaringType, symbolName);

            if (members.Count > 0)
            {
                return members;
            }

            // The caller named a type that exists but has no such member. Fall through rather
            // than answering "not found" — it may simply have guessed the wrong declaring type.
        }

        if (FindType(compilation, symbolName) is { } type)
        {
            return [type];
        }

        foreach (var root in RootTypes(compilation))
        {
            var members = FindMembers(root, symbolName);

            if (members.Count > 0)
            {
                return members;
            }
        }

        return [];
    }

    /// <summary>
    /// Turns a type name into a symbol. A dotted name is taken as fully qualified; a bare one is
    /// tried against each namespace the script imports, and finally against the root type's own
    /// properties, so a name like <c>API</c> resolves to the class behind it.
    /// </summary>
    private INamedTypeSymbol? FindType(Compilation compilation, string name)
    {
        if (FindDeclaredType(compilation, name) is { } declared)
        {
            return declared;
        }

        // Checked last, not first: a host is free to have a property whose name is also a real
        // type name, and the type is the better answer when both exist.
        return _rootProperties.TryGetValue(name, out var propertyType) && propertyType.FullName is { } fullName
            ? compilation.GetTypeByMetadataName(fullName)
            : null;
    }

    private INamedTypeSymbol? FindDeclaredType(Compilation compilation, string name)
    {
        if (name.Contains('.', StringComparison.Ordinal))
        {
            return compilation.GetTypeByMetadataName(name);
        }

        foreach (var candidate in _scriptNamespaces)
        {
            var qualified = candidate.Length == 0 ? name : $"{candidate}.{name}";

            if (compilation.GetTypeByMetadataName(qualified) is { } type)
            {
                return type;
            }

            // Generic types carry their arity in metadata (List`1), which a caller naming "List"
            // would not know to include.
            for (var arity = 1; arity <= 2; arity++)
            {
                if (compilation.GetTypeByMetadataName($"{qualified}`{arity}") is { } generic)
                {
                    return generic;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds public members by name, walking up the inheritance chain so an inherited member is
    /// not reported as missing.
    /// </summary>
    private static IReadOnlyList<ISymbol> FindMembers(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            var members = current
                .GetMembers(name)
                // Fully qualified: WinForms brings a COM interop namespace called Accessibility
                // into scope, which otherwise wins here.
                .Where(member => member.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public)
                .ToArray();

            if (members.Length > 0)
            {
                return members;
            }
        }

        return [];
    }

    /// <summary>
    /// The types a bare member name is worth looking for on, resolved against this compilation. A
    /// type the script cannot see is skipped rather than reported.
    /// </summary>
    private IEnumerable<INamedTypeSymbol> RootTypes(Compilation compilation)
    {
        foreach (var type in _rootTypes)
        {
            if (type.FullName is { } fullName && compilation.GetTypeByMetadataName(fullName) is { } symbol)
            {
                yield return symbol;
            }
        }
    }

    private static SymbolLookupResult Describe(IReadOnlyList<ISymbol> symbols, CancellationToken cancellationToken)
    {
        var first = symbols[0];

        return new SymbolLookupResult
        {
            Signature = first.ToDisplayString(s_signatureFormat),
            Namespace = first.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            XmlDocSummary = ReadSummary(first, cancellationToken),
            Overloads = [.. symbols.Skip(1).Select(symbol => symbol.ToDisplayString(s_signatureFormat))],
        };
    }

    /// <summary>
    /// Pulls the <c>&lt;summary&gt;</c> out of a symbol's documentation comment, flattened onto
    /// one line.
    /// </summary>
    private static string? ReadSummary(ISymbol symbol, CancellationToken cancellationToken)
    {
        var xml = symbol.GetDocumentationCommentXml(cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        XElement? summary;
        try
        {
            summary = XElement.Parse(xml).Element("summary");
        }
        catch (XmlException)
        {
            // Documentation is a nicety — a malformed comment is not worth failing a lookup that
            // has already produced a correct signature.
            return null;
        }

        if (summary is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        AppendText(summary, builder);

        // Documentation comments are wrapped and indented in source; a caller wants a sentence.
        var flattened = string.Join(' ', builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return flattened.Length == 0 ? null : flattened;
    }

    /// <summary>
    /// Renders a documentation element's text, resolving the cross-reference tags into the names
    /// they point at.
    /// </summary>
    /// <remarks>
    /// Taking <see cref="XElement.Value"/> would drop them entirely, because they carry their
    /// content in attributes rather than as text — turning "wraps an
    /// <c>&lt;see cref="ImageListPanel"/&gt;</c>" into "wraps an", which reads as though a word is
    /// missing and says nothing.
    /// </remarks>
    private static void AppendText(XElement element, StringBuilder builder)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;

                case XElement { Name.LocalName: "see" or "seealso" } reference:
                    builder.Append(' ').Append(ReferencedName(reference)).Append(' ');
                    break;

                case XElement { Name.LocalName: "paramref" or "typeparamref" } reference:
                    builder.Append(' ').Append(reference.Attribute("name")?.Value).Append(' ');
                    break;

                case XElement child:
                    AppendText(child, builder);
                    break;
            }
        }
    }

    /// <summary>
    /// The readable name a <c>see</c> tag points at: its language keyword, or the last identifier
    /// of its documentation-comment ID.
    /// </summary>
    private static string ReferencedName(XElement reference)
    {
        if (reference.Attribute("langword")?.Value is { Length: > 0 } keyword)
        {
            return keyword;
        }

        var cref = reference.Attribute("cref")?.Value;

        if (string.IsNullOrEmpty(cref))
        {
            return string.Empty;
        }

        // "M:Namespace.Type.Method(System.String)" — drop the kind prefix, the argument list, and
        // the namespace, leaving "Method".
        var name = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
        var arguments = name.IndexOf('(', StringComparison.Ordinal);

        if (arguments >= 0)
        {
            name = name[..arguments];
        }

        var lastDot = name.LastIndexOf('.');
        return lastDot >= 0 ? name[(lastDot + 1)..] : name;
    }
}
