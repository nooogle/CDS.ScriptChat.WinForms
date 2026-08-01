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

    private readonly List<string> _symbolsLookedUp = [];
    private readonly IList<AITool> _tools;

    /// <summary>Guards against overlapping turns; 1 while a turn is in flight.</summary>
    private int _turnInFlight;

    /// <summary>
    /// Set by the <c>propose_script_edit</c> tool during a turn, read once the turn completes.
    /// </summary>
    private ScriptEditProposal? _capturedProposal;

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
        _logger = _options.LoggerFactory?.CreateLogger(typeof(ScriptChatSession)) ?? NullLogger.Instance;

        // Function invocation is inside the logging client, so the log records every provider
        // round-trip a turn makes — the tool call and the follow-up — rather than collapsing
        // them into one entry. That distinction is the whole point of the log when a tool-using
        // turn goes wrong.
        var builder = chatClient.AsBuilder().UseFunctionInvocation(_options.LoggerFactory);

        // UseLogging resolves its factory with GetRequiredService when handed null, so it is
        // added only when there is one to give it. UseFunctionInvocation tolerates null.
        if (_options.LoggerFactory is not null)
        {
            builder = builder.UseLogging(_options.LoggerFactory);
        }

        _chatClient = builder.Build();

        _tools =
        [
            AIFunctionFactory.Create(LookupSymbolAsync, name: "lookup_symbol"),
            AIFunctionFactory.Create(ProposeScriptEdit, name: "propose_script_edit"),
        ];

        _logger.SessionCreated(_tools.Count, _logger.IsEnabled(LogLevel.Trace));
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
            _symbolsLookedUp.Clear();

            if (_history.Count == 0)
            {
                _history.Add(new ChatMessage(ChatRole.System, BuildSystemPrompt()));
            }

            var userTurn = BuildUserTurn(userMessage, currentScript);

            _logger.TurnStarted(turnIndex, userMessage.Length, currentScript.Length, _history.Count);
            _logger.TurnRequestContent(turnIndex, userTurn);

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

            AddTurn(
                new ChatTurn(
                    ChatTurnRole.Assistant,
                    text,
                    proposal?.ProposedCode,
                    proposal?.Summary,
                    proposal is null ? EditDisposition.None : EditDisposition.PendingReview),
                currentScript);

            _logger.TurnCompleted(
                turnIndex,
                stopwatch.ElapsedMilliseconds,
                proposal is not null,
                _symbolsLookedUp.Count,
                response.Messages.Count,
                response.FinishReason?.Value,
                response.Usage?.InputTokenCount,
                response.Usage?.OutputTokenCount);
            _logger.TurnResponseContent(turnIndex, text);

            return new AssistantTurnResult(text, proposal, [.. _symbolsLookedUp]);
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

        var turn = _turns[turnIndex];
        if (!turn.HasProposedEdit)
        {
            throw new InvalidOperationException($"Turn {turnIndex} proposed no edit.");
        }

        _turns[turnIndex] = turn with { Disposition = disposition };
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
        _turns.Clear();
        _turnBaselines.Clear();
        _symbolsLookedUp.Clear();
        _capturedProposal = null;
    }

    /// <summary>Appends a turn and the script it was sent against, keeping the two in step.</summary>
    private void AddTurn(ChatTurn turn, string baselineScript)
    {
        _turns.Add(turn);
        _turnBaselines.Add(baselineScript);
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
            - To change the script, call the propose_script_edit tool with the complete new
              script. Never write the revised script into your prose reply, and never wrap it in
              a markdown code fence expecting it to be applied — only a tool call reaches the
              editor, and only after the user accepts it.
            - If the user asks a question that implies no code change, just answer. Do not call
              propose_script_edit.
            - Before relying on any API detail you are not certain of, call lookup_symbol. It
              is answered by this host application itself, so it is accurate where recall may
              not be. A "not found" answer means the symbol is not available here.
            - Keep prose brief and focused on what changed and why.
            """);

        var hasOrientation = !string.IsNullOrWhiteSpace(_options.OrientationBlurb);
        if (hasOrientation)
        {
            prompt.AppendLine();
            prompt.AppendLine("About this host application and its scripts:");
            prompt.AppendLine(_options.OrientationBlurb!.Trim());
        }

        var result = prompt.ToString();

        _logger.SystemPromptBuilt(result.Length, hasOrientation);
        _logger.SystemPromptContent(result);

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
        _logger.SymbolLookupContent(symbolName, result.Signature, result.XmlDocSummary);

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
        _logger.EditProposalContent(summary, newScript);

        // Last call wins if the model proposes twice; the prompt asks for at most one.
        _capturedProposal = new ScriptEditProposal(newScript, summary);

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
