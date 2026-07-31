using CDS.ScriptChat.Core;

namespace CDS.ScriptChat.TestHost;

/// <summary>
/// The worked example of an <see cref="ISymbolLookupProvider"/> that a consuming app must
/// supply (D15). This one answers from an in-memory catalogue of an invented API; a real host
/// would answer from Roslyn, reflection, or whatever it already knows.
/// </summary>
internal sealed class KestrelSymbolLookupProvider : ISymbolLookupProvider
{
    /// <summary>
    /// Raised on every lookup, so the host can show the model's tool use as it happens. This
    /// is the host's own instrumentation — nothing in the library requires it.
    /// </summary>
    public event EventHandler<SymbolLookupEventArgs>? SymbolRequested;

    /// <inheritdoc />
    public Task<SymbolLookupResult?> LookupAsync(
        string symbolName,
        string? containingType,
        CancellationToken cancellationToken = default)
    {
        KestrelApiCatalogue.Symbols.TryGetValue(symbolName, out var result);

        SymbolRequested?.Invoke(this, new SymbolLookupEventArgs(symbolName, containingType, result is not null));

        return Task.FromResult(result);
    }
}

/// <summary>Describes one <c>lookup_symbol</c> call.</summary>
internal sealed class SymbolLookupEventArgs : EventArgs
{
    public SymbolLookupEventArgs(string symbolName, string? containingType, bool found)
    {
        SymbolName = symbolName;
        ContainingType = containingType;
        Found = found;
    }

    /// <summary>Gets the symbol the model asked about.</summary>
    public string SymbolName { get; }

    /// <summary>Gets the containing type the model supplied, if any.</summary>
    public string? ContainingType { get; }

    /// <summary>Gets a value indicating whether the catalogue could answer.</summary>
    public bool Found { get; }

    /// <summary>Builds a one-line description for the host's activity list.</summary>
    public override string ToString()
    {
        var name = ContainingType is null ? SymbolName : $"{ContainingType}.{SymbolName}";
        return $"{DateTime.Now:HH:mm:ss}  {name}  {(Found ? "→ resolved" : "→ not found")}";
    }
}
