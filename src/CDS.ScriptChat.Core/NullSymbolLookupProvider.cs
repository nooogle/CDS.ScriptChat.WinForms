namespace CDS.ScriptChat.Core;

/// <summary>
/// A provider that resolves nothing. Lets the <c>lookup_symbol</c> tool-calling path be
/// exercised end to end before a host wires up a real implementation, and is the sensible
/// default for a host app that has no symbol engine.
/// </summary>
public sealed class NullSymbolLookupProvider : ISymbolLookupProvider
{
    /// <summary>Gets the shared instance.</summary>
    public static NullSymbolLookupProvider Instance { get; } = new();

    private NullSymbolLookupProvider()
    {
    }

    /// <inheritdoc />
    public Task<SymbolLookupResult?> LookupAsync(
        string symbolName,
        string? containingType,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<SymbolLookupResult?>(null);
    }
}
