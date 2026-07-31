namespace CDS.ScriptChat.Core;

/// <summary>
/// Tracks what has happened to a code edit proposed by an assistant turn.
/// </summary>
public enum EditDisposition
{
    /// <summary>The turn proposed no edit.</summary>
    None,

    /// <summary>An edit was proposed and is awaiting the user's accept or reject.</summary>
    PendingReview,

    /// <summary>The user accepted the edit and it was applied to the editor buffer.</summary>
    Accepted,

    /// <summary>The user rejected the edit; the buffer was left unchanged.</summary>
    Rejected,
}
