using Microsoft.CodeAnalysis;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Answers the <c>lookup_symbol</c> tool from a Roslyn <see cref="Compilation"/>, so the model
/// gets real signatures and documentation out of the host's own assemblies rather than recalling
/// them (D22).
/// </summary>
/// <remarks>
/// <para>
/// Two shapes, because hosts come in two kinds. A host with a live script editor passes a
/// delegate, so every lookup resolves against the buffer as it stands now. A host whose reachable
/// API is fixed passes one <see cref="Compilation"/> — typically from
/// <see cref="MetadataCompilation.FromTypes(Type[])"/> — and it is reused for every lookup.
/// </para>
/// <para>
/// A <see cref="Compilation"/> is immutable once obtained, so resolving against it from the
/// background thread a tool call arrives on is safe. The delegate itself, however, may be called
/// from that thread: a host reading a UI-owned editor must marshal inside its own delegate.
/// </para>
/// </remarks>
public sealed class RoslynSymbolLookupProvider : ISymbolLookupProvider
{
    private readonly Func<CancellationToken, Task<Compilation?>> _compilationSource;
    private readonly RoslynSymbolResolver _resolver;

    /// <summary>
    /// Initialises a provider over a live compilation, re-read for every lookup.
    /// </summary>
    /// <param name="compilationSource">
    /// Returns the script's current compilation, or <see langword="null"/> when the host has none
    /// to give — an editor whose script does not currently parse, say. A null compilation is
    /// reported to the model as "not found", not as an error.
    /// </param>
    /// <param name="resolver">The engine that resolves against whatever the source returns.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public RoslynSymbolLookupProvider(
        Func<CancellationToken, Task<Compilation?>> compilationSource,
        RoslynSymbolResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(compilationSource);
        ArgumentNullException.ThrowIfNull(resolver);

        _compilationSource = compilationSource;
        _resolver = resolver;
    }

    /// <summary>
    /// Initialises a provider over one fixed compilation.
    /// </summary>
    /// <param name="compilation">
    /// The compilation every lookup resolves against, typically from
    /// <see cref="MetadataCompilation.FromTypes(Type[])"/>.
    /// </param>
    /// <param name="resolver">The engine that resolves against it.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public RoslynSymbolLookupProvider(Compilation compilation, RoslynSymbolResolver resolver)
        : this(_ => Task.FromResult<Compilation?>(compilation), resolver)
    {
        ArgumentNullException.ThrowIfNull(compilation);
    }

    /// <summary>
    /// Raised after every lookup, so a host can report what the model consulted.
    /// </summary>
    /// <remarks>
    /// Carries the symbol name and whether it resolved — never the signature or documentation,
    /// which are content (D17). Raised on the background thread the tool call arrives on, so a
    /// handler touching UI must marshal for itself.
    /// </remarks>
    public event EventHandler<SymbolLookedUpEventArgs>? SymbolLookedUp;

    /// <inheritdoc />
    public async Task<SymbolLookupResult?> LookupAsync(
        string symbolName,
        string? containingType,
        CancellationToken cancellationToken = default)
    {
        var compilation = await _compilationSource(cancellationToken).ConfigureAwait(false);

        var result = compilation is null
            ? null
            : _resolver.Resolve(compilation, symbolName, containingType, cancellationToken);

        SymbolLookedUp?.Invoke(
            this,
            new SymbolLookedUpEventArgs(symbolName, containingType, result is not null));

        return result;
    }
}
