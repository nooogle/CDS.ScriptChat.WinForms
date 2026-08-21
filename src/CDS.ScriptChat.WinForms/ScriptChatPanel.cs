using System.ComponentModel;
using System.Diagnostics;

using CDS.ScriptChat.Core;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDS.ScriptChat.WinForms;

/// <summary>
/// The script+chat panel: one scrolling transcript of user and assistant turns, an input box,
/// and a send button (D6). Proposed edits render inline as a diff with Accept and Reject.
/// </summary>
/// <remarks>
/// The panel never touches an editor control directly. It reads the script through
/// <see cref="ScriptTextProvider"/> and writes an accepted edit through
/// <see cref="ScriptTextSetter"/>, so the host can back those with Scintilla, a plain
/// <see cref="TextBox"/>, or anything else (D15).
/// </remarks>
public partial class ScriptChatPanel : UserControl
{
    private static readonly Color s_addedBackColour = Color.FromArgb(223, 245, 226);
    private static readonly Color s_removedBackColour = Color.FromArgb(255, 226, 226);

    private ScriptChatSession? _session;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = NullLogger.Instance;
    private bool _turnInFlight;

    /// <summary>
    /// Index into <see cref="ScriptChatSession.Turns"/> of the one proposal the decision bar
    /// currently acts on, or <see langword="null"/> when none is awaiting review.
    /// </summary>
    /// <remarks>
    /// Only one proposal can be pending at a time — <see cref="UpdateEnabledState"/> disables
    /// sending a new turn while this is set, so a proposal can never be silently superseded or
    /// buried by later chat before the user decides on it (D5).
    /// </remarks>
    private int? _pendingTurnIndex;

    /// <summary>
    /// The chat client created by <see cref="Configure"/>, which this panel owns and must
    /// dispose. Null when the session came from <see cref="AttachSession"/>, where the caller
    /// keeps ownership.
    /// </summary>
    private IChatClient? _ownedChatClient;

    /// <summary>Initialises a new instance of the <see cref="ScriptChatPanel"/> class.</summary>
    public ScriptChatPanel()
    {
        InitializeComponent();
        UpdateEnabledState();
    }

    /// <summary>
    /// Raised after an accepted edit has been handed to <see cref="ScriptTextSetter"/>, so the
    /// host can react — refocus the editor, mark the document dirty, and so on.
    /// </summary>
    public event EventHandler<ScriptEditAcceptedEventArgs>? EditAccepted;

    /// <summary>
    /// Gets or sets the callback that returns the script currently open in the host's editor.
    /// Called once per turn, so the model always sees the live buffer.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string>? ScriptTextProvider { get; set; }

    /// <summary>
    /// Gets or sets the callback that replaces the script in the host's editor. Invoked only
    /// when the user accepts a proposed edit — never automatically (D5).
    /// </summary>
    /// <remarks>
    /// Leaving this unset makes proposals review-only: the diff still renders, but Accept
    /// reports that no editor is wired up rather than silently doing nothing.
    /// </remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<string>? ScriptTextSetter { get; set; }

    /// <summary>
    /// Gets or sets the factory the panel logs through. <see langword="null"/> — the default —
    /// disables logging.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Also used for the sessions and clients <see cref="Configure"/> builds, so setting this
    /// one property instruments the whole chain: the panel, the conversation, function
    /// invocation, and the provider round-trips, each under its own log category.
    /// </para>
    /// <para>
    /// No prompt, script, response, or API key content is ever logged, at any level (D3, D16,
    /// D17) — this isn't a level a host could accidentally enable, the capability doesn't exist.
    /// </para>
    /// </remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ILoggerFactory? LoggerFactory
    {
        get => _loggerFactory;
        set
        {
            _loggerFactory = value;
            _logger = value?.CreateLogger(typeof(ScriptChatPanel)) ?? NullLogger.Instance;
        }
    }

    /// <summary>Gets a value indicating whether the panel is ready to send a turn.</summary>
    [Browsable(false)]
    public bool IsReady => _session is not null && ScriptTextProvider is not null;

    /// <summary>
    /// Builds a client and a fresh session from a provider configuration, and attaches it.
    /// This is what the settings panel's applied configuration feeds into, and it is how a
    /// provider or model change takes effect without restarting the host app.
    /// </summary>
    /// <param name="clientOptions">The provider, key, and model to use.</param>
    /// <param name="sessionOptions">
    /// Symbol lookup and orientation blurb for the new session. Pass <see langword="null"/> for
    /// defaults.
    /// </param>
    /// <remarks>
    /// <para>
    /// The conversation starts fresh, because history is not carried across providers (D10).
    /// A configuration that cannot produce a client leaves the panel unavailable with the
    /// reason shown, rather than throwing at the caller.
    /// </para>
    /// <para>
    /// The session inherits this panel's <see cref="LoggerFactory"/> unless
    /// <paramref name="sessionOptions"/> names one of its own, so a host that has set the
    /// panel's factory gets the conversation instrumented without wiring it twice.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="clientOptions"/> is <see langword="null"/>.</exception>
    public void Configure(ScriptChatClientOptions clientOptions, ScriptChatSessionOptions? sessionOptions = null)
    {
        ArgumentNullException.ThrowIfNull(clientOptions);

        var options = sessionOptions ?? new ScriptChatSessionOptions();
        options = options with { LoggerFactory = options.LoggerFactory ?? _loggerFactory };

        IChatClient client;
        try
        {
            client = ScriptChatClientFactory.Create(clientOptions, options.LoggerFactory);
        }
        catch (Exception ex)
        {
            _logger.PanelConfigurationFailed(ex, clientOptions.Provider);
            SetUnavailable(ex.Message);
            return;
        }

        AttachSession(new ScriptChatSession(client, options));

        // Only replace the owned client once the new one is in use, so a failure above leaves
        // the previous configuration working.
        ReplaceOwnedClient(client);
        SetStatus($"Ready · {clientOptions.Provider} · {clientOptions.ModelId}");
        _logger.PanelConfigured(clientOptions.Provider, clientOptions.ModelId);
    }

    /// <summary>
    /// Attaches the conversation this panel drives, clearing whatever was shown before.
    /// </summary>
    /// <param name="session">
    /// The session to drive, or <see langword="null"/> to detach — which is how the panel is
    /// left when no API key is configured.
    /// </param>
    /// <remarks>
    /// The caller keeps ownership of the session's chat client. Use <see cref="Configure"/> to
    /// have the panel build and own one instead.
    /// </remarks>
    public void AttachSession(ScriptChatSession? session)
    {
        _session = session;
        ClearTranscript();

        if (session is null)
        {
            SetStatus("Not configured.");
            _logger.SessionDetached();
        }
        else
        {
            SetStatus(ScriptTextProvider is null ? "No script source configured." : "Ready.");

            for (var i = 0; i < session.Turns.Count; i++)
            {
                AppendTurnToTranscript(session.Turns[i], session.GetScriptBaseline(i));

                if (session.Turns[i].Disposition == EditDisposition.PendingReview)
                {
                    // A session can arrive with a proposal from before this panel attached to
                    // it; if more than one is somehow still pending, the most recent is the one
                    // the decision bar acts on — the rest are stale by definition, since only
                    // one turn can be pending going forward.
                    _pendingTurnIndex = i;
                }
            }

            _logger.SessionAttached(
                session.Turns.Count,
                ScriptTextProvider is not null,
                ScriptTextSetter is not null);
        }

        UpdateEnabledState();
    }

    /// <summary>
    /// Shows the panel as unavailable — used when no API key is configured, so the feature
    /// reads as switched off rather than broken.
    /// </summary>
    /// <param name="reason">A short explanation to show the user.</param>
    public void SetUnavailable(string reason)
    {
        AttachSession(null);
        SetStatus(reason);
        _logger.PanelUnavailable(reason);
    }

    /// <summary>
    /// Disposes the previously owned chat client, if any, and takes ownership of the new one.
    /// </summary>
    private void ReplaceOwnedClient(IChatClient? client)
    {
        var previous = _ownedChatClient;
        _ownedChatClient = client;
        previous?.Dispose();
    }

    /// <summary>Removes every turn from the transcript.</summary>
    public void ClearTranscript()
    {
        _transcriptTextBox.SetMarkdown(null);
        _pendingTurnIndex = null;
        UpdateEnabledState();
    }

    private async void OnSendButtonClick(object? sender, EventArgs e)
    {
        await SendCurrentInputAsync().ConfigureAwait(true);
    }

    private async void OnInputTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        // Enter sends; Shift+Enter or Ctrl+Enter inserts a newline instead.
        if (e.KeyCode != Keys.Enter || e.Shift || e.Control)
        {
            return;
        }

        e.SuppressKeyPress = true;
        e.Handled = true;
        await SendCurrentInputAsync().ConfigureAwait(true);
    }

    private async Task SendCurrentInputAsync()
    {
        if (_turnInFlight || _session is null || ScriptTextProvider is null || _pendingTurnIndex is not null)
        {
            _logger.SendIgnored(
                _turnInFlight ? "a turn is already in flight"
                : _session is null ? "no session is attached"
                : _pendingTurnIndex is not null ? "a proposed edit is still awaiting a decision"
                : "no ScriptTextProvider is configured");
            return;
        }

        var userMessage = _inputTextBox.Text.Trim();
        if (userMessage.Length == 0)
        {
            _logger.SendIgnored("the input box is empty");
            return;
        }

        _turnInFlight = true;
        _inputTextBox.Clear();
        UpdateEnabledState();
        SetStatus("Thinking…");

        // Captured before the call so the diff is against what the model actually saw, even if
        // the user edits the buffer while the turn is in flight.
        var script = ScriptTextProvider() ?? string.Empty;
        var stopwatch = Stopwatch.StartNew();

        _logger.SendRequested(userMessage.Length, script.Length);

        try
        {
            AppendTurnToTranscript(
                new ChatTurn(ChatTurnRole.User, userMessage, null, null, EditDisposition.None),
                baselineScript: null);

            var result = await _session.SendAsync(userMessage, script).ConfigureAwait(true);

            var assistantTurn = _session.Turns[^1];
            AppendTurnToTranscript(assistantTurn, baselineScript: script);

            if (assistantTurn.Disposition == EditDisposition.PendingReview)
            {
                _pendingTurnIndex = _session.Turns.Count - 1;
            }

            SetStatus("Ready.");

            _logger.SendCompleted(
                stopwatch.ElapsedMilliseconds,
                result.ProposedEdit,
                result.SymbolsLookedUp.Count);
        }
        catch (Exception ex)
        {
            // The UI is the end of the line for this exception — rethrowing from an event
            // handler would take the host app down, so it is surfaced and logged instead. The
            // transcript only gets ex.Message; the log is where the stack trace survives.
            _logger.SendFailed(ex, stopwatch.ElapsedMilliseconds);
            AppendTurnToTranscript(
                new ChatTurn(ChatTurnRole.Assistant, $"That turn failed: {ex.Message}", null, null, EditDisposition.None),
                baselineScript: null);
            SetStatus("Last turn failed.");
        }
        finally
        {
            _turnInFlight = false;
            UpdateEnabledState();

            // Whether this send was confirmed via the two-Enter arm-then-send or a mouse click
            // on Send, hand focus straight back so the next message can be typed immediately.
            _inputTextBox.Focus();
        }
    }

    /// <summary>Appends one turn — role caption, prose, and a proposal's diff if it has one.</summary>
    private void AppendTurnToTranscript(ChatTurn turn, string? baselineScript)
    {
        var caption = BuildRoleCaption(turn);
        var markdown = string.IsNullOrWhiteSpace(turn.Text)
            ? $"**{caption}**"
            : $"**{caption}**\n\n{turn.Text}";

        _transcriptTextBox.AppendMarkdown(markdown);

        if (turn.ProposedCode is not null)
        {
            AppendProposalDiff(turn.ProposedCode, baselineScript);
        }
        else if (turn.ProposedHunks is not null)
        {
            AppendProposalHunks(turn.ProposedHunks);
        }
    }

    /// <summary>
    /// Appends a patch proposal's hunks (Job 3) as an unparsed, monospaced block — each hunk's
    /// old text shown as removed and its new text as added, in the order the hunks apply.
    /// </summary>
    private void AppendProposalHunks(IReadOnlyList<ScriptEditHunk> hunks)
    {
        // AppendPlainText never starts its own new paragraph the way AppendMarkdown does, so
        // without this the first line would run straight on from the caption/prose above.
        _transcriptTextBox.AppendPlainText(string.Empty);

        for (var i = 0; i < hunks.Count; i++)
        {
            if (i > 0)
            {
                _transcriptTextBox.AppendPlainText(string.Empty);
            }

            var hunk = hunks[i];

            foreach (var line in hunk.OldText.ReplaceLineEndings("\n").Split('\n'))
            {
                _transcriptTextBox.AppendPlainText("- " + line, s_removedBackColour);
            }

            foreach (var line in hunk.NewText.ReplaceLineEndings("\n").Split('\n'))
            {
                _transcriptTextBox.AppendPlainText("+ " + line, s_addedBackColour);
            }
        }
    }

    /// <summary>
    /// Appends a proposal's code as an unparsed, monospaced block — a line-by-line diff against
    /// <paramref name="baselineScript"/> when one is known, or the proposal in full otherwise
    /// (which is what happens for a turn restored from an existing session).
    /// </summary>
    private void AppendProposalDiff(string proposedCode, string? baselineScript)
    {
        // AppendPlainText never starts its own new paragraph the way AppendMarkdown does, so
        // without this the first diff line would run straight on from the caption/prose above.
        _transcriptTextBox.AppendPlainText(string.Empty);

        if (baselineScript is null)
        {
            foreach (var line in proposedCode.ReplaceLineEndings("\n").Split('\n'))
            {
                _transcriptTextBox.AppendPlainText(line);
            }

            return;
        }

        var diff = ScriptDiff.Compute(baselineScript, proposedCode);

        if (!ScriptDiff.HasChanges(diff))
        {
            _transcriptTextBox.AppendMarkdown("*(The proposal is identical to the current script.)*");
            return;
        }

        foreach (var line in diff)
        {
            var (marker, backColour) = line.Kind switch
            {
                ScriptDiffLineKind.Added => ("+ ", (Color?)s_addedBackColour),
                ScriptDiffLineKind.Removed => ("- ", (Color?)s_removedBackColour),
                _ => ("  ", null),
            };

            _transcriptTextBox.AppendPlainText(marker + line.Text, backColour);
        }
    }

    private static string BuildRoleCaption(ChatTurn turn)
    {
        var speaker = turn.Role == ChatTurnRole.User ? "You" : "Assistant";

        if (!turn.HasProposedEdit)
        {
            return speaker;
        }

        var state = turn.Disposition switch
        {
            EditDisposition.Accepted => "edit accepted",
            EditDisposition.Rejected => "edit rejected",
            _ => "proposed an edit",
        };

        var summary = string.IsNullOrWhiteSpace(turn.EditSummary) ? null : $" — {turn.EditSummary}";
        return $"{speaker} · {state}{summary}";
    }

    private void OnAcceptButtonClick(object? sender, EventArgs e)
    {
        if (!TryGetPendingTurn(out var turn))
        {
            _logger.EditActionIgnored();
            return;
        }

        if (ScriptTextSetter is null)
        {
            _logger.EditApplyHadNoSetter();
            SetStatus("No editor is wired up, so the edit could not be applied.");
            return;
        }

        if (turn.ProposedHunks is not null && ScriptTextProvider is null)
        {
            // A patch applies against a fresh read of the buffer, not the frozen baseline used
            // to render the diff — unlike a full-script replacement, it genuinely needs a source.
            _logger.EditApplyHadNoSetter();
            SetStatus("No script source is configured, so the patch could not be applied.");
            return;
        }

        var index = _pendingTurnIndex!.Value;
        string script;

        try
        {
            // A patch is re-applied to the buffer's current contents rather than the frozen
            // baseline the diff was rendered against, so it fails cleanly here — same as
            // Claude Code's Edit tool and Copilot's replace_string_in_file — if the buffer has
            // changed since the proposal was made, instead of silently overwriting an edit the
            // user made in the meantime.
            script = turn.ProposedHunks is not null
                ? ScriptPatchApplier.Apply(ScriptTextProvider!(), turn.ProposedHunks)
                : turn.ProposedCode!;

            // Models emit bare "\n". A plain WinForms TextBox only breaks lines on "\r\n", so the
            // script would arrive in the editor as one long line. Normalise here rather than in
            // every host's setter.
            script = script.ReplaceLineEndings();

            ScriptTextSetter(script);
        }
        catch (Exception ex)
        {
            // The patch no longer applied, or the host's setter failed — either way the buffer
            // is in an unknown or unchanged state, so leave the proposal pending rather than
            // marking it accepted.
            _logger.EditApplyFailed(ex, index);
            SetStatus($"Could not apply the edit: {ex.Message}");
            return;
        }

        _session!.SetEditDisposition(index, EditDisposition.Accepted);
        _transcriptTextBox.AppendMarkdown("*Edit accepted.*");
        _pendingTurnIndex = null;
        UpdateEnabledState();

        SetStatus("Edit applied.");
        _logger.EditAccepted(index, script.Length);
        EditAccepted?.Invoke(this, new ScriptEditAcceptedEventArgs(script, turn.EditSummary));
    }

    private void OnRejectButtonClick(object? sender, EventArgs e)
    {
        if (!TryGetPendingTurn(out _))
        {
            _logger.EditActionIgnored();
            return;
        }

        var index = _pendingTurnIndex!.Value;
        _session!.SetEditDisposition(index, EditDisposition.Rejected);
        _transcriptTextBox.AppendMarkdown("*Edit rejected.*");
        _pendingTurnIndex = null;
        UpdateEnabledState();

        SetStatus("Edit rejected.");
        _logger.EditRejected(index);
    }

    private bool TryGetPendingTurn(out ChatTurn turn)
    {
        turn = null!;

        if (_session is null || _pendingTurnIndex is not { } index || index >= _session.Turns.Count)
        {
            return false;
        }

        var candidate = _session.Turns[index];
        if (candidate.Disposition != EditDisposition.PendingReview || !candidate.HasProposedEdit)
        {
            return false;
        }

        turn = candidate;
        return true;
    }

    private void SetStatus(string status)
    {
        _statusLabel.Text = status;
    }

    private void UpdateEnabledState()
    {
        var canSend = IsReady && !_turnInFlight && _pendingTurnIndex is null;
        _sendButton.Enabled = canSend;
        _inputTextBox.Enabled = canSend;

        var hasPendingProposal = _pendingTurnIndex is not null;
        _acceptButton.Enabled = hasPendingProposal;
        _rejectButton.Enabled = hasPendingProposal;
    }
}
