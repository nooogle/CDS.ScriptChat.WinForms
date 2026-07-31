using AwesomeAssertions;

namespace CDS.ScriptChat.Core.Tests;

[TestClass]
[TestCategory("Byok")]
public sealed class ScriptChatClientOptionsTests
{
    private const string SecretKey = "sk-ant-super-secret-key-value";

    private static ScriptChatClientOptions SampleOptions => new()
    {
        Provider = ScriptChatProvider.Claude,
        ApiKey = SecretKey,
        ModelId = ScriptChatModels.ClaudeDefault,
    };

    [TestMethod]
    public void ToString_Always_RedactsTheApiKey()
    {
        // A record's generated ToString prints every member, so anything that logs or
        // interpolates the options would otherwise leak the user's key (D3).
        SampleOptions.ToString().Should().NotContain(SecretKey).And.Contain("[redacted]");
    }

    [TestMethod]
    public void ToString_Always_StillShowsTheNonSecretSettings()
    {
        var text = SampleOptions.ToString();

        text.Should().Contain("Claude").And.Contain(ScriptChatModels.ClaudeDefault);
    }

    [TestMethod]
    public void With_ChangingTheModel_KeepsTheProviderAndKey()
    {
        var derived = SampleOptions with { ModelId = "claude-sonnet-5" };

        derived.Provider.Should().Be(ScriptChatProvider.Claude);
        derived.ApiKey.Should().Be(SecretKey);
        derived.ModelId.Should().Be("claude-sonnet-5");
    }
}
