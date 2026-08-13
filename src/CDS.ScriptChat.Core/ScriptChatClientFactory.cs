using System.ClientModel;

using Anthropic;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using OpenAI;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Constructs an <see cref="IChatClient"/> for a provider. This is the only place in the
/// library that knows Claude, OpenAI, or Grok exist (D2).
/// </summary>
public static class ScriptChatClientFactory
{
    /// <summary>
    /// Creates a chat client for the configured provider, without logging.
    /// </summary>
    /// <param name="options">The provider, key, and model to use.</param>
    /// <returns>A chat client the session can converse over.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The API key or model ID is missing.</exception>
    /// <exception cref="NotSupportedException">The provider is recognised but not yet wired up.</exception>
    public static IChatClient Create(ScriptChatClientOptions options)
    {
        return Create(options, loggerFactory: null);
    }

    /// <summary>
    /// Creates a chat client for the configured provider.
    /// </summary>
    /// <param name="options">The provider, key, and model to use.</param>
    /// <param name="loggerFactory">
    /// Where to log the construction attempt and why it was rejected when it fails.
    /// <see langword="null"/> disables logging. The API key is never logged — only its length,
    /// which is what distinguishes a truncated paste from a wrong key (D3).
    /// </param>
    /// <returns>A chat client the session can converse over.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The API key or model ID is missing.</exception>
    /// <exception cref="NotSupportedException">
    /// The provider is recognised but not yet wired up. Claude and OpenAI are wired end to end;
    /// Grok follows once a host needs it.
    /// </exception>
    public static IChatClient Create(ScriptChatClientOptions options, ILoggerFactory? loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);

        var logger = loggerFactory?.CreateLogger(typeof(ScriptChatClientFactory)) ?? NullLogger.Instance;

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            logger.ClientOptionsRejected("No API key configured.");
            throw new ArgumentException(
                "No API key configured. CDS.ScriptChat is bring-your-own-key: the user must supply one before the panel can talk to a provider.",
                nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.ModelId))
        {
            logger.ClientOptionsRejected("No model ID configured.");
            throw new ArgumentException("No model ID configured.", nameof(options));
        }

        if (options.MaxOutputTokens <= 0)
        {
            logger.ClientOptionsRejected($"MaxOutputTokens must be positive, but was {options.MaxOutputTokens}.");
            throw new ArgumentOutOfRangeException(
                nameof(options), options.MaxOutputTokens, "MaxOutputTokens must be positive.");
        }

        logger.ClientCreating(options.Provider, options.ModelId, options.MaxOutputTokens, options.ApiKey.Length);

        var client = options.Provider switch
        {
            ScriptChatProvider.Claude => CreateClaudeClient(options),
            ScriptChatProvider.OpenAI => CreateOpenAIClient(options),

            ScriptChatProvider.Grok => throw new NotSupportedException(
                $"Provider '{options.Provider}' is not wired up yet. Claude and OpenAI are proven end to end first; Grok follows once there's a host that needs it."),

            _ => throw new ArgumentOutOfRangeException(
                nameof(options), options.Provider, "Unknown provider."),
        };

        logger.ClientCreated(options.Provider, options.ModelId);
        return client;
    }

    private static IChatClient CreateClaudeClient(ScriptChatClientOptions options)
    {
        var client = new AnthropicClient { ApiKey = options.ApiKey };
        return client.AsIChatClient(options.ModelId, options.MaxOutputTokens);
    }

    private static IChatClient CreateOpenAIClient(ScriptChatClientOptions options)
    {
        var client = new OpenAIClient(new ApiKeyCredential(options.ApiKey));

        // Captured as a local rather than closing over `options` itself, so the built client
        // doesn't hold a live reference to the API key for its whole lifetime (D3) just to reach
        // one unrelated int.
        var maxOutputTokens = options.MaxOutputTokens;

        // Unlike Anthropic.AsIChatClient(modelId, maxOutputTokens), the OpenAI adapter has no
        // constructor overload to bake in a default token ceiling — ConfigureOptions is the
        // equivalent for this provider. ??= so an explicit per-call value (none exist yet) would
        // still win.
        return client.GetChatClient(options.ModelId)
            .AsIChatClient()
            .AsBuilder()
            .ConfigureOptions(chatOptions => chatOptions.MaxOutputTokens ??= maxOutputTokens)
            .Build();
    }
}
