using Microsoft.Extensions.AI;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// A chat client that is never actually called — these tests exercise panel wiring, not
/// conversation behaviour, which <c>CDS.ScriptChat.Core.Tests</c> covers.
/// </summary>
internal sealed class StubChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("These tests never send a turn.");
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
}
