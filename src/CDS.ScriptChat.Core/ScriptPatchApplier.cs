namespace CDS.ScriptChat.Core;

/// <summary>
/// Applies a sequence of <see cref="ScriptEditHunk"/>s to a script using anchored search/replace —
/// the same technique Claude Code's <c>Edit</c> tool and GitHub Copilot's
/// <c>replace_string_in_file</c> tool use. There are no line numbers to drift out of step with;
/// a hunk either matches the script exactly once at the point it is applied, or it fails outright
/// (Job 3).
/// </summary>
public static class ScriptPatchApplier
{
    /// <summary>
    /// Applies every hunk in <paramref name="hunks"/> to <paramref name="script"/> in order, each
    /// one matched against the result of the previous.
    /// </summary>
    /// <param name="script">The script to patch.</param>
    /// <param name="hunks">The hunks to apply, in order. An empty list returns <paramref name="script"/> unchanged.</param>
    /// <returns><paramref name="script"/> with every hunk applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="script"/> or <paramref name="hunks"/> is <see langword="null"/>.</exception>
    /// <exception cref="ScriptPatchApplyException">
    /// A hunk's <see cref="ScriptEditHunk.OldText"/> does not match the script at that point
    /// exactly once — it is either no longer present or ambiguous, typically because the script
    /// changed since the proposal was made.
    /// </exception>
    public static string Apply(string script, IReadOnlyList<ScriptEditHunk> hunks)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(hunks);

        var result = script;

        for (var i = 0; i < hunks.Count; i++)
        {
            var hunk = hunks[i];
            var firstIndex = result.IndexOf(hunk.OldText, StringComparison.Ordinal);

            if (firstIndex < 0)
            {
                throw new ScriptPatchApplyException(
                    i,
                    hunks.Count,
                    $"Hunk {i + 1} of {hunks.Count}: the text to replace was not found in the current script. "
                        + "It may have changed since this change was proposed.");
            }

            if (result.IndexOf(hunk.OldText, firstIndex + 1, StringComparison.Ordinal) >= 0)
            {
                throw new ScriptPatchApplyException(
                    i,
                    hunks.Count,
                    $"Hunk {i + 1} of {hunks.Count}: the text to replace appears more than once in the current "
                        + "script, so it is not clear which occurrence to change.");
            }

            result = string.Concat(
                result.AsSpan(0, firstIndex),
                hunk.NewText,
                result.AsSpan(firstIndex + hunk.OldText.Length));
        }

        return result;
    }
}
