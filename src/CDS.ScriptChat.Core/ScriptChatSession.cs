using System.ComponentModel;
using System.Diagnostics;
using System.Text;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDS.ScriptChat.Core;

/// <summary>
/// Holds conversation state for one script, sends turns, and interprets the results.
/// Provider-agnostic: it consumes <see cref="IChatClient"/> and never names a provider.
/// </summary>
/// <remarks>
/// <para>
/// A session is single-conversation and not safe for concurrent turns — overlapping calls to
/// <see cref="SendAsync"/> throw rather than interleaving, because a turn's tool calls are
/// captured into per-turn state.
/// </para>
/// <para>
/// Switching provider or model means constructing a new client and calling <see cref="Reset"/>;
/// there is no cross-provider history carryover in v1 (D10).
/// </para>
/// </remarks>
public sealed class ScriptChatSession
{
    private readonly IChatClient _chatClient;
    private readonly ScriptChatSessionOptions _options;
    private readonly ILogger _logger;
    private readonly List<ChatMessage> _history = [];
    private readonly List<ChatTurn> _turns = [];

    /// <summary>
    /// The script as it stood when each turn was sent, parallel to <see cref="_turns"/>. Kept
    /// here rather than on <see cref="ChatTurn"/> so a proposal can still be rendered as a diff
    /// after the transcript is reloaded into a fresh panel.
    /// </summary>
    private readonly List<string> _turnBaselines = [];

    /// <summary>
    /// The <c>propose_script_edit</c> tool-result content for each turn, parallel to
    /// <see cref="_turns"/>; <see langword="null"/> for turns that proposed no edit. Kept so
    /// <see cref="SetEditDisposition"/> can update the frozen "not applied yet" result text once
    /// the user actually accepts or rejects — otherwise a later turn's history tells the model
    /// its edit is still pending even after the user has decided (UC2).
    /// </summary>
    private readonly List<FunctionResultContent?> _turnProposalResults = [];

    /// <summary>
    /// Guards <see cref="_turns"/>, <see cref="_turnBaselines"/>, and <see cref="_turnProposalResults"/>.
    /// <see cref="SendAsync"/> awaits the provider with <c>ConfigureAwait(false)</c>, so
    /// <see cref="AddTurn"/> can resume on a thread-pool thread while <see cref="SetEditDisposition"/>
    /// runs concurrently on the host's UI thread for an earlier, still-pending turn; without this,
    /// that's a genuine <see cref="List{T}"/> torn-read/corruption risk, not just a logical race.
    /// </summary>
    private readonly Lock _turnsLock = new();

    private readonly List<string> _symbolsLookedUp = [];
    private readonly IList<AITool> _tools;

    /// <summary>Guards against overlapping turns; 1 while a turn is in flight.</summary>
    private int _turnInFlight;

    /// <summary>
    /// Set by the <c>propose_script_edit</c> tool during a turn, read once the turn completes.
    /// </summary>
    private ScriptEditProposal? _capturedProposal;

    /// <summary>
    /// The <c>CallId</c> of the call that produced
    /// <see cref="_capturedProposal"/>, captured at the moment of the call via
    /// <see cref="FunctionInvokingChatClient.CurrentContext"/> rather than re-derived afterwards
    /// by scanning <c>response.Messages</c> for the tool name — one source of truth for "which
    /// call won" instead of two that could drift apart.
    /// </summary>
    private string? _capturedProposalCallId;

    /// <summary>
    /// Set by the <c>propose_script_patch</c> tool during a turn, read once the turn completes.
    /// Parallel to <see cref="_capturedProposal"/> — a turn proposes at most one kind of edit,
    /// but which tool it called is not known until the response comes back, so both are tracked
    /// independently rather than through one shared field.
    /// </summary>
    private ScriptPatchProposal? _capturedPatchProposal;

    /// <summary>The <c>CallId</c> of the call that produced <see cref="_capturedPatchProposal"/>.</summary>
    private string? _capturedPatchProposalCallId;

    /// <summary>
    /// The script <see cref="SendAsync"/> was called with for the turn in flight, so
    /// <see cref="ProposeScriptPatch"/> can validate a hunk against what the model was actually
    /// shown, without needing the caller to thread it through as a tool argument.
    /// </summary>
    private string _currentScript = string.Empty;

    /// <summary>
    /// Initialises a session over an existing chat client.
    /// </summary>
    /// <param name="chatClient">
    /// The provider client, typically from <see cref="ScriptChatClientFactory.Create(ScriptChatClientOptions)"/>.
    /// The session wraps it with function invocation and logging; the caller keeps ownership and
    /// is responsible for disposing it.
    /// </param>
    /// <param name="options">Host-supplied configuration, or <see langword="null"/> for defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="chatClient"/> is <see langword="null"/>.</exception>
    public ScriptChatSession(IChatClient chatClient, ScriptChatSessionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        _options = options ?? new ScriptChatSessionOptions();

        // Wrapped once, here, and used for everything below — the one chokepoint every logger
        // this session or its dependencies see passes through. No caller-visible logging exists
        // above Trace in this library any more, but Microsoft.Extensions.AI's own
        // FunctionInvokingChatClient logs full function arguments and results at Trace (the
        // entire proposed script, for propose_script_edit) — this wrapper makes that
        // unreachable regardless of how the host's own logging pipeline is configured (D17).
        var loggerFactory = _options.LoggerFactory is null
            ? null
            : new TraceSuppressingLoggerFactory(_options.LoggerFactory);

        _logger = loggerFactory?.CreateLogger(typeof(ScriptChatSession)) ?? NullLogger.Instance;

        // Function invocation is inside the logging client, so the log records every provider
        // round-trip a turn makes — the tool call and the follow-up — rather than collapsing
        // them into one entry. That distinction is the whole point of the log when a tool-using
        // turn goes wrong. UseLogging is deliberately not added: Microsoft.Extensions.AI's
        // LoggingChatClient only ever logs at Trace, so under the wrapper above it would never
        // write anything — a permanently inert pipeline stage, not worth carrying.
        _chatClient = chatClient.AsBuilder().UseFunctionInvocation(loggerFactory).Build();

        // lookup_symbol is offered only when a provider can actually answer it. Advertised
        // against NullSymbolLookupProvider it answers "not found" to everything, which reads to
        // a model as "this host's API does not exist" — worse than having no lookup at all.
        _tools = _options.SymbolLookup is NullSymbolLookupProvider
            ?
            [
                AIFunctionFactory.Create(ProposeScriptEdit, name: "propose_script_edit"),
                AIFunctionFactory.Create(ProposeScriptPatch, name: "propose_script_patch"),
            ]
            :
            [
                AIFunctionFactory.Create(LookupSymbolAsync, name: "lookup_symbol"),
                AIFunctionFactory.Create(ProposeScriptEdit, name: "propose_script_edit"),
                AIFunctionFactory.Create(ProposeScriptPatch, name: "propose_script_patch"),
            ];

        _logger.SessionCreated(_tools.Count);
    }

    /// <summary>
    /// Gets the transcript as the panel renders it, oldest first.
    /// </summary>
    public IReadOnlyList<ChatTurn> Turns => _turns;

    /// <summary>
    /// Gets the script as it stood when a turn was sent, so a proposed edit can be shown as a
    /// diff against what the model actually saw.
    /// </summary>
    /// <param name="turnIndex">Index into <see cref="Turns"/>.</param>
    /// <returns>The script that accompanied that turn.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="turnIndex"/> is out of range.</exception>
    public string GetScriptBaseline(int turnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(turnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(turnIndex, _turnBaselines.Count);

        return _turnBaselines[turnIndex];
    }

    /// <summary>
    /// Sends a user turn and returns what the assistant produced.
    /// </summary>
    /// <param name="userMessage">What the user typed.</param>
    /// <param name="currentScript">
    /// The live contents of the editor buffer. Supplied per turn rather than once at
    /// construction, so the model always sees the script as it stands now.
    /// </param>
    /// <param name="cancellationToken">Cancels the turn.</param>
    /// <returns>The assistant's prose, any proposed edit, and the symbols it looked up.</returns>
    /// <exception cref="ArgumentException"><paramref name="userMessage"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="currentScript"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A turn is already in flight on this session.</exception>
    public async Task<AssistantTurnResult> SendAsync(
        string userMessage,
        string currentScript,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        ArgumentNullException.ThrowIfNull(currentScript);

        if (Interlocked.Exchange(ref _turnInFlight, 1) == 1)
        {
            _logger.TurnRejectedAsOverlapping(_turns.Count);
            throw new InvalidOperationException(
                "A turn is already in flight on this session. Await the previous SendAsync before starting another.");
        }

        var turnIndex = _turns.Count;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _capturedProposal = null;
            _capturedProposalCallId = null;
            _capturedPatchProposal = null;
            _capturedPatchProposalCallId = null;
            _currentScript = currentScript;
            _symbolsLookedUp.Clear();

            if (_history.Count == 0)
            {
                _history.Add(new ChatMessage(ChatRole.System, BuildSystemPrompt()));
            }

            var userTurn = BuildUserTurn(userMessage, currentScript);

            _logger.TurnStarted(turnIndex, userMessage.Length, currentScript.Length, _history.Count);

            _history.Add(new ChatMessage(ChatRole.User, userTurn));
            AddTurn(new ChatTurn(ChatTurnRole.User, userMessage, null, null, EditDisposition.None), currentScript);

            var chatOptions = new ChatOptions { Tools = _tools };
            var response = await _chatClient
                .GetResponseAsync(_history, chatOptions, cancellationToken)
                .ConfigureAwait(false);

            // Includes the assistant turn plus any tool call/result messages, so the next turn
            // sees the same history the provider did.
            _history.AddRange(response.Messages);

            var text = string.IsNullOrWhiteSpace(response.Text) ? null : response.Text.Trim();
            var proposal = _capturedProposal;

            // The system prompt asks for at most one proposal tool per turn; if the model calls
            // both anyway, the full-script replacement wins deterministically rather than either
            // silently combining or picking whichever happened to run last.
            var patchProposal = proposal is null ? _capturedPatchProposal : null;

            var proposalCallId = proposal is not null ? _capturedProposalCallId
                : patchProposal is not null ? _capturedPatchProposalCallId
                : null;
            var proposalResultContent = proposalCallId is null
                ? null
                : FindProposalResultContent(response.Messages, proposalCallId);

            var hasProposal = proposal is not null || patchProposal is not null;

            AddTurn(
                new ChatTurn(
                    ChatTurnRole.Assistant,
                    text,
                    proposal?.ProposedCode,
                    proposal?.Summary ?? patchProposal?.Summary,
                    hasProposal ? EditDisposition.PendingReview : EditDisposition.None,
                    patchProposal?.Hunks),
                currentScript,
                proposalResultContent);

            _logger.TurnCompleted(
                turnIndex,
                stopwatch.ElapsedMilliseconds,
                hasProposal,
                _symbolsLookedUp.Count,
                response.Messages.Count,
                response.FinishReason?.Value,
                response.Usage?.InputTokenCount,
                response.Usage?.OutputTokenCount);

            return new AssistantTurnResult(text, proposal, [.. _symbolsLookedUp], patchProposal);
        }
        catch (OperationCanceledException)
        {
            // Logged rather than swallowed: a cancelled turn and a failed one look identical in
            // the transcript, so the log is the only place the difference survives.
            _logger.TurnCancelled(turnIndex, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            // Same reasoning, plus the provider's own exception detail — which the panel reduces
            // to a one-line message — is only ever recorded here.
            _logger.TurnFailed(ex, turnIndex, stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            Interlocked.Exchange(ref _turnInFlight, 0);
        }
    }

    /// <summary>
    /// Records the user's decision on the edit proposed by a turn.
    /// </summary>
    /// <param name="turnIndex">Index into <see cref="Turns"/>.</param>
    /// <param name="disposition">
    /// <see cref="EditDisposition.Accepted"/> or <see cref="EditDisposition.Rejected"/>.
    /// </param>
    /// <remarks>
    /// Also rewrites that turn's <c>propose_script_edit</c> tool-result content in
    /// <see cref="_history"/> from "not applied yet" to what actually happened, so a later turn
    /// doesn't send the model a history where it still believes the edit is undecided (UC2).
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="turnIndex"/> is out of range.</exception>
    /// <exception cref="ArgumentException"><paramref name="disposition"/> is not a user decision.</exception>
    /// <exception cref="InvalidOperationException">That turn proposed no edit.</exception>
    public void SetEditDisposition(int turnIndex, EditDisposition disposition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(turnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(turnIndex, _turns.Count);

        if (disposition is not (EditDisposition.Accepted or EditDisposition.Rejected))
        {
            throw new ArgumentException(
                "Only Accepted or Rejected can be recorded as a user decision.",
                nameof(disposition));
        }

        lock (_turnsLock)
        {
            var turn = _turns[turnIndex];
            if (!turn.HasProposedEdit)
            {
                throw new InvalidOperationException($"Turn {turnIndex} proposed no edit.");
            }

            _turns[turnIndex] = turn with { Disposition = disposition };

            var resultContent = _turnProposalResults[turnIndex];
            if (resultContent is not null)
            {
                resultContent.Result = disposition == EditDisposition.Accepted
                    ? "The user accepted this edit. The script now reflects it."
                    : "The user rejected this edit. The script is unchanged from before this proposal.";
            }
            else
            {
                // Should be unreachable — HasProposedEdit is true, so FindProposalResultContent
                // should have matched something when the turn was added. Logged rather than
                // silently accepted: without this, the turn would show as Accepted/Rejected in
                // the UI while the model's own history still says the edit is undecided, and
                // nothing would explain why (this is the exact UC2 bug this method exists to fix).
                _logger.EditDispositionReconciliationMissed(turnIndex);
            }
        }

        _logger.EditDispositionRecorded(turnIndex, disposition);
    }

    /// <summary>
    /// Clears the conversation and the rendered transcript. Used when the provider or model
    /// changes mid-session (D10).
    /// </summary>
    public void Reset()
    {
        _logger.SessionReset(_turns.Count, _history.Count);

        _history.Clear();

        lock (_turnsLock)
        {
            _turns.Clear();
            _turnBaselines.Clear();
            _turnProposalResults.Clear();
        }

        _symbolsLookedUp.Clear();
        _capturedProposal = null;
        _capturedProposalCallId = null;
        _capturedPatchProposal = null;
        _capturedPatchProposalCallId = null;
        _currentScript = string.Empty;
    }

    /// <summary>Appends a turn and the script it was sent against, keeping the two in step.</summary>
    private void AddTurn(ChatTurn turn, string baselineScript, FunctionResultContent? proposalResultContent = null)
    {
        lock (_turnsLock)
        {
            _turns.Add(turn);
            _turnBaselines.Add(baselineScript);
            _turnProposalResults.Add(proposalResultContent);
        }
    }

    /// <summary>
    /// Finds the tool-result content matching <paramref name="callId"/>, so
    /// <see cref="SetEditDisposition"/> can rewrite it later.
    /// </summary>
    /// <param name="messages">The turn's response messages, including any tool call/result pairs.</param>
    /// <param name="callId">
    /// The <c>CallId</c> captured at the moment <c>propose_script_edit</c>
    /// was called, or <see langword="null"/> if that capture failed for some reason — in which case
    /// this returns <see langword="null"/> too rather than guessing.
    /// </param>
    private static FunctionResultContent? FindProposalResultContent(IEnumerable<ChatMessage> messages, string? callId)
    {
        if (callId is null)
        {
            return null;
        }

        return messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .FirstOrDefault(result => result.CallId == callId);
    }

    private string BuildSystemPrompt()
    {
        var prompt = new StringBuilder();

        prompt.AppendLine(
            """
            You are an assistant embedded in a script editor. The user is working on a single
            script, shown to you with every message. Answer questions about it and propose
            changes to it.

            Rules:
            - To change the script, call one of propose_script_edit or propose_script_patch — at
              most one, at most once per turn. Never write the revised script into your prose
              reply, and never wrap it in a markdown code fence expecting it to be applied — only
              a tool call reaches the editor, and only after the user accepts it.
            - Prefer propose_script_patch for a small, localised change: each hunk's oldText must
              match the current script exactly, including whitespace, and appear only once —
              include enough surrounding context to make it unique. If a hunk does not apply, the
              tool tells you why; re-read the script and try again rather than guessing.
            - Use propose_script_edit instead for a large rewrite, where most of the script is
              changing anyway.
            - If the user asks a question that implies no code change, just answer. Do not call
              either proposal tool.
            """);

        // Only worth telling the model about a tool it has actually been given. Unconditional,
        // this rule points it at a lookup that may not exist.
        if (_tools.Any(tool => tool.Name == "lookup_symbol"))
        {
            prompt.AppendLine(
                """
                - Before relying on any API detail you are not certain of, call lookup_symbol. It
                  is answered by this host application itself, so it is accurate where recall may
                  not be. A "not found" answer means the symbol is not available here.
                """);
        }

        prompt.AppendLine("- Keep prose brief and focused on what changed and why.");

        var hasOrientation = !string.IsNullOrWhiteSpace(_options.OrientationBlurb);
        if (hasOrientation)
        {
            prompt.AppendLine();
            prompt.AppendLine("About this host application and its scripts:");
            prompt.AppendLine(_options.OrientationBlurb!.Trim());
        }

        var result = prompt.ToString();

        _logger.SystemPromptBuilt(result.Length, hasOrientation);

        return result;
    }

    private static string BuildUserTurn(string userMessage, string currentScript)
    {
        return $"""
            The script currently open in the editor:

            ```csharp
            {currentScript}
            ```

            {userMessage}
            """;
    }

    [Description(
        "Look up the real signature and documentation of a symbol available to this script. "
        + "Use this before relying on any API detail you are not certain of.")]
    private async Task<LookupSymbolResponse> LookupSymbolAsync(
        [Description("The symbol to resolve, e.g. FindContours or ImagePanel.")]
        string symbolName,
        [Description("The type that declares it, when known, e.g. Cv2 for Cv2.FindContours.")]
        string? containingType = null,
        CancellationToken cancellationToken = default)
    {
        _symbolsLookedUp.Add(containingType is null ? symbolName : $"{containingType}.{symbolName}");
        _logger.SymbolLookupRequested(symbolName, containingType);

        // A provider that throws is logged by the function-invocation client, so there is no
        // try/catch here — only the timing of a successful lookup needs measuring.
        var stopwatch = Stopwatch.StartNew();
        var result = await _options.SymbolLookup
            .LookupAsync(symbolName, containingType, cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
        {
            _logger.SymbolLookupNotFound(stopwatch.ElapsedMilliseconds, symbolName);

            return new LookupSymbolResponse(
                Found: false,
                Message: "No such symbol is reachable from this script's usings and referenced assemblies.");
        }

        _logger.SymbolLookupResolved(
            stopwatch.ElapsedMilliseconds,
            symbolName,
            result.Namespace,
            result.Overloads.Count);

        return new LookupSymbolResponse(
            Found: true,
            Signature: result.Signature,
            Namespace: result.Namespace,
            XmlDocSummary: result.XmlDocSummary,
            Overloads: result.Overloads);
    }

    [Description(
        "Propose a replacement for the entire script. The user sees it as a diff and must "
        + "accept it before the editor changes. Call this once per turn, at most.")]
    private string ProposeScriptEdit(
        [Description("The complete new script, not a fragment or a diff.")]
        string newScript,
        [Description("A one-line summary of what this edit changes.")]
        string summary)
    {
        _logger.EditProposed(newScript.Length, summary.Length, _capturedProposal is not null);

        // Last call wins if the model proposes twice; the prompt asks for at most one. Capturing
        // CallId here — the one place that genuinely knows it — means FindProposalResultContent
        // only ever has to look up a known ID, rather than independently re-deriving "which call
        // was last" by re-scanning response.Messages after the fact.
        _capturedProposal = new ScriptEditProposal(newScript, summary);
        _capturedProposalCallId = FunctionInvokingChatClient.CurrentContext?.CallContent.CallId;

        return "Proposal recorded and shown to the user as a diff. It is not applied until they accept it.";
    }

    [Description(
        "Propose one or more targeted find-and-replace changes to the script, instead of "
        + "rewriting the whole thing. The user sees every hunk as a diff and must accept it "
        + "before the editor changes. Call this at most once per turn.")]
    private string ProposeScriptPatch(
        [Description("The hunks to apply, in order. Each is an exact old-text/new-text pair.")]
        IReadOnlyList<ScriptEditHunk> hunks,
        [Description("A one-line summary of what this change does.")]
        string summary)
    {
        if (hunks.Count == 0)
        {
            return "No hunks were supplied. Call this tool with at least one hunk, "
                + "or use propose_script_edit for a full rewrite.";
        }

        try
        {
            // Discarded — this call only validates that every hunk applies to the script the
            // model was actually shown. The buffer is re-read and patched again at accept time,
            // since it may have changed in the meantime (Job 3).
            _ = ScriptPatchApplier.Apply(_currentScript, hunks);
        }
        catch (ScriptPatchApplyException ex)
        {
            _logger.PatchProposalRejected(ex.HunkIndex + 1, ex.HunkCount);
            return $"This patch could not be applied: {ex.Message} Re-read the script and try again with corrected hunks.";
        }

        _logger.PatchProposed(hunks.Count, summary.Length, _capturedPatchProposal is not null);

        _capturedPatchProposal = new ScriptPatchProposal(hunks, summary);
        _capturedPatchProposalCallId = FunctionInvokingChatClient.CurrentContext?.CallContent.CallId;

        return "Proposal recorded and shown to the user as a diff. It is not applied until they accept it.";
    }

    /// <summary>Shape returned to the model by the <c>lookup_symbol</c> tool.</summary>
    private sealed record LookupSymbolResponse(
        bool Found,
        string? Signature = null,
        string? Namespace = null,
        string? XmlDocSummary = null,
        IReadOnlyList<string>? Overloads = null,
        string? Message = null);
}
