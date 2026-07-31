namespace CDS.ScriptChat.Core;

/// <summary>
/// How one line of a rendered diff relates to the original script.
/// </summary>
public enum ScriptDiffLineKind
{
    /// <summary>The line is present in both the original and the proposal.</summary>
    Unchanged,

    /// <summary>The line appears only in the proposal.</summary>
    Added,

    /// <summary>The line appears only in the original.</summary>
    Removed,
}

/// <summary>
/// One line of a rendered diff.
/// </summary>
/// <param name="Kind">Whether the line was added, removed, or left alone.</param>
/// <param name="Text">The line itself, without any diff marker.</param>
public sealed record ScriptDiffLine(ScriptDiffLineKind Kind, string Text);
