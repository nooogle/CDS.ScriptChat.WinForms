namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// A symbol provider that resolves a fixed set of symbols, keyed by the name the model asks for.
/// </summary>
internal sealed class StubSymbolLookupProvider : ISymbolLookupProvider
{
    private readonly Dictionary<string, SymbolLookupResult> _symbols;

    public StubSymbolLookupProvider(Dictionary<string, SymbolLookupResult> symbols)
    {
        _symbols = symbols;
    }

    /// <summary>Gets the arguments of each call, oldest first.</summary>
    public List<(string SymbolName, string? ContainingType)> Calls { get; } = [];

    public Task<SymbolLookupResult?> LookupAsync(
        string symbolName,
        string? containingType,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((symbolName, containingType));
        _symbols.TryGetValue(symbolName, out var result);
        return Task.FromResult(result);
    }
}
