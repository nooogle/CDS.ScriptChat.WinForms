using Microsoft.Extensions.AI;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// A chat client whose one reply is held until the test releases it, so a test can act while a
/// turn is genuinely in flight.
/// </summary>
internal sealed class HeldChatClient : IChatClient
{
    private readonly TaskCompletionSource<ChatResponse> _reply = new();

    /// <summary>Releases the held reply.</summary>
    /// <param name="response">The reply to deliver.</param>
    public void Release(ChatResponse response) => _reply.SetResult(response);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        _reply.Task;

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
