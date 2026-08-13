using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.AI;

using OpenAI;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// Exercises <see cref="ScriptChatSession"/>'s UC2 disposition reconciliation through the same
/// pipeline production code actually uses — a real OpenAI SDK client wrapped with
/// <c>UseFunctionInvocation</c> by <see cref="ScriptChatSession"/> itself — instead of the
/// hand-rolled <see cref="FakeChatClient"/> used everywhere else.
/// </summary>
/// <remarks>
/// <see cref="ScriptChatSessionTests"/> already proves the reconciliation logic is correct
/// against <see cref="FakeChatClient"/>, which constructs <see cref="FunctionCallContent"/> and
/// lets the real function-invocation machinery mint a matching <see cref="FunctionResultContent"/>
/// entirely in memory — it never touches JSON. What it cannot prove is whether the OpenAI
/// adapter's own wire-format round trip (parsing a real <c>tool_calls[].id</c> out of a response,
/// then serialising that same id back as <c>tool_call_id</c> on the follow-up request) actually
/// lines up with the <c>CallId</c> <see cref="ScriptChatSession"/> captures via
/// <c>FunctionInvokingChatClient.CurrentContext</c> at the moment <c>propose_script_edit</c> is
/// called. This class is that proof, still without a real network call or API key (D3).
/// </remarks>
[TestClass]
[TestCategory("Session")]
public sealed class ScriptChatSessionOpenAIIntegrationTests
{
    private const string ProposeToolCallResponse = """
        {
          "id": "chatcmpl-1",
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
                    "id": "call_bump_x",
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

    private static string TextResponse(string id, string text) => $$"""
        {
          "id": "{{id}}",
          "object": "chat.completion",
          "created": 1700000000,
          "model": "gpt-5",
          "choices": [
            {
              "index": 0,
              "message": { "role": "assistant", "content": "{{text}}" },
              "finish_reason": "stop"
            }
          ],
          "usage": { "prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15 }
        }
        """;

    [TestMethod]
    public async Task SetEditDisposition_Accepted_RewritesTheRealOpenAIToolResultForTheNextTurn()
    {
        var handler = new RecordingHttpMessageHandler(
            ProposeToolCallResponse,
            TextResponse("chatcmpl-2", "Done."),
            TextResponse("chatcmpl-3", "Sure, anything else?"));

        var transport = new HttpClientPipelineTransport(new HttpClient(handler));
        var openAIClient = new OpenAIClient(new ApiKeyCredential("test-key"), new OpenAIClientOptions { Transport = transport });

        // Deliberately not wrapped with UseFunctionInvocation here — ScriptChatSession does that
        // itself in its constructor, exactly as it would for a client built by
        // ScriptChatClientFactory.CreateOpenAIClient.
        var chatClient = openAIClient.GetChatClient("gpt-5").AsIChatClient();

        var session = new ScriptChatSession(chatClient);

        await session.SendAsync("Set x to 2", "var x = 1;");
        var turnIndex = session.Turns.Count - 1;
        session.Turns[turnIndex].HasProposedEdit.Should().BeTrue();

        session.SetEditDisposition(turnIndex, EditDisposition.Accepted);
        await session.SendAsync("Thanks", "var x = 2;");

        // Request 0: initial "Set x to 2" call. Request 1: the follow-up after the function
        // invocation client appends the tool result. Request 2: the "Thanks" turn — this is the
        // one that should carry the rewritten tool-result text.
        var lastRequest = JsonDocument.Parse(handler.RequestBodies[2]);
        var toolMessages = lastRequest.RootElement.GetProperty("messages")
            .EnumerateArray()
            .Where(m => m.GetProperty("role").GetString() == "tool")
            .Select(m => m.GetProperty("content").GetString());

        toolMessages.Should().ContainSingle(text => text!.Contains("accepted", StringComparison.OrdinalIgnoreCase));
    }
}
