using System.Collections.Concurrent;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Builds a metadata-only <see cref="Compilation"/> over a host's own assemblies, so
/// <see cref="RoslynSymbolLookupProvider"/> can answer <c>lookup_symbol</c> in a host that has no
/// compilation of its own to hand over.
/// </summary>
/// <remarks>
/// <para>
/// Metadata only: no syntax trees, nothing is parsed or emitted. This exists purely so symbols
/// can be looked up by name, which needs references and nothing else. A host whose editor already
/// produces a real <see cref="Compilation"/> should pass that instead — it reflects what the
/// script can currently see, which this cannot.
/// </para>
/// <para>
/// The non-obvious part is documentation. Roslyn does <b>not</b> go looking for an assembly's
/// <c>.xml</c> file on its own — it must be attached explicitly when the reference is created.
/// Miss that and every lookup returns a correct signature with no documentation, which is the
/// failure mode most likely to go unnoticed, because the answer still looks right.
/// </para>
/// </remarks>
public static class MetadataCompilation
{
    /// <summary>
    /// Caches one <see cref="MetadataReference"/> per assembly path. Reading an assembly's
    /// metadata and its XML documentation is the expensive part of building a compilation, and a
    /// host with several scripts builds one per script over largely the same reference set.
    /// </summary>
    private static readonly ConcurrentDictionary<string, MetadataReference> s_references =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Builds a compilation covering the assemblies that declare the given types, everything
    /// those assemblies reference, and everything already loaded in this process.
    /// </summary>
    /// <param name="types">
    /// The types whose assemblies must be reachable — typically the host's globals type and its
    /// API class.
    /// </param>
    /// <returns>A compilation suitable for <see cref="RoslynSymbolResolver"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="types"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="types"/> is empty.</exception>
    public static Compilation FromTypes(params Type[] types)
    {
        ArgumentNullException.ThrowIfNull(types);

        if (types.Length == 0)
        {
            throw new ArgumentException("At least one type is required.", nameof(types));
        }

        return FromAssemblies([.. types.Select(type => type.Assembly).Distinct()]);
    }

    /// <summary>
    /// Builds a compilation covering the given assemblies, everything they reference, and
    /// everything already loaded in this process.
    /// </summary>
    /// <param name="assemblies">The assemblies whose types must be reachable.</param>
    /// <returns>A compilation suitable for <see cref="RoslynSymbolResolver"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="assemblies"/> is empty.</exception>
    public static Compilation FromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        if (assemblies.Length == 0)
        {
            throw new ArgumentException("At least one assembly is required.", nameof(assemblies));
        }

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Everything already loaded comes in first: a host app has, by definition, loaded what
        // its own scripts run against, and that includes framework assemblies a reference walk
        // would otherwise have to rediscover.
        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            AddPath(paths, loaded);
        }

        // Then the transitive closure of what was asked for, in case a referenced assembly has
        // not been loaded yet — nothing forces a host to have touched every type it exposes.
        foreach (var assembly in assemblies)
        {
            AddClosure(paths, assembly, new HashSet<string>(StringComparer.Ordinal));
        }

        return CSharpCompilation.Create(
            assemblyName: "ScriptChatSymbolLookup",
            syntaxTrees: null,
            references: paths.Select(ReferenceFor),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Creates the reference for one assembly path, attaching its XML documentation when the
    /// file is deployed beside it.
    /// </summary>
    /// <remarks>
    /// The documentation file is the whole reason this method exists rather than a bare
    /// <see cref="MetadataReference.CreateFromFile(string, MetadataReferenceProperties, DocumentationProvider)"/>
    /// call at the call site — see the note on the class.
    /// </remarks>
    private static MetadataReference ReferenceFor(string path) =>
        s_references.GetOrAdd(path, static assemblyPath =>
        {
            var xmlPath = Path.ChangeExtension(assemblyPath, ".xml");

            DocumentationProvider? documentation = File.Exists(xmlPath)
                ? new XmlFileDocumentationProvider(xmlPath)
                : null;

            return MetadataReference.CreateFromFile(
                assemblyPath,
                MetadataReferenceProperties.Assembly,
                documentation);
        });

    /// <summary>Adds an assembly and everything it references, depth-first, without repeating.</summary>
    private static void AddClosure(HashSet<string> paths, Assembly assembly, HashSet<string> visited)
    {
        if (!visited.Add(assembly.FullName ?? assembly.ToString()))
        {
            return;
        }

        AddPath(paths, assembly);

        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            Assembly referenced;
            try
            {
                referenced = Assembly.Load(reference);
            }
            catch (Exception exception) when (
                exception is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                // An ordinary outcome, not a fault: reference lists routinely name assemblies
                // that are not deployed on this platform or configuration. Skipping one costs at
                // most a "not found" on a symbol the script could not have used anyway.
                continue;
            }

            AddClosure(paths, referenced, visited);
        }
    }

    /// <summary>
    /// Records an assembly's file path, if it has one. Dynamic and single-file-bundled assemblies
    /// report an empty <see cref="Assembly.Location"/> and cannot be referenced from metadata.
    /// </summary>
    private static void AddPath(HashSet<string> paths, Assembly assembly)
    {
        if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
        {
            paths.Add(assembly.Location);
        }
    }
}
