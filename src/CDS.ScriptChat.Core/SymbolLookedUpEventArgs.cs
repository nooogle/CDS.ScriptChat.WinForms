namespace CDS.ScriptChat.Core;

/// <summary>
/// Reports that the model looked a symbol up, and whether the host could answer.
/// </summary>
/// <remarks>
/// Deliberately carries no signature, namespace or documentation. Those are content, and content
/// never leaves the direct provider call (D17) — this exists so a host can show "the assistant
/// checked <c>FindContours</c>" in its own status strip, not so the result can be recorded.
/// </remarks>
/// <param name="symbolName">The symbol the model asked about.</param>
/// <param name="containingType">The declaring type it named, if any.</param>
/// <param name="found">Whether the lookup resolved.</param>
public sealed class SymbolLookedUpEventArgs(string symbolName, string? containingType, bool found) : EventArgs
{
    /// <summary>Gets the symbol the model asked about.</summary>
    public string SymbolName { get; } = symbolName;

    /// <summary>Gets the declaring type the model named, or <see langword="null"/> if it named none.</summary>
    public string? ContainingType { get; } = containingType;

    /// <summary>Gets a value indicating whether the lookup resolved.</summary>
    public bool Found { get; } = found;

    /// <summary>Renders the lookup as a one-line status message.</summary>
    /// <returns>Something like <c>lookup_symbol: Cv2.FindContours — found</c>.</returns>
    public override string ToString()
    {
        var name = ContainingType is null ? SymbolName : $"{ContainingType}.{SymbolName}";
        return $"lookup_symbol: {name} — {(Found ? "found" : "not found")}";
    }
}
