using AwesomeAssertions;

using CDS.ScriptChat.Core;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// Covers <see cref="ScriptChatHostPanel.UseStoredKey(IApiKeyStore, Func{ScriptChatProviderPreference?}, Action{ScriptChatProviderPreference})"/>
/// — the one call that replaces the ~70 lines of load-key / show-settings / persist-choice
/// every host previously wrote by hand.
/// </summary>
[TestClass]
[TestCategory("StoredKey")]
public sealed class StoredKeyTests
{
    private static ScriptChatTarget MakeTarget() => new()
    {
        DisplayName = "Script",
        ScriptTextProvider = () => "var x = 1;",
        ScriptTextSetter = _ => { },
        CreateSessionOptions = () => new ScriptChatSessionOptions(),
    };

    [TestMethod]
    public void UseStoredKey_WithAStoredKey_ConfiguresThePanel()
    {
        using var panel = new ScriptChatHostPanel();
        panel.SetTargets(MakeTarget());

        var store = new FakeApiKeyStore { [ScriptChatProvider.Claude] = "sk-ant-not-a-real-key" };

        panel.UseStoredKey(store, () => null, _ => { });

        FindStatus(panel).Text.Should().StartWith("Ready");
    }

    [TestMethod]
    public void UseStoredKey_WithNoStoredKey_SwitchesThePanelOffWithAPointerAtSettings()
    {
        using var panel = new ScriptChatHostPanel();
        panel.SetTargets(MakeTarget());

        panel.UseStoredKey(new FakeApiKeyStore(), () => null, _ => { });

        // Unconfigured must read as switched off, not broken (D3 — bring your own key).
        FindStatus(panel).Text.Should().Be("No API key yet. Choose Settings… to enter your own.");
    }

    [TestMethod]
    public void UseStoredKey_WithAnUnreadableKeyFile_SaysSoRatherThanPromptingSilently()
    {
        using var panel = new ScriptChatHostPanel();
        panel.SetTargets(MakeTarget());

        var store = new FakeApiKeyStore { LoadThrows = new UnauthorizedAccessException("denied") };

        panel.UseStoredKey(store, () => null, _ => { });

        FindStatus(panel).Text.Should().Be(
            "The stored API key could not be read. Choose Settings… to enter it again.");
    }

    [TestMethod]
    public void UseStoredKey_LoadsTheKeyForTheRememberedProvider()
    {
        using var panel = new ScriptChatHostPanel();
        panel.SetTargets(MakeTarget());

        var store = new FakeApiKeyStore { [ScriptChatProvider.OpenAI] = "sk-proj-not-a-real-key" };

        panel.UseStoredKey(
            store,
            () => new ScriptChatProviderPreference(ScriptChatProvider.OpenAI, "gpt-5"),
            _ => { });

        // The key must be fetched for the remembered provider, not the default one.
        store.Loaded.Should().ContainSingle().Which.Should().Be(ScriptChatProvider.OpenAI);
        FindStatus(panel).Text.Should().StartWith("Ready");
    }

    [TestMethod]
    public void UseStoredKey_WithNoRememberedProvider_StartsOnClaude()
    {
        using var panel = new ScriptChatHostPanel();
        panel.SetTargets(MakeTarget());

        var store = new FakeApiKeyStore();

        panel.UseStoredKey(store, () => null, _ => { });

        store.Loaded.Should().ContainSingle().Which.Should().Be(ScriptChatProvider.Claude);
    }

    [TestMethod]
    public void UseStoredKey_WithARememberedProviderButNoModel_FallsBackToTheProvidersDefault()
    {
        using var panel = new ScriptChatHostPanel();
        panel.SetTargets(MakeTarget());

        var store = new FakeApiKeyStore { [ScriptChatProvider.Claude] = "sk-ant-not-a-real-key" };

        panel.UseStoredKey(
            store,
            () => new ScriptChatProviderPreference(ScriptChatProvider.Claude, null),
            _ => { });

        // ScriptChatClientFactory rejects a blank ModelId, so reaching "Ready" is itself the
        // proof that the provider's default was substituted rather than an empty string passed on.
        FindStatus(panel).Text.Should().StartWith("Ready");
    }

    [TestMethod]
    public void UseStoredKey_ExposesTheStoreSoAHostCanReachIt()
    {
        using var panel = new ScriptChatHostPanel();
        var store = new FakeApiKeyStore();

        panel.UseStoredKey(store, () => null, _ => { });

        panel.ApiKeyStore.Should().BeSameAs(store);
    }

    [TestMethod]
    public void ApiKeyStore_BeforeUseStoredKey_IsNull()
    {
        using var panel = new ScriptChatHostPanel();

        panel.ApiKeyStore.Should().BeNull();
    }

    [TestMethod]
    public void UseStoredKey_NullArguments_Throw()
    {
        using var panel = new ScriptChatHostPanel();
        var store = new FakeApiKeyStore();

        ((Action)(() => panel.UseStoredKey((IApiKeyStore)null!, () => null, _ => { })))
            .Should().Throw<ArgumentNullException>();
        ((Action)(() => panel.UseStoredKey(store, null!, _ => { })))
            .Should().Throw<ArgumentNullException>();
        ((Action)(() => panel.UseStoredKey(store, () => null, null!)))
            .Should().Throw<ArgumentNullException>();
        ((Action)(() => panel.UseStoredKey("   ")))
            .Should().Throw<ArgumentException>();
    }

    private static Label FindStatus(Control root) =>
        root.Controls.Find("_statusLabel", searchAllChildren: true).OfType<Label>().Single();

    /// <summary>An in-memory key store, so tests never touch the user's real AppData.</summary>
    private sealed class FakeApiKeyStore : IApiKeyStore
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
}
