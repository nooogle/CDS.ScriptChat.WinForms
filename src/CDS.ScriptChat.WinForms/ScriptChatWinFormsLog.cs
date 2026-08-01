using CDS.ScriptChat.Core;

using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.WinForms;

/// <summary>
/// Every log message the WinForms layer emits, as compile-time generated
/// <see cref="LoggerMessage"/> methods.
/// </summary>
/// <remarks>
/// <para>
/// IDs are banded by area: 2000–2099 the chat panel, 2100–2199 the settings panel, 2200–2299 the
/// key store. The core library uses the 1000 band.
/// </para>
/// <para>
/// The same level discipline applies here as in the core library (D16): user and model content
/// only ever appears at <see cref="LogLevel.Trace"/>. API key material appears at no level at
/// all — not its value, not a prefix, not a hash (D3). Where a key's presence matters, its
/// length is logged instead, which is enough to tell a truncated paste from a wrong key.
/// </para>
/// </remarks>
internal static partial class ScriptChatWinFormsLog
{
    [LoggerMessage(
        EventId = 2000,
        EventName = "PanelConfigured",
        Level = LogLevel.Information,
        Message = "Chat panel configured. Provider={Provider} Model={ModelId}")]
    public static partial void PanelConfigured(this ILogger logger, ScriptChatProvider provider, string modelId);

    [LoggerMessage(
        EventId = 2001,
        EventName = "PanelConfigurationFailed",
        Level = LogLevel.Error,
        Message = "Configuring the chat panel for {Provider} failed; the panel is now unavailable.")]
    public static partial void PanelConfigurationFailed(
        this ILogger logger,
        Exception exception,
        ScriptChatProvider provider);

    [LoggerMessage(
        EventId = 2002,
        EventName = "SessionAttached",
        Level = LogLevel.Information,
        Message = "Session attached. ExistingTurns={ExistingTurns} HasScriptProvider={HasScriptProvider} HasScriptSetter={HasScriptSetter}")]
    public static partial void SessionAttached(
        this ILogger logger,
        int existingTurns,
        bool hasScriptProvider,
        bool hasScriptSetter);

    [LoggerMessage(
        EventId = 2003,
        EventName = "SessionDetached",
        Level = LogLevel.Information,
        Message = "Session detached; the panel is inert.")]
    public static partial void SessionDetached(this ILogger logger);

    [LoggerMessage(
        EventId = 2004,
        EventName = "PanelUnavailable",
        Level = LogLevel.Information,
        Message = "Chat panel marked unavailable: {Reason}")]
    public static partial void PanelUnavailable(this ILogger logger, string reason);

    [LoggerMessage(
        EventId = 2010,
        EventName = "SendRequested",
        Level = LogLevel.Information,
        Message = "Send requested. MessageLength={MessageLength} ScriptLength={ScriptLength}")]
    public static partial void SendRequested(this ILogger logger, int messageLength, int scriptLength);

    [LoggerMessage(
        EventId = 2011,
        EventName = "SendIgnored",
        Level = LogLevel.Debug,
        Message = "Send ignored: {Reason}")]
    public static partial void SendIgnored(this ILogger logger, string reason);

    [LoggerMessage(
        EventId = 2012,
        EventName = "SendCompleted",
        Level = LogLevel.Information,
        Message = "Send completed in {ElapsedMs}ms. ProposedEdit={ProposedEdit} SymbolLookups={SymbolLookups}")]
    public static partial void SendCompleted(
        this ILogger logger,
        long elapsedMs,
        bool proposedEdit,
        int symbolLookups);

    [LoggerMessage(
        EventId = 2013,
        EventName = "SendFailed",
        Level = LogLevel.Error,
        Message = "Send failed after {ElapsedMs}ms; the failure was shown in the transcript.")]
    public static partial void SendFailed(this ILogger logger, Exception exception, long elapsedMs);

    [LoggerMessage(
        EventId = 2020,
        EventName = "EditAccepted",
        Level = LogLevel.Information,
        Message = "Turn {TurnIndex} edit accepted and applied. ScriptLength={ScriptLength}")]
    public static partial void EditAccepted(this ILogger logger, int turnIndex, int scriptLength);

    [LoggerMessage(
        EventId = 2021,
        EventName = "EditRejected",
        Level = LogLevel.Information,
        Message = "Turn {TurnIndex} edit rejected.")]
    public static partial void EditRejected(this ILogger logger, int turnIndex);

    [LoggerMessage(
        EventId = 2022,
        EventName = "EditApplyFailed",
        Level = LogLevel.Error,
        Message = "Applying turn {TurnIndex}'s accepted edit to the host editor failed; the proposal stays pending.")]
    public static partial void EditApplyFailed(this ILogger logger, Exception exception, int turnIndex);

    [LoggerMessage(
        EventId = 2023,
        EventName = "EditApplyHadNoSetter",
        Level = LogLevel.Warning,
        Message = "An edit was accepted but no ScriptTextSetter is configured, so nothing was applied.")]
    public static partial void EditApplyHadNoSetter(this ILogger logger);

    [LoggerMessage(
        EventId = 2024,
        EventName = "EditActionIgnored",
        Level = LogLevel.Debug,
        Message = "An accept or reject was ignored: the turn is no longer pending review.")]
    public static partial void EditActionIgnored(this ILogger logger);

    [LoggerMessage(
        EventId = 2100,
        EventName = "ProviderSelectionChanged",
        Level = LogLevel.Information,
        Message = "Provider selection changed to {Provider}. Model={ModelId} HasStoredKey={HasStoredKey}")]
    public static partial void ProviderSelectionChanged(
        this ILogger logger,
        ScriptChatProvider provider,
        string modelId,
        bool hasStoredKey);

    [LoggerMessage(
        EventId = 2101,
        EventName = "ConfigurationApplied",
        Level = LogLevel.Information,
        Message = "Configuration applied. Provider={Provider} Model={ModelId} ApiKeyLength={ApiKeyLength}")]
    public static partial void ConfigurationApplied(
        this ILogger logger,
        ScriptChatProvider provider,
        string modelId,
        int apiKeyLength);

    [LoggerMessage(
        EventId = 2102,
        EventName = "ConfigurationIncomplete",
        Level = LogLevel.Warning,
        Message = "Configuration is incomplete and was not applied: {Reason}")]
    public static partial void ConfigurationIncomplete(this ILogger logger, string reason);

    [LoggerMessage(
        EventId = 2110,
        EventName = "ConnectionTestStarted",
        Level = LogLevel.Information,
        Message = "Testing the {Provider} connection with model {ModelId}.")]
    public static partial void ConnectionTestStarted(
        this ILogger logger,
        ScriptChatProvider provider,
        string modelId);

    [LoggerMessage(
        EventId = 2111,
        EventName = "ConnectionTestSucceeded",
        Level = LogLevel.Information,
        Message = "Connection test succeeded in {ElapsedMs}ms. Provider={Provider} ReplyLength={ReplyLength}")]
    public static partial void ConnectionTestSucceeded(
        this ILogger logger,
        long elapsedMs,
        ScriptChatProvider provider,
        int replyLength);

    [LoggerMessage(
        EventId = 2112,
        EventName = "ConnectionTestFailed",
        Level = LogLevel.Error,
        Message = "Connection test failed after {ElapsedMs}ms. Provider={Provider}")]
    public static partial void ConnectionTestFailed(
        this ILogger logger,
        Exception exception,
        long elapsedMs,
        ScriptChatProvider provider);

    [LoggerMessage(
        EventId = 2200,
        EventName = "ApiKeyLoaded",
        Level = LogLevel.Information,
        Message = "Stored API key loaded for {Provider}. Length={ApiKeyLength}")]
    public static partial void ApiKeyLoaded(this ILogger logger, ScriptChatProvider provider, int apiKeyLength);

    [LoggerMessage(
        EventId = 2201,
        EventName = "ApiKeyNotStored",
        Level = LogLevel.Information,
        Message = "No API key stored for {Provider} at {Path}.")]
    public static partial void ApiKeyNotStored(this ILogger logger, ScriptChatProvider provider, string path);

    [LoggerMessage(
        EventId = 2202,
        EventName = "ApiKeyUndecryptable",
        Level = LogLevel.Warning,
        Message = "The stored {Provider} key at {Path} could not be decrypted — written by a different Windows user, restored from another machine, or corrupt. Treating it as absent.")]
    public static partial void ApiKeyUndecryptable(this ILogger logger, ScriptChatProvider provider, string path);

    [LoggerMessage(
        EventId = 2203,
        EventName = "ApiKeySaved",
        Level = LogLevel.Information,
        Message = "API key stored for {Provider} at {Path}. Length={ApiKeyLength}")]
    public static partial void ApiKeySaved(
        this ILogger logger,
        ScriptChatProvider provider,
        string path,
        int apiKeyLength);

    [LoggerMessage(
        EventId = 2204,
        EventName = "ApiKeyCleared",
        Level = LogLevel.Information,
        Message = "Stored API key removed for {Provider}. Existed={Existed}")]
    public static partial void ApiKeyCleared(this ILogger logger, ScriptChatProvider provider, bool existed);

    [LoggerMessage(
        EventId = 2205,
        EventName = "ApiKeyStoreFailed",
        Level = LogLevel.Error,
        Message = "The API key store failed while trying to {Operation} the {Provider} key.")]
    public static partial void ApiKeyStoreFailed(
        this ILogger logger,
        Exception exception,
        string operation,
        ScriptChatProvider provider);
}
