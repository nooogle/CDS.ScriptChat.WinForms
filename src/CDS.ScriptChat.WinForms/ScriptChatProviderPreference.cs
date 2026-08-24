using CDS.ScriptChat.Core;

namespace CDS.ScriptChat.WinForms;

/// <summary>
/// The provider and model a user last chose. Not secret — the API key itself lives in
/// <see cref="IApiKeyStore"/> and never appears here (D3).
/// </summary>
/// <param name="Provider">The provider the user selected.</param>
/// <param name="ModelId">
/// The model they selected, or <see langword="null"/> to use
/// <see cref="ScriptChatModels.DefaultForProvider(ScriptChatProvider)"/>.
/// </param>
public sealed record ScriptChatProviderPreference(ScriptChatProvider Provider, string? ModelId);
