using AwesomeAssertions;

namespace CDS.ScriptChat.Core.Tests;

[TestClass]
[TestCategory("Factory")]
public sealed class ScriptChatClientFactoryTests
{
    [TestMethod]
    public void Create_ClaudeWithValidOptions_ReturnsAClient()
    {
        var options = new ScriptChatClientOptions
        {
            Provider = ScriptChatProvider.Claude,
            ApiKey = "sk-ant-not-a-real-key",
            ModelId = ScriptChatModels.ClaudeDefault,
        };

        using var client = ScriptChatClientFactory.Create(options);

        client.Should().NotBeNull();
    }

    [TestMethod]
    public void Create_NoApiKey_ThrowsSoTheFeatureStaysInert()
    {
        var options = new ScriptChatClientOptions
        {
            Provider = ScriptChatProvider.Claude,
            ApiKey = "   ",
            ModelId = ScriptChatModels.ClaudeDefault,
        };

        var act = () => ScriptChatClientFactory.Create(options);

        act.Should().Throw<ArgumentException>().WithMessage("*bring-your-own-key*");
    }

    [TestMethod]
    public void Create_NoModelId_Throws()
    {
        var options = new ScriptChatClientOptions
        {
            Provider = ScriptChatProvider.Claude,
            ApiKey = "sk-ant-not-a-real-key",
            ModelId = "",
        };

        var act = () => ScriptChatClientFactory.Create(options);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Create_NonPositiveMaxOutputTokens_Throws()
    {
        var options = new ScriptChatClientOptions
        {
            Provider = ScriptChatProvider.Claude,
            ApiKey = "sk-ant-not-a-real-key",
            ModelId = ScriptChatModels.ClaudeDefault,
            MaxOutputTokens = 0,
        };

        var act = () => ScriptChatClientFactory.Create(options);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    [DataRow(ScriptChatProvider.OpenAI)]
    [DataRow(ScriptChatProvider.Grok)]
    public void Create_ProviderNotYetWiredUp_ThrowsNotSupported(ScriptChatProvider provider)
    {
        var options = new ScriptChatClientOptions
        {
            Provider = provider,
            ApiKey = "not-a-real-key",
            ModelId = ScriptChatModels.DefaultForProvider(provider),
        };

        var act = () => ScriptChatClientFactory.Create(options);

        act.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void DefaultForProvider_Claude_IsTheStrongestCodingModel()
    {
        ScriptChatModels.DefaultForProvider(ScriptChatProvider.Claude)
            .Should().Be("claude-opus-5");
    }
}
