using System.Text;

namespace CDS.ScriptChat.Core;

/// <summary>
/// What <see cref="ScriptChatClientFactory"/> needs to construct a chat client.
/// </summary>
/// <remarks>
/// A record so callers can derive a variant with <c>with</c> — changing just the model while
/// keeping the provider and key, for instance.
/// </remarks>
public sealed record ScriptChatClientOptions
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

    /// <summary>
    /// Keeps <see cref="ApiKey"/> out of the compiler-generated <see cref="ToString"/>.
    /// Without this, logging or interpolating the options would print the user's key (D3).
    /// </summary>
    /// <param name="builder">The builder the record writes its members into.</param>
    /// <returns><see langword="true"/>, since members were written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Append($"{nameof(Provider)} = {Provider}, ");
        builder.Append($"{nameof(ApiKey)} = [redacted], ");
        builder.Append($"{nameof(ModelId)} = {ModelId}, ");
        builder.Append($"{nameof(MaxOutputTokens)} = {MaxOutputTokens}");
        return true;
    }
}
