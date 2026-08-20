using Microsoft.Extensions.AI;

namespace CDS.ScriptChat.TestHost.Demo;

/// <summary>
/// An <see cref="IChatClient"/> that replies with a fixed message instantly, no network call —
/// used only behind <c>--demo=markdown</c> so the transcript can be exercised end to end without
/// a real provider key.
/// </summary>
internal sealed class EchoChatClient(string reply) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Streaming is deferred past v1 (D9).");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
