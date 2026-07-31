using CDS.ScriptChat.Core;

namespace CDS.ScriptChat.WinForms;

/// <summary>
/// Stores the user's own API keys between sessions. Bring-your-own-key only: a key belongs to
/// the user, is never bundled with the app, and is never sent anywhere except the provider's
/// own SDK call (D3).
/// </summary>
public interface IApiKeyStore
{
    /// <summary>
    /// Loads the stored key for a provider.
    /// </summary>
    /// <param name="provider">The provider whose key to load.</param>
    /// <returns>The key, or <see langword="null"/> when none is stored.</returns>
    string? Load(ScriptChatProvider provider);

    /// <summary>
    /// Stores a key for a provider, replacing any previous one.
    /// </summary>
    /// <param name="provider">The provider the key belongs to.</param>
    /// <param name="apiKey">The key to store.</param>
    void Save(ScriptChatProvider provider, string apiKey);

    /// <summary>
    /// Removes the stored key for a provider. Removing a key that is not there is not an error.
    /// </summary>
    /// <param name="provider">The provider whose key to remove.</param>
    void Clear(ScriptChatProvider provider);
}
