using Microsoft.Extensions.AI;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// An <see cref="IChatClient"/> that replays scripted responses, so session behaviour and the
/// tool-calling path can be tested without a provider or a network call.
/// </summary>
internal sealed class FakeChatClient : IChatClient
{
    private readonly Queue<ChatResponse> _responses;

    public FakeChatClient(params ChatResponse[] responses)
    {
        _responses = new Queue<ChatResponse>(responses);
    }

    /// <summary>Gets the message list passed on each call, oldest first.</summary>
    public List<List<ChatMessage>> ReceivedRequests { get; } = [];

    /// <summary>Gets the options passed on the most recent call.</summary>
    public ChatOptions? LastOptions { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ReceivedRequests.Add([.. messages]);
        LastOptions = options;

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                "FakeChatClient ran out of scripted responses — the code under test made more calls than expected.");
        }

        return Task.FromResult(_responses.Dequeue());
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Streaming is deferred past v1 (D9).");
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    /// <summary>Builds a plain text response.</summary>
    public static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    /// <summary>Builds a response that calls one tool.</summary>
    public static ChatResponse ToolCall(string name, Dictionary<string, object?> arguments)
    {
        var call = new FunctionCallContent(Guid.NewGuid().ToString("N"), name, arguments);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, [call]));
    }
}
