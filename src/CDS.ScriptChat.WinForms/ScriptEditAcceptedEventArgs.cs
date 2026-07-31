namespace CDS.ScriptChat.WinForms;

/// <summary>
/// Reports an edit the user accepted and the panel has already handed to the host's script
/// setter.
/// </summary>
public sealed class ScriptEditAcceptedEventArgs : EventArgs
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ScriptEditAcceptedEventArgs"/> class.
    /// </summary>
    /// <param name="proposedCode">The script that was applied.</param>
    /// <param name="summary">The assistant's summary of the change, when it supplied one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="proposedCode"/> is <see langword="null"/>.</exception>
    public ScriptEditAcceptedEventArgs(string proposedCode, string? summary)
    {
        ArgumentNullException.ThrowIfNull(proposedCode);

        ProposedCode = proposedCode;
        Summary = summary;
    }

    /// <summary>Gets the script that was applied to the editor.</summary>
    public string ProposedCode { get; }

    /// <summary>Gets the assistant's summary of the change, if any.</summary>
    public string? Summary { get; }
}
