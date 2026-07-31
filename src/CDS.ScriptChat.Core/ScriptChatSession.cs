using System.ComponentModel;
using System.Text;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

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
    private readonly List<ChatMessage> _history = [];
    private readonly List<ChatTurn> _turns = [];
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
    /// The provider client, typically from <see cref="ScriptChatClientFactory.Create"/>. The
    /// session wraps it with function invocation; the caller keeps ownership and is responsible
    /// for disposing it.
    /// </param>
    /// <param name="options">Host-supplied configuration, or <see langword="null"/> for defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="chatClient"/> is <see langword="null"/>.</exception>
    public ScriptChatSession(IChatClient chatClient, ScriptChatSessionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        _options = options ?? new ScriptChatSessionOptions();
        _chatClient = chatClient.AsBuilder().UseFunctionInvocation().Build();

        _tools =
        [
            AIFunctionFactory.Create(LookupSymbolAsync, name: "lookup_symbol"),
            AIFunctionFactory.Create(ProposeScriptEdit, name: "propose_script_edit"),
        ];
    }

    /// <summary>
    /// Gets the transcript as the panel renders it, oldest first.
    /// </summary>
    public IReadOnlyList<ChatTurn> Turns => _turns;

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
            throw new InvalidOperationException(
                "A turn is already in flight on this session. Await the previous SendAsync before starting another.");
        }

        try
        {
            _capturedProposal = null;
            _symbolsLookedUp.Clear();

            if (_history.Count == 0)
            {
                _history.Add(new ChatMessage(ChatRole.System, BuildSystemPrompt()));
            }

            _history.Add(new ChatMessage(ChatRole.User, BuildUserTurn(userMessage, currentScript)));
            _turns.Add(new ChatTurn(ChatTurnRole.User, userMessage, null, null, EditDisposition.None));

            var chatOptions = new ChatOptions { Tools = _tools };
            var response = await _chatClient
                .GetResponseAsync(_history, chatOptions, cancellationToken)
                .ConfigureAwait(false);

            // Includes the assistant turn plus any tool call/result messages, so the next turn
            // sees the same history the provider did.
            _history.AddRange(response.Messages);

            var text = string.IsNullOrWhiteSpace(response.Text) ? null : response.Text.Trim();
            var proposal = _capturedProposal;

            _turns.Add(new ChatTurn(
                ChatTurnRole.Assistant,
                text,
                proposal?.ProposedCode,
                proposal?.Summary,
                proposal is null ? EditDisposition.None : EditDisposition.PendingReview));

            _options.Logger.LogInformation(
                "Assistant turn complete. ProposedEdit={ProposedEdit} SymbolLookups={SymbolLookups}",
                proposal is not null,
                _symbolsLookedUp.Count);

            return new AssistantTurnResult(text, proposal, [.. _symbolsLookedUp]);
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
    }

    /// <summary>
    /// Clears the conversation and the rendered transcript. Used when the provider or model
    /// changes mid-session (D10).
    /// </summary>
    public void Reset()
    {
        _history.Clear();
        _turns.Clear();
        _symbolsLookedUp.Clear();
        _capturedProposal = null;
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

        if (!string.IsNullOrWhiteSpace(_options.OrientationBlurb))
        {
            prompt.AppendLine();
            prompt.AppendLine("About this host application and its scripts:");
            prompt.AppendLine(_options.OrientationBlurb.Trim());
        }

        return prompt.ToString();
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

        _options.Logger.LogInformation(
            "lookup_symbol called. Symbol={SymbolName} ContainingType={ContainingType}",
            symbolName,
            containingType);

        var result = await _options.SymbolLookup
            .LookupAsync(symbolName, containingType, cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
        {
            return new LookupSymbolResponse(
                Found: false,
                Message: "No such symbol is reachable from this script's usings and referenced assemblies.");
        }

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
        _options.Logger.LogInformation("propose_script_edit called. ScriptLength={ScriptLength}", newScript.Length);

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
