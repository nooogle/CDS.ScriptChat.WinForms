namespace CDS.ScriptChat.Core;

/// <summary>
/// The AI providers the panel can talk to. This enum and
/// <see cref="ScriptChatClientFactory"/> are the only places in the library that know
/// specific providers exist (D2) — everything else consumes
/// <see cref="Microsoft.Extensions.AI.IChatClient"/>.
/// </summary>
public enum ScriptChatProvider
{
    /// <summary>Anthropic Claude.</summary>
    Claude,

    /// <summary>OpenAI.</summary>
    OpenAI,

    /// <summary>xAI Grok.</summary>
    Grok,
}
