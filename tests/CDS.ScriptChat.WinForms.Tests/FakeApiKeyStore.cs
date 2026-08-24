using CDS.ScriptChat.Core;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>An in-memory key store, so tests never touch the user's real AppData.</summary>
internal sealed class FakeApiKeyStore : IApiKeyStore
{
    private readonly Dictionary<ScriptChatProvider, string> _keys = [];

    public List<ScriptChatProvider> Loaded { get; } = [];

    public Exception? LoadThrows { get; init; }

    public string this[ScriptChatProvider provider]
    {
        set => _keys[provider] = value;
    }

    public string? Load(ScriptChatProvider provider)
    {
        Loaded.Add(provider);

        if (LoadThrows is not null)
        {
            throw LoadThrows;
        }

        return _keys.TryGetValue(provider, out var key) ? key : null;
    }

    public void Save(ScriptChatProvider provider, string apiKey) => _keys[provider] = apiKey;

    public void Clear(ScriptChatProvider provider) => _keys.Remove(provider);
}
