using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.AI;

using OpenAI;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// Exercises the real OpenAI SDK's request/response wire format — the same construction
/// pattern <c>ScriptChatClientFactory.CreateOpenAIClient</c> uses internally
/// (<c>OpenAIClient(...).GetChatClient(modelId).AsIChatClient().AsBuilder().ConfigureOptions(...)</c>)
/// — against a fake transport, so the request shape and response parsing are proven without a
/// real network call or API key.
/// </summary>
/// <remarks>
/// <see cref="ScriptChatClientFactoryTests"/> covers the factory's own validation and dispatch
/// logic against a fake key, and <see cref="ScriptChatSessionTests"/> covers session behaviour
/// against a fake <see cref="IChatClient"/>; neither exercises what the real OpenAI SDK actually
/// puts on the wire, or how it parses a real response back. This class fills that gap without
/// needing BYOK credentials — this is a bring-your-own-key library (D3), so nothing here talks
/// to the real API or ever will.
/// </remarks>
[TestClass]
[TestCategory("Factory")]
public sealed class OpenAIWireFormatTests
{
    private const string TextResponseBody = """
        {
          "id": "chatcmpl-test",
          "object": "chat.completion",
          "created": 1700000000,
          "model": "gpt-5",
          "choices": [
            {
              "index": 0,
              "message": { "role": "assistant", "content": "Hello there." },
              "finish_reason": "stop"
            }
          ],
          "usage": { "prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15 }
        }
        """;

    private const string ToolCallResponseBody = """
        {
          "id": "chatcmpl-test2",
          "object": "chat.completion",
          "created": 1700000000,
          "model": "gpt-5",
          "choices": [
            {
              "index": 0,
              "message": {
                "role": "assistant",
                "content": null,
                "tool_calls": [
                  {
                    "id": "call_abc123",
                    "type": "function",
                    "function": {
                      "name": "propose_script_edit",
                      "arguments": "{\"newScript\":\"var x = 2;\",\"summary\":\"Bump x\"}"
                    }
                  }
                ]
              },
              "finish_reason": "tool_calls"
            }
          ],
          "usage": { "prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15 }
        }
        """;

    [TestMethod]
    public async Task GetResponseAsync_TextReply_SendsModelAndMaxOutputTokensAndParsesTheReply()
    {
        var handler = new RecordingHttpMessageHandler(TextResponseBody);
        var chatClient = BuildWireLevelOpenAIChatClient("gpt-5", maxOutputTokens: 4321, handler);

        var response = await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);

        response.Text.Should().Be("Hello there.");

        using var requestJson = JsonDocument.Parse(handler.RequestBodies.Single());
        requestJson.RootElement.GetProperty("model").GetString().Should().Be("gpt-5");
        FindTokenLimit(requestJson.RootElement).Should().Be(
            4321,
            "ScriptChatClientOptions.MaxOutputTokens should reach the real request body, " +
            "not just sit unused in ChatOptions (this is what ConfigureOptions in " +
            "CreateOpenAIClient exists to guarantee, since unlike Anthropic's client this " +
            "provider has no constructor overload to bake in a default)");
    }

    [TestMethod]
    public async Task GetResponseAsync_ExplicitMaxOutputTokensOnTheCall_IsNotOverriddenByTheConfiguredDefault()
    {
        // Mirrors ScriptChatSettingsPanel's "Test connection" button, which deliberately passes
        // MaxOutputTokens = 16 for a cheap credential/reachability check rather than the
        // configured ceiling (often in the thousands). ConfigureOptions uses ??=, so an explicit
        // per-call value must win.
        var handler = new RecordingHttpMessageHandler(TextResponseBody);
        var chatClient = BuildWireLevelOpenAIChatClient("gpt-5", maxOutputTokens: 16_000, handler);

        await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Reply with the single word: ok")],
            new ChatOptions { MaxOutputTokens = 16 });

        using var requestJson = JsonDocument.Parse(handler.RequestBodies.Single());
        FindTokenLimit(requestJson.RootElement).Should().Be(16);
    }

    [TestMethod]
    public async Task GetResponseAsync_ToolCallReply_ParsesAFunctionCallContentForProposeScriptEdit()
    {
        var handler = new RecordingHttpMessageHandler(ToolCallResponseBody);
        var chatClient = BuildWireLevelOpenAIChatClient("gpt-5", maxOutputTokens: 1000, handler);

        var response = await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "Set x to 2")]);

        var call = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .Should().ContainSingle().Which;

        call.Name.Should().Be("propose_script_edit");
        call.Arguments.Should().NotBeNull();
        call.Arguments!["newScript"]!.ToString().Should().Be("var x = 2;");
        call.Arguments["summary"]!.ToString().Should().Be("Bump x");
    }

    /// <summary>
    /// Builds an <see cref="IChatClient"/> the same way <c>ScriptChatClientFactory.CreateOpenAIClient</c>
    /// does, but pointed at a fake transport instead of the network.
    /// </summary>
    private static IChatClient BuildWireLevelOpenAIChatClient(
        string modelId, int maxOutputTokens, RecordingHttpMessageHandler handler)
    {
        var transport = new HttpClientPipelineTransport(new HttpClient(handler));
        var clientOptions = new OpenAIClientOptions { Transport = transport };
        var client = new OpenAIClient(new ApiKeyCredential("test-key"), clientOptions);

        return client.GetChatClient(modelId)
            .AsIChatClient()
            .AsBuilder()
            .ConfigureOptions(chatOptions => chatOptions.MaxOutputTokens ??= maxOutputTokens)
            .Build();
    }

    /// <summary>
    /// The Chat Completions API has used different field names for the output-token ceiling
    /// across API/SDK versions (<c>max_tokens</c>, <c>max_completion_tokens</c>); this looks for
    /// whichever one the installed SDK actually sent, rather than assuming one.
    /// </summary>
    private static long? FindTokenLimit(JsonElement root)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Contains("token", StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.Number)
            {
                return property.Value.GetInt64();
            }
        }

        return null;
    }
}
