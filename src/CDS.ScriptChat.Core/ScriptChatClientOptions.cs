namespace CDS.ScriptChat.Core;

/// <summary>
/// What <see cref="ScriptChatClientFactory"/> needs to construct a chat client.
/// </summary>
public sealed class ScriptChatClientOptions
{
    /// <summary>Gets the provider to talk to.</summary>
    public required ScriptChatProvider Provider { get; init; }

    /// <summary>
    /// Gets the user's own API key. BYOK only — this value is never persisted, logged, or
    /// transmitted anywhere except the provider SDK call itself (D3).
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>Gets the model ID to use, e.g. <c>claude-opus-5</c>.</summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Gets the ceiling on tokens generated per response. Generous by default: a proposed
    /// script replacement has to fit inside one response.
    /// </summary>
    public int MaxOutputTokens { get; init; } = 16_000;
}
