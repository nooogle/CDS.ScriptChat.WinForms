namespace CDS.ScriptChat.Core;

/// <summary>
/// The outcome of one assistant turn: prose, an optional proposed edit, and which symbols
/// the model looked up while answering.
/// </summary>
/// <param name="Text">
/// The assistant's prose response, or <see langword="null"/> when the turn produced no text.
/// </param>
/// <param name="Proposal">
/// The proposed full-script replacement, or <see langword="null"/> for a question-and-answer
/// turn that implied no code change (UC3) or a turn that proposed a patch instead.
/// </param>
/// <param name="SymbolsLookedUp">
/// The symbol names the model resolved via <c>lookup_symbol</c> during this turn, in call order.
/// Exposed so that tool use is observable without trawling a debug log (UC4).
/// </param>
/// <param name="PatchProposal">
/// The proposed find-and-replace patch (Job 3), or <see langword="null"/> when this turn
/// proposed no edit or proposed a full-script replacement instead. Mutually exclusive with
/// <paramref name="Proposal"/>.
/// </param>
public sealed record AssistantTurnResult(
    string? Text,
    ScriptEditProposal? Proposal,
    IReadOnlyList<string> SymbolsLookedUp,
    ScriptPatchProposal? PatchProposal = null)
{
    /// <summary>Gets a value indicating whether this turn proposed a code edit.</summary>
    public bool ProposedEdit => Proposal is not null || PatchProposal is not null;
}
