namespace CDS.ScriptChat.Core;

/// <summary>
/// A model a provider reports as available to the caller's API key.
/// </summary>
/// <param name="Id">The model ID to pass as <see cref="ScriptChatClientOptions.ModelId"/>.</param>
/// <param name="DisplayName">The provider's human-readable name, or <see langword="null"/> if it gives none.</param>
/// <param name="CreatedAt">When the provider released the model.</param>
public sealed record ScriptChatModelInfo(string Id, string? DisplayName, DateTimeOffset CreatedAt);
