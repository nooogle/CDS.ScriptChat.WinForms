namespace CDS.ScriptChat.Core;

/// <summary>
/// Thrown by <see cref="ScriptPatchApplier.Apply"/> when a hunk's old text does not match the
/// script it is being applied to exactly once — either it is no longer present, or it is
/// ambiguous. Mirrors how Claude Code's <c>Edit</c> tool and GitHub Copilot's
/// <c>replace_string_in_file</c> tool both fail closed rather than attempting a fuzzy re-anchor.
/// </summary>
/// <param name="hunkIndex">The zero-based index of the hunk that failed, within the sequence passed to <see cref="ScriptPatchApplier.Apply"/>.</param>
/// <param name="hunkCount">The total number of hunks in that sequence.</param>
/// <param name="message">A message describing why the hunk did not apply.</param>
public sealed class ScriptPatchApplyException(int hunkIndex, int hunkCount, string message)
    : InvalidOperationException(message)
{
    /// <summary>Gets the zero-based index of the hunk that failed to apply.</summary>
    public int HunkIndex { get; } = hunkIndex;

    /// <summary>Gets the total number of hunks in the sequence the failed hunk belonged to.</summary>
    public int HunkCount { get; } = hunkCount;
}
