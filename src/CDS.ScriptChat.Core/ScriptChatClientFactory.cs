using Anthropic;

using Microsoft.Extensions.AI;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Constructs an <see cref="IChatClient"/> for a provider. This is the only place in the
/// library that knows Claude, OpenAI, or Grok exist (D2).
/// </summary>
public static class ScriptChatClientFactory
{
    /// <summary>
    /// Creates a chat client for the configured provider.
    /// </summary>
    /// <param name="options">The provider, key, and model to use.</param>
    /// <returns>A chat client the session can converse over.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The API key or model ID is missing.</exception>
    /// <exception cref="NotSupportedException">
    /// The provider is recognised but not yet wired up. Claude is wired end to end first;
    /// OpenAI and Grok follow once the shape is confirmed working.
    /// </exception>
    public static IChatClient Create(ScriptChatClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException(
                "No API key configured. CDS.ScriptChat is bring-your-own-key: the user must supply one before the panel can talk to a provider.",
                nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.ModelId))
        {
            throw new ArgumentException("No model ID configured.", nameof(options));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.MaxOutputTokens, 0, nameof(options));

        return options.Provider switch
        {
            ScriptChatProvider.Claude => CreateClaudeClient(options),

            ScriptChatProvider.OpenAI or ScriptChatProvider.Grok => throw new NotSupportedException(
                $"Provider '{options.Provider}' is not wired up yet. Claude is being proven end to end first; the others follow once the shape is confirmed."),

            _ => throw new ArgumentOutOfRangeException(
                nameof(options), options.Provider, "Unknown provider."),
        };
    }

    private static IChatClient CreateClaudeClient(ScriptChatClientOptions options)
    {
        var client = new AnthropicClient { ApiKey = options.ApiKey };
        return client.AsIChatClient(options.ModelId, options.MaxOutputTokens);
    }
}
