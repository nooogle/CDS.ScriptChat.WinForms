using System.Text.Json;

using Anthropic;

using AwesomeAssertions;

using Microsoft.Extensions.AI;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// Exercises the real Anthropic SDK's request/response wire format — the same construction
/// pattern <c>ScriptChatClientFactory.CreateClaudeClient</c> uses internally
/// (<c>new AnthropicClient { ApiKey = ... }.AsIChatClient(modelId, maxOutputTokens)</c>) — against
/// a fake transport, so the request shape and response parsing are proven without a real network
/// call or API key.
/// </summary>
/// <remarks>
/// The Claude counterpart of <see cref="OpenAIWireFormatTests"/>. The fixtures follow the
/// documented Messages API shape, so these tests catch an SDK upgrade that changes what is sent or
/// how a reply is parsed; they cannot catch the live API itself changing shape. This is a
/// bring-your-own-key library (D3), so nothing here talks to the real API or ever will.
/// </remarks>
[TestClass]
[TestCategory("Factory")]
public sealed class ClaudeWireFormatTests
{
    private const string TextResponseBody = """
        {
          "id": "msg_test",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-5-5",
          "content": [ { "type": "text", "text": "Hello there." } ],
          "stop_reason": "end_turn",
          "stop_sequence": null,
          "usage": { "input_tokens": 10, "output_tokens": 5 }
        }
        """;

    private const string ToolCallResponseBody = """
        {
          "id": "msg_test2",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-5-5",
          "content": [
            {
              "type": "tool_use",
              "id": "toolu_abc123",
              "name": "propose_script_edit",
              "input": { "newScript": "var x = 2;", "summary": "Bump x" }
            }
          ],
          "stop_reason": "tool_use",
          "stop_sequence": null,
          "usage": { "input_tokens": 10, "output_tokens": 5 }
        }
        """;

    [TestMethod]
    public async Task GetResponseAsync_TextReply_SendsModelAndMaxTokensAndParsesTheReply()
    {
        var handler = new RecordingHttpMessageHandler(TextResponseBody);
        var chatClient = BuildWireLevelClaudeChatClient("claude-sonnet-5-5", maxOutputTokens: 4321, handler);

        var response = await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);

        response.Text.Should().Be("Hello there.");

        handler.RequestUris.Single()!.AbsolutePath.Should().EndWith("/v1/messages");

        using var requestJson = JsonDocument.Parse(handler.RequestBodies.Single());
        var root = requestJson.RootElement;
        root.GetProperty("model").GetString().Should().Be("claude-sonnet-5-5");
        root.GetProperty("max_tokens").GetInt64().Should().Be(
            4321,
            "ScriptChatClientOptions.MaxOutputTokens is baked in through AsIChatClient's " +
            "default and must reach the real request body");

        var message = root.GetProperty("messages").EnumerateArray().Should().ContainSingle().Which;
        message.GetProperty("role").GetString().Should().Be("user");
    }

    [TestMethod]
    public async Task GetResponseAsync_OptionsWithToolsButNoMaxOutputTokens_SendsTheConfiguredDefaultAndTheTools()
    {
        // ScriptChatSession.SendAsync always passes ChatOptions { Tools = ... } with no token
        // limit, so the default must still apply when options are present but the limit is unset.
        var handler = new RecordingHttpMessageHandler(TextResponseBody);
        var chatClient = BuildWireLevelClaudeChatClient("claude-sonnet-5-5", maxOutputTokens: 4321, handler);
        var tool = AIFunctionFactory.Create((string newScript) => newScript, "propose_script_edit");

        await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hello")],
            new ChatOptions { Tools = [tool] });

        using var requestJson = JsonDocument.Parse(handler.RequestBodies.Single());
        var root = requestJson.RootElement;
        root.GetProperty("max_tokens").GetInt64().Should().Be(4321);
        root.GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString())
            .Should().ContainSingle().Which.Should().Be("propose_script_edit");
    }

    [TestMethod]
    public async Task GetResponseAsync_ExplicitMaxOutputTokensOnTheCall_IsNotOverriddenByTheConfiguredDefault()
    {
        // Mirrors ScriptChatSettingsPanel's "Test connection" button, which passes
        // MaxOutputTokens = 16 for a cheap reachability check rather than the configured ceiling.
        var handler = new RecordingHttpMessageHandler(TextResponseBody);
        var chatClient = BuildWireLevelClaudeChatClient("claude-sonnet-5-5", maxOutputTokens: 16_000, handler);

        await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Reply with the single word: ok")],
            new ChatOptions { MaxOutputTokens = 16 });

        using var requestJson = JsonDocument.Parse(handler.RequestBodies.Single());
        requestJson.RootElement.GetProperty("max_tokens").GetInt64().Should().Be(16);
    }

    [TestMethod]
    public async Task GetResponseAsync_ToolUseReply_ParsesAFunctionCallContentForProposeScriptEdit()
    {
        var handler = new RecordingHttpMessageHandler(ToolCallResponseBody);
        var chatClient = BuildWireLevelClaudeChatClient("claude-sonnet-5-5", maxOutputTokens: 1000, handler);

        var response = await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "Set x to 2")]);

        var call = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .Should().ContainSingle().Which;

        call.CallId.Should().Be("toolu_abc123");
        call.Name.Should().Be("propose_script_edit");
        call.Arguments.Should().NotBeNull();
        call.Arguments!["newScript"]!.ToString().Should().Be("var x = 2;");
        call.Arguments["summary"]!.ToString().Should().Be("Bump x");
    }

    /// <summary>
    /// Builds an <see cref="IChatClient"/> the same way <c>ScriptChatClientFactory.CreateClaudeClient</c>
    /// does, but pointed at a fake transport instead of the network.
    /// </summary>
    private static IChatClient BuildWireLevelClaudeChatClient(
        string modelId, int maxOutputTokens, RecordingHttpMessageHandler handler)
    {
        var client = new AnthropicClient
        {
            ApiKey = "test-key",
            HttpClient = new HttpClient(handler),
            MaxRetries = 0,
        };

        return client.AsIChatClient(modelId, maxOutputTokens);
    }
}
