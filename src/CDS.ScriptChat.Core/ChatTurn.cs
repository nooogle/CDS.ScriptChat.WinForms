namespace CDS.ScriptChat.Core;

/// <summary>
/// A single turn as the panel renders it, top to bottom. This is a display projection
/// over the raw provider message history, not a replacement for it — the session keeps
/// the provider history separately.
/// </summary>
/// <param name="Role">Who produced the turn.</param>
/// <param name="Text">The prose for the turn, if any.</param>
/// <param name="ProposedCode">
/// The complete replacement script proposed by this turn, or <see langword="null"/> when the
/// turn proposed no edit. Full-script replacement is the v1 diff granularity (D13).
/// </param>
/// <param name="EditSummary">A short description of the proposed edit, when one was proposed.</param>
/// <param name="Disposition">Whether a proposed edit is pending, accepted, or rejected.</param>
public sealed record ChatTurn(
    ChatTurnRole Role,
    string? Text,
    string? ProposedCode,
    string? EditSummary,
    EditDisposition Disposition)
{
    /// <summary>Gets a value indicating whether this turn carries a proposed code edit.</summary>
    public bool HasProposedEdit => ProposedCode is not null;
}
