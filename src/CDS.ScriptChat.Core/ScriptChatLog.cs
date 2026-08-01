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
/// <b>Level discipline (see D16).</b> Prompt text, response text, proposed scripts, edit
/// summaries, symbol signatures, and the orientation blurb are logged at
/// <see cref="LogLevel.Trace"/> and nowhere else. Every other level carries only structure —
/// names, lengths, counts, timings, and exceptions. That split is what makes the feature
/// shippable: a host that never enables <see cref="LogLevel.Trace"/> records no user content,
/// with no code change needed.
/// </para>
/// <para>
/// API keys are never logged at any level, not even at <see cref="LogLevel.Trace"/> (D3).
/// </para>
/// </remarks>
internal static partial class ScriptChatLog
{
    [LoggerMessage(
        EventId = 1000,
        EventName = "SessionCreated",
        Level = LogLevel.Information,
        Message = "Session created. Tools={ToolCount} ContentLogging={ContentLoggingEnabled}")]
    public static partial void SessionCreated(this ILogger logger, int toolCount, bool contentLoggingEnabled);

    [LoggerMessage(
        EventId = 1001,
        EventName = "SystemPromptBuilt",
        Level = LogLevel.Debug,
        Message = "System prompt built. Length={PromptLength} HasOrientation={HasOrientation}")]
    public static partial void SystemPromptBuilt(this ILogger logger, int promptLength, bool hasOrientation);

    [LoggerMessage(
        EventId = 1002,
        EventName = "SystemPromptContent",
        Level = LogLevel.Trace,
        Message = "System prompt content: {SystemPrompt}")]
    public static partial void SystemPromptContent(this ILogger logger, string systemPrompt);

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
        EventId = 1011,
        EventName = "TurnRequestContent",
        Level = LogLevel.Trace,
        Message = "Turn {TurnIndex} request content: {UserTurn}")]
    public static partial void TurnRequestContent(this ILogger logger, int turnIndex, string userTurn);

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
        EventId = 1013,
        EventName = "TurnResponseContent",
        Level = LogLevel.Trace,
        Message = "Turn {TurnIndex} response content: {ResponseText}")]
    public static partial void TurnResponseContent(this ILogger logger, int turnIndex, string? responseText);

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
        EventId = 1023,
        EventName = "SymbolLookupContent",
        Level = LogLevel.Trace,
        Message = "lookup_symbol returned. Symbol={SymbolName} Signature={Signature} Summary={XmlDocSummary}")]
    public static partial void SymbolLookupContent(
        this ILogger logger,
        string symbolName,
        string? signature,
        string? xmlDocSummary);

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
        EventId = 1031,
        EventName = "EditProposalContent",
        Level = LogLevel.Trace,
        Message = "Proposed edit. Summary={Summary} Script={ProposedScript}")]
    public static partial void EditProposalContent(this ILogger logger, string summary, string proposedScript);

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

    [LoggerMessage(
        EventId = 1203,
        EventName = "OrientationContent",
        Level = LogLevel.Trace,
        Message = "Orientation blurb content: {Blurb}")]
    public static partial void OrientationContent(this ILogger logger, string blurb);
}
