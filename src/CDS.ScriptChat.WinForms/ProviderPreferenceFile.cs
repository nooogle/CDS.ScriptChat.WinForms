using CDS.ScriptChat.Core;

namespace CDS.ScriptChat.WinForms;

/// <summary>
/// Remembers the chosen provider and model in a small text file beside the encrypted key store,
/// so a host that has no settings file of its own does not need one just to use this panel.
/// </summary>
/// <remarks>
/// <para>
/// A host that already persists its own settings passes its own load/save pair to
/// <see cref="ScriptChatHostPanel.UseStoredKey(string, Func{ScriptChatProviderPreference?}, Action{ScriptChatProviderPreference})"/>
/// instead, and this is not used.
/// </para>
/// <para>
/// Plain <c>key=value</c> rather than JSON: two values do not justify a serialiser, and this
/// stays free of the trimming and source-generator questions that reflection-based serialisation
/// brings into a library. It is also readable and hand-editable, which is what someone
/// diagnosing "why did it open on the wrong provider" actually wants.
/// </para>
/// <para>
/// Nothing here is secret, but nothing here is important either: every failure results in "no
/// preference recorded", because falling back to the default provider is always survivable and
/// refusing to open the chat panel is not.
/// </para>
/// </remarks>
internal sealed class ProviderPreferenceFile(string path)
{
    private const string ProviderKey = "provider";
    private const string ModelKey = "model";

    /// <summary>Builds the conventional path beside a host application's key store.</summary>
    /// <param name="applicationName">The host application's name.</param>
    /// <returns>The preference file's full path.</returns>
    public static ProviderPreferenceFile ForApplication(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        return new ProviderPreferenceFile(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            applicationName,
            "scriptchat",
            "provider.txt"));
    }

    /// <summary>Reads the recorded preference, or <see langword="null"/> when there is none.</summary>
    public ScriptChatProviderPreference? Load()
    {
        string[] lines;
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            lines = File.ReadAllLines(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        ScriptChatProvider? provider = null;
        string? modelId = null;

        foreach (var line in lines)
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (string.Equals(key, ProviderKey, StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<ScriptChatProvider>(value, ignoreCase: true, out var parsed))
            {
                // An unrecognised name falls through to null: a file written by a later build
                // that knows a provider this one does not must not stop the app configuring
                // itself.
                provider = parsed;
            }
            else if (string.Equals(key, ModelKey, StringComparison.OrdinalIgnoreCase) && value.Length > 0)
            {
                modelId = value;
            }
        }

        return provider is null ? null : new ScriptChatProviderPreference(provider.Value, modelId);
    }

    /// <summary>Records the preference, replacing whatever was there.</summary>
    public void Save(ScriptChatProviderPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(
                path,
                [$"{ProviderKey}={preference.Provider}", $"{ModelKey}={preference.ModelId}"]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The chat still works this session; the user just gets the default provider next
            // time. Not worth failing the configuration the user just applied.
        }
    }
}
