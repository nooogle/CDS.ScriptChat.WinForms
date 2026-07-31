namespace CDS.ScriptChat.Core;

/// <summary>
/// Answers "what does this symbol look like" on behalf of the host app.
/// </summary>
/// <remarks>
/// <para>
/// The library defines this abstraction and never implements it for real (D15) — nothing
/// use-case-specific ships here. Where the answers come from is entirely the host's choice:
/// a Roslyn <c>SemanticModel</c>, a reflection pass over loaded assemblies, a hand-maintained
/// table, or a remote service. <see cref="NullSymbolLookupProvider"/> is the stand-in until a
/// host wires one up.
/// </para>
/// <para>
/// v1 scope is deliberately narrow (D11): resolving a single named symbol. Workspace-wide
/// symbol search, "find references", and related-file awareness are a later milestone.
/// </para>
/// <para>
/// The interface is shaped so those can arrive as additional methods without changing v1
/// callers — implementers should expect new members to gain default implementations rather
/// than this contract being replaced.
/// </para>
/// </remarks>
public interface ISymbolLookupProvider
{
    /// <summary>
    /// Resolves a single symbol by name.
    /// </summary>
    /// <param name="symbolName">
    /// The symbol to resolve, e.g. <c>FindContours</c> or <c>ImagePanel</c>.
    /// </param>
    /// <param name="containingType">
    /// The type that declares the symbol, when the model knows it — e.g. <c>Cv2</c> for
    /// <c>Cv2.FindContours</c>. <see langword="null"/> when the symbol should be resolved
    /// on its own.
    /// </param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>
    /// The resolved symbol, or <see langword="null"/> when nothing matches — a miss is an
    /// ordinary outcome, not an error.
    /// </returns>
    Task<SymbolLookupResult?> LookupAsync(
        string symbolName,
        string? containingType,
        CancellationToken cancellationToken = default);
}
