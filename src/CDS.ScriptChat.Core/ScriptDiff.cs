namespace CDS.ScriptChat.Core;

/// <summary>
/// Produces a line-by-line diff between the current script and a proposed replacement, so the
/// user can see what would change before accepting it.
/// </summary>
/// <remarks>
/// This is presentation only. Accept and reject stay all-or-nothing in v1 (D13) — the diff
/// exists to make a full-script replacement reviewable, not to let the user take part of it.
/// </remarks>
public static class ScriptDiff
{
    /// <summary>
    /// Above this many lines on either side, the quadratic comparison stops being worth it and
    /// the diff degrades to "everything replaced". Scripts in these apps are far smaller.
    /// </summary>
    private const int MaxLinesForLineDiff = 2_000;

    /// <summary>
    /// Computes the diff from <paramref name="original"/> to <paramref name="proposed"/>.
    /// </summary>
    /// <param name="original">The script as it stands now.</param>
    /// <param name="proposed">The replacement the assistant proposed.</param>
    /// <returns>
    /// The diff in reading order. Two identical scripts yield all-unchanged lines rather than
    /// an empty result.
    /// </returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public static IReadOnlyList<ScriptDiffLine> Compute(string original, string proposed)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(proposed);

        var originalLines = SplitLines(original);
        var proposedLines = SplitLines(proposed);

        if (originalLines.Length > MaxLinesForLineDiff || proposedLines.Length > MaxLinesForLineDiff)
        {
            return WholesaleReplacement(originalLines, proposedLines);
        }

        return LongestCommonSubsequenceDiff(originalLines, proposedLines);
    }

    /// <summary>
    /// Gets a value indicating whether a computed diff contains any change at all.
    /// </summary>
    /// <param name="diff">The diff to inspect.</param>
    /// <returns><see langword="true"/> when at least one line was added or removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="diff"/> is <see langword="null"/>.</exception>
    public static bool HasChanges(IReadOnlyList<ScriptDiffLine> diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        return diff.Any(line => line.Kind != ScriptDiffLineKind.Unchanged);
    }

    private static string[] SplitLines(string text)
    {
        // Normalise first so a script that only differs in line endings reads as unchanged.
        return text.ReplaceLineEndings("\n").Split('\n');
    }

    private static List<ScriptDiffLine> WholesaleReplacement(string[] originalLines, string[] proposedLines)
    {
        var result = new List<ScriptDiffLine>(originalLines.Length + proposedLines.Length);
        result.AddRange(originalLines.Select(l => new ScriptDiffLine(ScriptDiffLineKind.Removed, l)));
        result.AddRange(proposedLines.Select(l => new ScriptDiffLine(ScriptDiffLineKind.Added, l)));
        return result;
    }

    private static List<ScriptDiffLine> LongestCommonSubsequenceDiff(string[] original, string[] proposed)
    {
        // lengths[i, j] is the length of the longest common subsequence of original[i..] and
        // proposed[j..], filled from the end so the walk below can run forwards.
        var lengths = new int[original.Length + 1, proposed.Length + 1];

        for (var i = original.Length - 1; i >= 0; i--)
        {
            for (var j = proposed.Length - 1; j >= 0; j--)
            {
                lengths[i, j] = string.Equals(original[i], proposed[j], StringComparison.Ordinal)
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        var result = new List<ScriptDiffLine>();
        int x = 0, y = 0;

        while (x < original.Length && y < proposed.Length)
        {
            if (string.Equals(original[x], proposed[y], StringComparison.Ordinal))
            {
                result.Add(new ScriptDiffLine(ScriptDiffLineKind.Unchanged, original[x]));
                x++;
                y++;
            }
            else if (lengths[x + 1, y] >= lengths[x, y + 1])
            {
                result.Add(new ScriptDiffLine(ScriptDiffLineKind.Removed, original[x]));
                x++;
            }
            else
            {
                result.Add(new ScriptDiffLine(ScriptDiffLineKind.Added, proposed[y]));
                y++;
            }
        }

        while (x < original.Length)
        {
            result.Add(new ScriptDiffLine(ScriptDiffLineKind.Removed, original[x]));
            x++;
        }

        while (y < proposed.Length)
        {
            result.Add(new ScriptDiffLine(ScriptDiffLineKind.Added, proposed[y]));
            y++;
        }

        return result;
    }
}
