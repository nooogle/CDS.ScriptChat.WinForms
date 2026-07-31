namespace CDS.ScriptChat.Core;

/// <summary>
/// A code change proposed by the assistant via the <c>propose_script_edit</c> tool call.
/// Proposals are never applied automatically — the panel shows a diff and requires an
/// explicit accept before the editor buffer is touched (D5).
/// </summary>
/// <param name="ProposedCode">The complete replacement script (D13 — full-script replacement in v1).</param>
/// <param name="Summary">A short description of what the edit changes.</param>
public sealed record ScriptEditProposal(string ProposedCode, string Summary);
