using System.Security.Cryptography;
using System.Text;

using CDS.ScriptChat.Core;

namespace CDS.ScriptChat.WinForms;

/// <summary>
/// Stores API keys on disk encrypted with the Windows Data Protection API, scoped to the
/// current user account.
/// </summary>
/// <remarks>
/// <para>
/// DPAPI <see cref="DataProtectionScope.CurrentUser"/> means the ciphertext is only decryptable
/// by the same Windows user on the same machine. Copying the file elsewhere yields nothing
/// useful, and no key material is written in plaintext at any point.
/// </para>
/// <para>
/// Keys are never logged. Callers should keep them out of exception messages too — this class
/// deliberately never includes key material in the exceptions it throws.
/// </para>
/// </remarks>
public sealed class DpapiApiKeyStore : IApiKeyStore
{
    /// <summary>
    /// Ties the ciphertext to this application, so a blob lifted from another DPAPI-using app
    /// cannot be decrypted here and vice versa.
    /// </summary>
    private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("CDS.ScriptChat.ApiKey.v1");

    private readonly string _directory;

    private DpapiApiKeyStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <summary>
    /// Creates a store under the current user's roaming application data. This is what host
    /// apps normally want.
    /// </summary>
    /// <param name="applicationName">
    /// The host application's name, used to keep its keys separate from other apps that embed
    /// this panel.
    /// </param>
    /// <returns>A store rooted beneath the user's application data.</returns>
    /// <exception cref="ArgumentException"><paramref name="applicationName"/> is empty or whitespace.</exception>
    public static DpapiApiKeyStore ForApplication(string applicationName)
    {
        return new DpapiApiKeyStore(BuildDefaultDirectory(applicationName));
    }

    /// <summary>
    /// Creates a store rooted at an explicit directory, for hosts that manage their own
    /// settings location — and for tests.
    /// </summary>
    /// <param name="directory">The directory to keep encrypted key files in.</param>
    /// <returns>A store rooted at that directory.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty or whitespace.</exception>
    public static DpapiApiKeyStore ForDirectory(string directory)
    {
        return new DpapiApiKeyStore(directory);
    }

    /// <inheritdoc />
    public string? Load(ScriptChatProvider provider)
    {
        var path = GetKeyPath(provider);
        if (!File.Exists(path))
        {
            return null;
        }

        var protectedBytes = File.ReadAllBytes(path);

        byte[] plaintextBytes;
        try
        {
            plaintextBytes = ProtectedData.Unprotect(protectedBytes, s_entropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            // Written by a different Windows user, restored from another machine, or corrupt.
            // Treat as "no key stored" so the panel prompts for one instead of failing hard.
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    /// <inheritdoc />
    public void Save(ScriptChatProvider provider, string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        Directory.CreateDirectory(_directory);

        var plaintextBytes = Encoding.UTF8.GetBytes(apiKey);
        try
        {
            var protectedBytes = ProtectedData.Protect(plaintextBytes, s_entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(GetKeyPath(provider), protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    /// <inheritdoc />
    public void Clear(ScriptChatProvider provider)
    {
        var path = GetKeyPath(provider);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string BuildDefaultDirectory(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            applicationName,
            "scriptchat");
    }

    private string GetKeyPath(ScriptChatProvider provider)
    {
        // The enum name is a fixed identifier, so it is safe as a file name.
        return Path.Combine(_directory, $"{provider}.key");
    }
}
