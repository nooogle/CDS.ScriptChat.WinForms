using AwesomeAssertions;

using CDS.ScriptChat.Core;
using CDS.ScriptChat.WinForms;

namespace CDS.ScriptChat.WinForms.Tests;

[TestClass]
[TestCategory("Settings")]
public sealed class ScriptChatSettingsPanelTests
{
    [TestMethod]
    public void Constructed_Always_DefaultsToClaudeAndItsDefaultModel()
    {
        using var panel = new ScriptChatSettingsPanel();

        panel.SelectedProvider.Should().Be(ScriptChatProvider.Claude);
        panel.SelectedModelId.Should().Be(ScriptChatModels.ClaudeDefault);
    }

    [TestMethod]
    public void Constructed_Always_HasNoApiKey()
    {
        using var panel = new ScriptChatSettingsPanel();

        panel.HasApiKey.Should().BeFalse();
    }

    [TestMethod]
    public void BuildClientOptions_NoApiKeyEntered_Throws()
    {
        using var panel = new ScriptChatSettingsPanel();

        var act = () => panel.BuildClientOptions();

        act.Should().Throw<InvalidOperationException>().WithMessage("*API key*");
    }

    [TestMethod]
    public void KeyStore_SetWithAStoredKey_LoadsItForTheSelectedProvider()
    {
        var store = new InMemoryApiKeyStore();
        store.Save(ScriptChatProvider.Claude, "sk-ant-stored");
        using var panel = new ScriptChatSettingsPanel();

        panel.KeyStore = store;

        panel.HasApiKey.Should().BeTrue();
        panel.BuildClientOptions().ApiKey.Should().Be("sk-ant-stored");
    }

    [TestMethod]
    public void KeyStore_SetWithNothingStored_LeavesTheKeyBlank()
    {
        using var panel = new ScriptChatSettingsPanel();

        panel.KeyStore = new InMemoryApiKeyStore();

        panel.HasApiKey.Should().BeFalse();
    }

    [TestMethod]
    public void Apply_WithAStoredKeyLoaded_RaisesConfigurationAppliedWithTheSelection()
    {
        var store = new InMemoryApiKeyStore();
        store.Save(ScriptChatProvider.Claude, "sk-ant-stored");
        using var panel = new ScriptChatSettingsPanel { KeyStore = store };
        ScriptChatConfigurationEventArgs? applied = null;
        panel.ConfigurationApplied += (_, e) => applied = e;

        ClickButton(panel, "_applyButton");

        applied.Should().NotBeNull();
        applied!.ClientOptions.Provider.Should().Be(ScriptChatProvider.Claude);
        applied.ClientOptions.ModelId.Should().Be(ScriptChatModels.ClaudeDefault);
    }

    [TestMethod]
    public void Apply_WithNoApiKey_RaisesNothing()
    {
        using var panel = new ScriptChatSettingsPanel();
        var raised = false;
        panel.ConfigurationApplied += (_, _) => raised = true;

        ClickButton(panel, "_applyButton");

        raised.Should().BeFalse();
    }

    [TestMethod]
    public void Apply_WithAKeyEntered_PersistsItToTheStore()
    {
        var store = new InMemoryApiKeyStore();
        using var panel = new ScriptChatSettingsPanel { KeyStore = store };
        SetApiKey(panel, "sk-ant-typed-in");

        ClickButton(panel, "_applyButton");

        store.Load(ScriptChatProvider.Claude).Should().Be("sk-ant-typed-in");
    }

    [TestMethod]
    public void ForgetKey_AfterStoringOne_ClearsBothTheBoxAndTheStore()
    {
        var store = new InMemoryApiKeyStore();
        store.Save(ScriptChatProvider.Claude, "sk-ant-stored");
        using var panel = new ScriptChatSettingsPanel { KeyStore = store };

        ClickButton(panel, "_forgetKeyButton");

        panel.HasApiKey.Should().BeFalse();
        store.Load(ScriptChatProvider.Claude).Should().BeNull();
    }

    [TestMethod]
    public void SelectedProvider_ChangedToOpenAI_SwapsTheModelListAndTheLoadedKey()
    {
        var store = new InMemoryApiKeyStore();
        store.Save(ScriptChatProvider.Claude, "claude-key");
        store.Save(ScriptChatProvider.OpenAI, "openai-key");
        using var panel = new ScriptChatSettingsPanel { KeyStore = store };

        SelectProvider(panel, ScriptChatProvider.OpenAI);

        panel.SelectedProvider.Should().Be(ScriptChatProvider.OpenAI);
        panel.SelectedModelId.Should().Be(ScriptChatModels.DefaultForProvider(ScriptChatProvider.OpenAI));
        panel.BuildClientOptions().ApiKey.Should().Be("openai-key");
    }

    private static void ClickButton(ScriptChatSettingsPanel panel, string name) =>
        panel.Controls.Find(name, searchAllChildren: true).OfType<Button>().Single().PerformClick();

    private static void SetApiKey(ScriptChatSettingsPanel panel, string key) =>
        panel.Controls.Find("_apiKeyTextBox", searchAllChildren: true).OfType<TextBox>().Single().Text = key;

    private static void SelectProvider(ScriptChatSettingsPanel panel, ScriptChatProvider provider) =>
        panel.Controls.Find("_providerComboBox", searchAllChildren: true)
            .OfType<ComboBox>().Single().SelectedItem = provider;

    /// <summary>A key store that keeps everything in memory, so tests never touch DPAPI or disk.</summary>
    private sealed class InMemoryApiKeyStore : IApiKeyStore
    {
        private readonly Dictionary<ScriptChatProvider, string> _keys = [];

        public string? Load(ScriptChatProvider provider) =>
            _keys.TryGetValue(provider, out var key) ? key : null;

        public void Save(ScriptChatProvider provider, string apiKey) => _keys[provider] = apiKey;

        public void Clear(ScriptChatProvider provider) => _keys.Remove(provider);
    }
}
