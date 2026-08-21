using CDS.ScriptChat.Core;
using CDS.ScriptChat.WinForms;

using Microsoft.Extensions.AI;

namespace CDS.ScriptChat.TestHost.Demo;

/// <summary>
/// Seeds the chat panel with one canned turn that proposes a patch edit, without a real provider
/// key — the fixture <c>--demo=patch</c> launches this for UI-automation coverage of the
/// diff/accept UI (Job 3's <c>propose_script_patch</c>) against a real, painted window.
/// </summary>
internal static class PatchDemo
{
    /// <summary>The reply the assistant turn carries once seeded — read back by UI tests.</summary>
    public const string Reply = "I've added a rebase step before the return, so downstream stages see aligned coordinates.";

    /// <summary>Attaches a fresh session to <paramref name="panel"/> with one seeded proposal turn.</summary>
    /// <param name="panel">The chat panel to seed.</param>
    /// <param name="script">The script the patch is proposed against.</param>
    public static async Task SeedAsync(ScriptChatPanel panel, string script)
    {
        var call = new FunctionCallContent(
            Guid.NewGuid().ToString("N"),
            "propose_script_patch",
            new Dictionary<string, object?>
            {
                ["hunks"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["oldText"] = "var result = frame;",
                        ["newText"] = "var result = frame.Rebase();",
                    },
                },
                ["summary"] = "Rebase the frame before returning it.",
            });

        var session = new ScriptChatSession(new DemoToolCallChatClient(call, Reply));
        await session.SendAsync("Rebase the frame before returning it.", script);
        panel.AttachSession(session);
    }

    /// <summary>Replies with one scripted tool call, then plain text — no network call.</summary>
    private sealed class DemoToolCallChatClient(FunctionCallContent call, string followUpText) : IChatClient
    {
        private int _callCount;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = Interlocked.Increment(ref _callCount) == 1
                ? new ChatResponse(new ChatMessage(ChatRole.Assistant, [call]))
                : new ChatResponse(new ChatMessage(ChatRole.Assistant, followUpText));

            return Task.FromResult(response);
        }

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
}
