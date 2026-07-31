namespace CDS.ScriptChat.Core;

/// <summary>
/// The outcome of one assistant turn: prose, an optional proposed edit, and which symbols
/// the model looked up while answering.
/// </summary>
/// <param name="Text">
/// The assistant's prose response, or <see langword="null"/> when the turn produced no text.
/// </param>
/// <param name="Proposal">
/// The proposed code edit, or <see langword="null"/> for a question-and-answer turn that
/// implied no code change (UC3).
/// </param>
/// <param name="SymbolsLookedUp">
/// The symbol names the model resolved via <c>lookup_symbol</c> during this turn, in call order.
/// Exposed so that tool use is observable without trawling a debug log (UC4).
/// </param>
public sealed record AssistantTurnResult(
    string? Text,
    ScriptEditProposal? Proposal,
    IReadOnlyList<string> SymbolsLookedUp)
{
    /// <summary>Gets a value indicating whether this turn proposed a code edit.</summary>
    public bool ProposedEdit => Proposal is not null;
}
