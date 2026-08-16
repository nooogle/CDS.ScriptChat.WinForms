namespace CDS.ScriptChat.Core;

/// <summary>
/// One of a host's scripts, described in the terms a multi-script chat host needs: a display
/// name, a way to read the script, a way to replace it, and a factory for the session options
/// that go with it.
/// </summary>
/// <remarks>
/// The host panel knows nothing about editors — this is the whole contract between it and the
/// application, which is what lets one panel serve any number of scripts.
/// </remarks>
public sealed class ScriptChatTarget
{
    /// <summary>The label shown on the target selector, e.g. <c>"Workspace"</c>.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Reads the script currently in the target's editor.</summary>
    public required Func<string> ScriptTextProvider { get; init; }

    /// <summary>Replaces the script in the target's editor, once the user has accepted an edit.</summary>
    public required Action<string> ScriptTextSetter { get; init; }

    /// <summary>
    /// Builds the session options — symbol lookup and orientation blurb — for a new conversation
    /// about this script.
    /// </summary>
    /// <remarks>
    /// A factory rather than a fixed value because the orientation carries a snapshot of the
    /// counterpart script, which must be taken when the conversation starts rather than when the
    /// host did.
    /// </remarks>
    public required Func<ScriptChatSessionOptions> CreateSessionOptions { get; init; }
}
