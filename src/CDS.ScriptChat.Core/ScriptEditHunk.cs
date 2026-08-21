using System.ComponentModel;

namespace CDS.ScriptChat.Core;

/// <summary>
/// One targeted find-and-replace change within a script, as proposed by the
/// <c>propose_script_patch</c> tool (Job 3). This is the same anchored search/replace technique
/// Claude Code's <c>Edit</c> tool and GitHub Copilot's <c>replace_string_in_file</c> tool use —
/// no line numbers, so there is nothing for them to drift out of step with; a hunk either matches
/// the script exactly once or it does not apply (see <see cref="ScriptPatchApplier"/>).
/// </summary>
/// <param name="OldText">
/// The exact text to find. Also doubles as this tool's JSON schema, so the description guides
/// what the model is told to supply.
/// </param>
/// <param name="NewText">The text to replace it with.</param>
public sealed record ScriptEditHunk(
    [property: Description(
        "The exact text to find in the current script, including whitespace. Must match the "
        + "script exactly once — include enough surrounding context to make it unique.")]
    string OldText,
    [property: Description("The text to replace it with.")]
    string NewText);
