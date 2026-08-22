namespace CDS.ScriptChat.Core;

/// <summary>
/// A set of targeted find-and-replace changes proposed by the assistant via the
/// <c>propose_script_patch</c> tool call (Job 3). Proposals are never applied automatically —
/// the panel shows every hunk as a diff and requires an explicit accept before the editor buffer
/// is touched (D5), the same rule <see cref="ScriptEditProposal"/> follows for a full rewrite.
/// </summary>
/// <param name="Hunks">The find-and-replace hunks, in the order they must be applied.</param>
/// <param name="Summary">A short description of what the change does.</param>
public sealed record ScriptPatchProposal(IReadOnlyList<ScriptEditHunk> Hunks, string Summary);
