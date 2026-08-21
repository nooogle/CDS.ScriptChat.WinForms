using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Every log message the core library emits, as compile-time generated
/// <see cref="LoggerMessage"/> methods.
/// </summary>
/// <remarks>
/// <para>
/// Keeping them in one place fixes the event IDs and the message templates, so a log file can be
/// filtered by ID rather than by matching on prose. IDs are banded by area: 1000–1099 session,
/// 1100–1199 client factory, 1200–1299 orientation resolution. The WinForms assembly uses the
/// 2000 band.
/// </para>
/// <para>
/// <b>No content-bearing message exists here, at any level (D16, D17).</b> Prompt text, response
/// text, proposed scripts, edit summaries, symbol signatures, and the orientation blurb are never
/// logged — not at <see cref="LogLevel.Trace"/>, not anywhere. Earlier this library logged that
/// content at Trace, on the theory that a host which never enabled Trace was safe; D17 rejects
/// that theory, because Trace can be re-enabled by anything sharing the same logging pipeline —
/// a host misconfiguration, or another library reconfiguring the same provider — with no code
/// change on this library's part. There is now nothing left for that to re-enable: every message
/// here carries only structure — names, lengths, counts, timings, event IDs, and exceptions.
/// <see cref="ScriptChatSession"/> additionally wraps every <see cref="ILoggerFactory"/> it's
/// given in <see cref="TraceSuppressingLoggerFactory"/>, so <see cref="LogLevel.Trace"/> is
/// unreachable even for <c>Microsoft.Extensions.AI</c>'s own function-invocation and chat-client
/// logging, which is not defined in this file and not something this file's discipline alone
/// could constrain.
/// </para>
/// <para>
/// API keys are never logged at any level (D3).
/// </para>
/// </remarks>
internal static partial class ScriptChatLog
{
    [LoggerMessage(
        EventId = 1000,
        EventName = "SessionCreated",
        Level = LogLevel.Information,
        Message = "Session created. Tools={ToolCount}")]
    public static partial void SessionCreated(this ILogger logger, int toolCount);

    [LoggerMessage(
        EventId = 1001,
        EventName = "SystemPromptBuilt",
        Level = LogLevel.Debug,
        Message = "System prompt built. Length={PromptLength} HasOrientation={HasOrientation}")]
    public static partial void SystemPromptBuilt(this ILogger logger, int promptLength, bool hasOrientation);

    [LoggerMessage(
        EventId = 1010,
        EventName = "TurnStarted",
        Level = LogLevel.Information,
        Message = "Turn {TurnIndex} started. UserMessageLength={UserMessageLength} ScriptLength={ScriptLength} HistoryMessages={HistoryMessages}")]
    public static partial void TurnStarted(
        this ILogger logger,
        int turnIndex,
        int userMessageLength,
        int scriptLength,
        int historyMessages);

    [LoggerMessage(
        EventId = 1012,
        EventName = "TurnCompleted",
        Level = LogLevel.Information,
        Message = "Turn {TurnIndex} completed in {ElapsedMs}ms. ProposedEdit={ProposedEdit} SymbolLookups={SymbolLookups} ResponseMessages={ResponseMessages} FinishReason={FinishReason} InputTokens={InputTokens} OutputTokens={OutputTokens}")]
    public static partial void TurnCompleted(
        this ILogger logger,
        int turnIndex,
        long elapsedMs,
        bool proposedEdit,
        int symbolLookups,
        int responseMessages,
        string? finishReason,
        long? inputTokens,
        long? outputTokens);

    [LoggerMessage(
        EventId = 1014,
        EventName = "TurnFailed",
        Level = LogLevel.Error,
        Message = "Turn {TurnIndex} failed after {ElapsedMs}ms.")]
    public static partial void TurnFailed(this ILogger logger, Exception exception, int turnIndex, long elapsedMs);

    [LoggerMessage(
        EventId = 1015,
        EventName = "TurnCancelled",
        Level = LogLevel.Warning,
        Message = "Turn {TurnIndex} cancelled after {ElapsedMs}ms.")]
    public static partial void TurnCancelled(this ILogger logger, int turnIndex, long elapsedMs);

    [LoggerMessage(
        EventId = 1016,
        EventName = "TurnRejectedAsOverlapping",
        Level = LogLevel.Warning,
        Message = "A turn was requested while turn {TurnIndex} was still in flight, and was rejected.")]
    public static partial void TurnRejectedAsOverlapping(this ILogger logger, int turnIndex);

    [LoggerMessage(
        EventId = 1020,
        EventName = "SymbolLookupRequested",
        Level = LogLevel.Information,
        Message = "lookup_symbol requested. Symbol={SymbolName} ContainingType={ContainingType}")]
    public static partial void SymbolLookupRequested(this ILogger logger, string symbolName, string? containingType);

    [LoggerMessage(
        EventId = 1021,
        EventName = "SymbolLookupResolved",
        Level = LogLevel.Information,
        Message = "lookup_symbol resolved in {ElapsedMs}ms. Symbol={SymbolName} Namespace={Namespace} Overloads={OverloadCount}")]
    public static partial void SymbolLookupResolved(
        this ILogger logger,
        long elapsedMs,
        string symbolName,
        string? @namespace,
        int overloadCount);

    [LoggerMessage(
        EventId = 1022,
        EventName = "SymbolLookupNotFound",
        Level = LogLevel.Information,
        Message = "lookup_symbol found nothing in {ElapsedMs}ms. Symbol={SymbolName}")]
    public static partial void SymbolLookupNotFound(this ILogger logger, long elapsedMs, string symbolName);

    [LoggerMessage(
        EventId = 1030,
        EventName = "EditProposed",
        Level = LogLevel.Information,
        Message = "propose_script_edit called. ScriptLength={ScriptLength} SummaryLength={SummaryLength} ReplacesEarlierProposal={ReplacesEarlierProposal}")]
    public static partial void EditProposed(
        this ILogger logger,
        int scriptLength,
        int summaryLength,
        bool replacesEarlierProposal);

    [LoggerMessage(
        EventId = 1032,
        EventName = "EditDispositionRecorded",
        Level = LogLevel.Information,
        Message = "Turn {TurnIndex} edit disposition recorded as {Disposition}.")]
    public static partial void EditDispositionRecorded(
        this ILogger logger,
        int turnIndex,
        EditDisposition disposition);

    [LoggerMessage(
        EventId = 1033,
        EventName = "EditDispositionReconciliationMissed",
        Level = LogLevel.Warning,
        Message = "Turn {TurnIndex} proposed an edit but no matching proposal tool-result was found "
            + "to reconcile — the model's history still says the edit is undecided even though the "
            + "user just accepted or rejected it.")]
    public static partial void EditDispositionReconciliationMissed(this ILogger logger, int turnIndex);

    [LoggerMessage(
        EventId = 1034,
        EventName = "PatchProposed",
        Level = LogLevel.Information,
        Message = "propose_script_patch called. Hunks={HunkCount} SummaryLength={SummaryLength} ReplacesEarlierProposal={ReplacesEarlierProposal}")]
    public static partial void PatchProposed(
        this ILogger logger,
        int hunkCount,
        int summaryLength,
        bool replacesEarlierProposal);

    [LoggerMessage(
        EventId = 1035,
        EventName = "PatchProposalRejected",
        Level = LogLevel.Information,
        Message = "propose_script_patch call {HunkIndex} of {HunkCount} did not apply to the current script and was rejected.")]
    public static partial void PatchProposalRejected(this ILogger logger, int hunkIndex, int hunkCount);

    [LoggerMessage(
        EventId = 1040,
        EventName = "SessionReset",
        Level = LogLevel.Information,
        Message = "Session reset. TurnsDiscarded={TurnsDiscarded} MessagesDiscarded={MessagesDiscarded}")]
    public static partial void SessionReset(this ILogger logger, int turnsDiscarded, int messagesDiscarded);

    [LoggerMessage(
        EventId = 1100,
        EventName = "ClientCreating",
        Level = LogLevel.Information,
        Message = "Creating a chat client. Provider={Provider} Model={ModelId} MaxOutputTokens={MaxOutputTokens} ApiKeyLength={ApiKeyLength}")]
    public static partial void ClientCreating(
        this ILogger logger,
        ScriptChatProvider provider,
        string modelId,
        int maxOutputTokens,
        int apiKeyLength);

    [LoggerMessage(
        EventId = 1101,
        EventName = "ClientCreated",
        Level = LogLevel.Information,
        Message = "Chat client created. Provider={Provider} Model={ModelId}")]
    public static partial void ClientCreated(this ILogger logger, ScriptChatProvider provider, string modelId);

    [LoggerMessage(
        EventId = 1102,
        EventName = "ClientOptionsRejected",
        Level = LogLevel.Warning,
        Message = "Chat client options rejected: {Reason}")]
    public static partial void ClientOptionsRejected(this ILogger logger, string reason);

    [LoggerMessage(
        EventId = 1200,
        EventName = "OrientationResolvedFromFile",
        Level = LogLevel.Information,
        Message = "Orientation blurb read from {Path}. Length={BlurbLength}")]
    public static partial void OrientationResolvedFromFile(this ILogger logger, string path, int blurbLength);

    [LoggerMessage(
        EventId = 1201,
        EventName = "OrientationResolvedFromHostContext",
        Level = LogLevel.Information,
        Message = "Orientation blurb taken from the host context, no file at {Path}. Length={BlurbLength}")]
    public static partial void OrientationResolvedFromHostContext(this ILogger logger, string path, int blurbLength);

    [LoggerMessage(
        EventId = 1202,
        EventName = "OrientationNotResolved",
        Level = LogLevel.Warning,
        Message = "No orientation blurb: no file at {Path} and the host context supplied none. The model will work without host-specific orientation.")]
    public static partial void OrientationNotResolved(this ILogger logger, string path);
}
