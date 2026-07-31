namespace CDS.ScriptChat.Core;

/// <summary>
/// Identifies who produced a <see cref="ChatTurn"/> in the rendered transcript.
/// </summary>
public enum ChatTurnRole
{
    /// <summary>A turn typed by the user.</summary>
    User,

    /// <summary>A turn produced by the AI assistant.</summary>
    Assistant,
}
