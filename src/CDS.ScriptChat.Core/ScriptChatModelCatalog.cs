using System.ClientModel;
using System.ClientModel.Primitives;

using Anthropic;
using Anthropic.Models.Models;

using OpenAI;
using OpenAI.Models;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Asks a provider which models the caller's API key can use, so a model picker can show the
/// live list rather than one compiled into the library (D25). Alongside
/// <see cref="ScriptChatClientFactory"/>, the only place that knows the providers exist (D2).
/// </summary>
/// <remarks>
/// The key goes to the provider's own SDK call and nowhere else (D3). Nothing is cached here;
/// <see cref="ScriptChatModels"/> stays the fallback when the list cannot be fetched.
/// </remarks>
public static class ScriptChatModelCatalog
{
    // OpenAI's model list covers every modality on the account and says nothing about which
    // models accept chat completions, so the chat ones are picked out by name. This list goes
    // stale more slowly than a list of IDs would, but it does go stale.
    private static readonly string[] s_openAINonChatMarkers =
    [
        "audio",
        "codex",
        "embedding",
        "image",
        "instruct",
        "moderation",
        "realtime",
        "search",
        "transcribe",
        "tts",
    ];

    /// <summary>
    /// Gets a value indicating whether <see cref="ListModelsAsync(ScriptChatProvider, string, CancellationToken)"/>
    /// can answer for a provider.
    /// </summary>
    /// <param name="provider">The provider to check.</param>
    /// <returns><see langword="true"/> for Claude and OpenAI; <see langword="false"/> otherwise.</returns>
    public static bool CanListModels(ScriptChatProvider provider) =>
        provider is ScriptChatProvider.Claude or ScriptChatProvider.OpenAI;

    /// <summary>
    /// Lists the chat models a provider offers to an API key, newest first.
    /// </summary>
    /// <param name="provider">The provider to ask.</param>
    /// <param name="apiKey">The caller's own key for that provider.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The models, most recently released first.</returns>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> is missing.</exception>
    /// <exception cref="NotSupportedException">The provider cannot list its models yet.</exception>
    public static Task<IReadOnlyList<ScriptChatModelInfo>> ListModelsAsync(
        ScriptChatProvider provider,
        string apiKey,
        CancellationToken cancellationToken) =>
        ListModelsAsync(provider, apiKey, httpMessageHandler: null, cancellationToken);

    internal static async Task<IReadOnlyList<ScriptChatModelInfo>> ListModelsAsync(
        ScriptChatProvider provider,
        string apiKey,
        HttpMessageHandler? httpMessageHandler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("No API key supplied.", nameof(apiKey));
        }

        var models = provider switch
        {
            ScriptChatProvider.Claude =>
                await ListClaudeModelsAsync(apiKey, httpMessageHandler, cancellationToken).ConfigureAwait(false),
            ScriptChatProvider.OpenAI =>
                await ListOpenAIModelsAsync(apiKey, httpMessageHandler, cancellationToken).ConfigureAwait(false),
            ScriptChatProvider.Grok => throw new NotSupportedException(
                $"Provider '{provider}' cannot list its models: it is not wired up yet."),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider."),
        };

        return models
            .OrderByDescending(model => model.CreatedAt)
            .ThenBy(model => model.Id, StringComparer.Ordinal)
            .ToArray();
    }

    internal static bool IsOpenAIChatModel(string modelId)
    {
        var isChatFamily = modelId.StartsWith("gpt-", StringComparison.Ordinal)
            || (modelId.Length > 1 && modelId[0] == 'o' && char.IsAsciiDigit(modelId[1]));

        return isChatFamily
            && !s_openAINonChatMarkers.Any(marker => modelId.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<List<ScriptChatModelInfo>> ListClaudeModelsAsync(
        string apiKey,
        HttpMessageHandler? httpMessageHandler,
        CancellationToken cancellationToken)
    {
        var client = httpMessageHandler is null
            ? new AnthropicClient { ApiKey = apiKey }
            : new AnthropicClient { ApiKey = apiKey, HttpClient = new HttpClient(httpMessageHandler), MaxRetries = 0 };

        var firstPage = await client.Models
            .List(new ModelListParams { Limit = 1000 }, cancellationToken)
            .ConfigureAwait(false);

        var models = new List<ScriptChatModelInfo>();
        await foreach (var model in firstPage.Paginate(cancellationToken).ConfigureAwait(false))
        {
            models.Add(new ScriptChatModelInfo(model.ID, model.DisplayName, model.CreatedAt));
        }

        return models;
    }

    private static async Task<List<ScriptChatModelInfo>> ListOpenAIModelsAsync(
        string apiKey,
        HttpMessageHandler? httpMessageHandler,
        CancellationToken cancellationToken)
    {
        var options = new OpenAIClientOptions();
        if (httpMessageHandler is not null)
        {
            options.Transport = new HttpClientPipelineTransport(new HttpClient(httpMessageHandler));
        }

        var client = new OpenAIModelClient(new ApiKeyCredential(apiKey), options);
        var result = await client.GetModelsAsync(cancellationToken).ConfigureAwait(false);

        return result.Value
            .Where(model => IsOpenAIChatModel(model.Id))
            .Select(model => new ScriptChatModelInfo(model.Id, DisplayName: null, model.CreatedAt))
            .ToList();
    }
}
