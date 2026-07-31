using CDS.ScriptChat.Core;

namespace CDS.ScriptChat.WinForms;

/// <summary>
/// Carries a provider configuration the user has applied.
/// </summary>
public sealed class ScriptChatConfigurationEventArgs : EventArgs
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ScriptChatConfigurationEventArgs"/> class.
    /// </summary>
    /// <param name="clientOptions">The configuration the user applied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="clientOptions"/> is <see langword="null"/>.</exception>
    public ScriptChatConfigurationEventArgs(ScriptChatClientOptions clientOptions)
    {
        ArgumentNullException.ThrowIfNull(clientOptions);
        ClientOptions = clientOptions;
    }

    /// <summary>
    /// Gets the applied configuration. Carries the user's API key, so treat it as a secret:
    /// pass it to <see cref="ScriptChatClientFactory.Create"/> and nowhere else (D3).
    /// </summary>
    public ScriptChatClientOptions ClientOptions { get; }
}
