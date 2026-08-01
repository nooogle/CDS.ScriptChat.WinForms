using System.Security.Cryptography;
using System.Text;

using CDS.ScriptChat.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
/// Keys are never logged, at any level, in any form. The log records where a key file lives,
/// whether one was found, and how long the key was — never the key itself. Callers should keep
/// keys out of exception messages too; this class deliberately never includes key material in
/// the exceptions it throws.
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
    private readonly ILogger _logger;

    private DpapiApiKeyStore(string directory, ILogger? logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _logger = logger ?? NullLogger.Instance;
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
        return ForApplication(applicationName, logger: null);
    }

    /// <summary>
    /// Creates a store under the current user's roaming application data, logging what it does.
    /// </summary>
    /// <param name="applicationName">
    /// The host application's name, used to keep its keys separate from other apps that embed
    /// this panel.
    /// </param>
    /// <param name="logger">Where to record load, save, and clear outcomes. Never receives key material.</param>
    /// <returns>A store rooted beneath the user's application data.</returns>
    /// <exception cref="ArgumentException"><paramref name="applicationName"/> is empty or whitespace.</exception>
    public static DpapiApiKeyStore ForApplication(string applicationName, ILogger? logger)
    {
        return new DpapiApiKeyStore(BuildDefaultDirectory(applicationName), logger);
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
        return ForDirectory(directory, logger: null);
    }

    /// <summary>
    /// Creates a store rooted at an explicit directory, logging what it does.
    /// </summary>
    /// <param name="directory">The directory to keep encrypted key files in.</param>
    /// <param name="logger">Where to record load, save, and clear outcomes. Never receives key material.</param>
    /// <returns>A store rooted at that directory.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty or whitespace.</exception>
    public static DpapiApiKeyStore ForDirectory(string directory, ILogger? logger)
    {
        return new DpapiApiKeyStore(directory, logger);
    }

    /// <inheritdoc />
    public string? Load(ScriptChatProvider provider)
    {
        var path = GetKeyPath(provider);
        if (!File.Exists(path))
        {
            _logger.ApiKeyNotStored(provider, path);
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
            // Treat as "no key stored" so the panel prompts for one instead of failing hard —
            // but say so in the log, because a silently-ignored key file is baffling otherwise.
            _logger.ApiKeyUndecryptable(provider, path);
            return null;
        }

        try
        {
            var apiKey = Encoding.UTF8.GetString(plaintextBytes);
            _logger.ApiKeyLoaded(provider, apiKey.Length);
            return apiKey;
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

        var path = GetKeyPath(provider);
        var plaintextBytes = Encoding.UTF8.GetBytes(apiKey);
        try
        {
            var protectedBytes = ProtectedData.Protect(plaintextBytes, s_entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }

        _logger.ApiKeySaved(provider, path, apiKey.Length);
    }

    /// <inheritdoc />
    public void Clear(ScriptChatProvider provider)
    {
        var path = GetKeyPath(provider);
        var existed = File.Exists(path);
        if (existed)
        {
            File.Delete(path);
        }

        _logger.ApiKeyCleared(provider, existed);
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
