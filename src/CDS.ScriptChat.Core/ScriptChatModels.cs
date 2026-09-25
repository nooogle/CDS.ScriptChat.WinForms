namespace CDS.ScriptChat.Core;

/// <summary>
/// The model choices the settings panel offers per provider.
/// </summary>
public static class ScriptChatModels
{
    /// <summary>The default Claude model.</summary>
    public const string ClaudeDefault = "claude-opus-5-5";

    private static readonly string[] s_claudeModels =
    [
        "claude-opus-5-5",
        "claude-fable-5-1",
        "claude-sonnet-5",
        "claude-haiku-4-5",
    ];

    private static readonly string[] s_openAIModels =
    [
        "gpt-6-sol",
        "gpt-6-astra",
        "gpt-6-luna",
    ];

    private static readonly string[] s_grokModels =
    [
        "grok-4.7",
    ];

    /// <summary>
    /// Gets the suggested model IDs for a provider, default first.
    /// </summary>
    /// <param name="provider">The provider to list models for.</param>
    /// <returns>The suggested model IDs.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="provider"/> is not a known provider.</exception>
    public static IReadOnlyList<string> ForProvider(ScriptChatProvider provider) => provider switch
    {
        ScriptChatProvider.Claude => s_claudeModels,
        ScriptChatProvider.OpenAI => s_openAIModels,
        ScriptChatProvider.Grok => s_grokModels,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider."),
    };

    /// <summary>
    /// Gets the default model ID for a provider.
    /// </summary>
    /// <param name="provider">The provider to get the default for.</param>
    /// <returns>The default model ID.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="provider"/> is not a known provider.</exception>
    public static string DefaultForProvider(ScriptChatProvider provider) => ForProvider(provider)[0];
}
