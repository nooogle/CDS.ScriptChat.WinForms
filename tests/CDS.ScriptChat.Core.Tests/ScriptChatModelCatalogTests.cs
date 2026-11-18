using AwesomeAssertions;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// Exercises <see cref="ScriptChatModelCatalog"/> through the real provider SDKs against a fake
/// transport, so the request path, paging, ordering, and OpenAI's chat-model filter are proven
/// without a network call or a real key (D3).
/// </summary>
[TestClass]
[TestCategory("ModelCatalog")]
public sealed class ScriptChatModelCatalogTests
{
    private const string ClaudeFirstPage = """
        {
          "data": [
            { "type": "model", "id": "claude-sonnet-5-5", "display_name": "Claude Sonnet 5.5", "created_at": "2026-09-01T00:00:00Z" },
            { "type": "model", "id": "claude-haiku-4-5", "display_name": "Claude Haiku 4.5", "created_at": "2025-10-01T00:00:00Z" }
          ],
          "has_more": true,
          "first_id": "claude-sonnet-5-5",
          "last_id": "claude-haiku-4-5"
        }
        """;

    private const string ClaudeSecondPage = """
        {
          "data": [
            { "type": "model", "id": "claude-fable-5-2", "display_name": "Claude Fable 5.2", "created_at": "2026-10-15T00:00:00Z" }
          ],
          "has_more": false,
          "first_id": "claude-fable-5-2",
          "last_id": "claude-fable-5-2"
        }
        """;

    private const string OpenAIModels = """
        {
          "object": "list",
          "data": [
            { "id": "gpt-6-luna", "object": "model", "created": 1760000000, "owned_by": "openai" },
            { "id": "text-embedding-3-large", "object": "model", "created": 1790000000, "owned_by": "openai" },
            { "id": "gpt-6-sol", "object": "model", "created": 1780000000, "owned_by": "openai" },
            { "id": "gpt-6-realtime", "object": "model", "created": 1785000000, "owned_by": "openai" },
            { "id": "o5", "object": "model", "created": 1770000000, "owned_by": "openai" },
            { "id": "whisper-1", "object": "model", "created": 1677000000, "owned_by": "openai-internal" }
          ]
        }
        """;

    [TestMethod]
    public async Task ListModelsAsync_Claude_FollowsEveryPageAndReturnsNewestFirst()
    {
        var handler = new RecordingHttpMessageHandler(ClaudeFirstPage, ClaudeSecondPage);

        var models = await ScriptChatModelCatalog.ListModelsAsync(
            ScriptChatProvider.Claude, "test-key", handler, CancellationToken.None);

        models.Select(m => m.Id).Should().Equal("claude-fable-5-2", "claude-sonnet-5-5", "claude-haiku-4-5");
        models[0].DisplayName.Should().Be("Claude Fable 5.2");
        handler.RequestUris.Should().HaveCount(2);
        handler.RequestUris[0]!.AbsolutePath.Should().EndWith("/v1/models");
    }

    [TestMethod]
    public async Task ListModelsAsync_OpenAI_KeepsOnlyChatModelsNewestFirst()
    {
        var handler = new RecordingHttpMessageHandler(OpenAIModels);

        var models = await ScriptChatModelCatalog.ListModelsAsync(
            ScriptChatProvider.OpenAI, "test-key", handler, CancellationToken.None);

        models.Select(m => m.Id).Should().Equal("gpt-6-sol", "o5", "gpt-6-luna");
        handler.RequestUris.Single()!.AbsolutePath.Should().EndWith("/models");
    }

    [TestMethod]
    public async Task ListModelsAsync_Grok_ThrowsNotSupported()
    {
        var act = () => ScriptChatModelCatalog.ListModelsAsync(
            ScriptChatProvider.Grok, "test-key", CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [TestMethod]
    public async Task ListModelsAsync_NoKey_ThrowsBeforeAnyRequest()
    {
        var handler = new RecordingHttpMessageHandler();

        var act = () => ScriptChatModelCatalog.ListModelsAsync(
            ScriptChatProvider.Claude, " ", handler, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        handler.RequestUris.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(ScriptChatProvider.Claude, true)]
    [DataRow(ScriptChatProvider.OpenAI, true)]
    [DataRow(ScriptChatProvider.Grok, false)]
    public void CanListModels_ReportsOnlyWiredProviders(ScriptChatProvider provider, bool expected)
    {
        ScriptChatModelCatalog.CanListModels(provider).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("gpt-6-sol", true)]
    [DataRow("o5-mini", true)]
    [DataRow("gpt-6-audio-preview", false)]
    [DataRow("gpt-6-mini-transcribe", false)]
    [DataRow("gpt-6-mini-tts", false)]
    [DataRow("gpt-image-2", false)]
    [DataRow("gpt-6-search-preview", false)]
    [DataRow("gpt-6-codex", false)]
    [DataRow("gpt-3.5-turbo-instruct", false)]
    [DataRow("omni-moderation-latest", false)]
    [DataRow("text-embedding-3-small", false)]
    [DataRow("dall-e-3", false)]
    public void IsOpenAIChatModel_FiltersByName(string modelId, bool expected)
    {
        ScriptChatModelCatalog.IsOpenAIChatModel(modelId).Should().Be(expected);
    }
}
