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
    private ScriptChatSession? _session;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = NullLogger.Instance;
    private bool _turnInFlight;

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
                AppendTurn(session.Turns[i], sessionTurnIndex: i, baselineScript: session.GetScriptBaseline(i));
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
        // Snapshot first: disposing a control removes it from this collection, so disposing
        // while enumerating it would skip every other turn and leak its handles.
        var views = _transcriptPanel.Controls.Cast<Control>().ToArray();

        _transcriptPanel.Controls.Clear();

        foreach (var view in views)
        {
            view.Dispose();
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ResizeTurnViews();
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
        if (_turnInFlight || _session is null || ScriptTextProvider is null)
        {
            _logger.SendIgnored(
                _turnInFlight ? "a turn is already in flight"
                : _session is null ? "no session is attached"
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
            AppendTurn(
                new ChatTurn(ChatTurnRole.User, userMessage, null, null, EditDisposition.None),
                sessionTurnIndex: _session.Turns.Count,
                baselineScript: null);

            var result = await _session.SendAsync(userMessage, script).ConfigureAwait(true);

            AppendTurn(_session.Turns[^1], sessionTurnIndex: _session.Turns.Count - 1, baselineScript: script);
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
            AppendTurn(
                new ChatTurn(ChatTurnRole.Assistant, $"That turn failed: {ex.Message}", null, null, EditDisposition.None),
                sessionTurnIndex: null,
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

    private void AppendTurn(ChatTurn turn, int? sessionTurnIndex, string? baselineScript)
    {
        var view = new ChatTurnView
        {
            Width = GetTurnViewWidth(),
            SessionTurnIndex = sessionTurnIndex,
        };

        view.EditAccepted += OnTurnEditAccepted;
        view.EditRejected += OnTurnEditRejected;
        view.Bind(turn, baselineScript);

        _transcriptPanel.Controls.Add(view);
        _transcriptPanel.ScrollControlIntoView(view);
    }

    private void OnTurnEditAccepted(object? sender, EventArgs e)
    {
        if (sender is not ChatTurnView view || !TryGetPendingTurn(view, out var turn))
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

        // Models emit bare "\n". A plain WinForms TextBox only breaks lines on "\r\n", so the
        // script would arrive in the editor as one long line. Normalise here rather than in
        // every host's setter.
        var script = turn.ProposedCode!.ReplaceLineEndings();

        try
        {
            ScriptTextSetter(script);
        }
        catch (Exception ex)
        {
            // The host's setter failed, so the buffer is in an unknown state — leave the
            // proposal pending rather than marking it accepted.
            _logger.EditApplyFailed(ex, view.SessionTurnIndex!.Value);
            SetStatus($"Could not apply the edit: {ex.Message}");
            return;
        }

        RecordDisposition(view, EditDisposition.Accepted);
        SetStatus("Edit applied.");
        _logger.EditAccepted(view.SessionTurnIndex!.Value, script.Length);
        EditAccepted?.Invoke(this, new ScriptEditAcceptedEventArgs(script, turn.EditSummary));
    }

    private void OnTurnEditRejected(object? sender, EventArgs e)
    {
        if (sender is not ChatTurnView view || !TryGetPendingTurn(view, out _))
        {
            _logger.EditActionIgnored();
            return;
        }

        RecordDisposition(view, EditDisposition.Rejected);
        SetStatus("Edit rejected.");
        _logger.EditRejected(view.SessionTurnIndex!.Value);
    }

    private bool TryGetPendingTurn(ChatTurnView view, out ChatTurn turn)
    {
        turn = null!;

        if (_session is null || view.SessionTurnIndex is not { } index || index >= _session.Turns.Count)
        {
            return false;
        }

        var candidate = _session.Turns[index];
        if (candidate.Disposition != EditDisposition.PendingReview || candidate.ProposedCode is null)
        {
            return false;
        }

        turn = candidate;
        return true;
    }

    private void RecordDisposition(ChatTurnView view, EditDisposition disposition)
    {
        var index = view.SessionTurnIndex!.Value;
        _session!.SetEditDisposition(index, disposition);

        // Re-bind so the caption updates and the buttons disappear; the diff itself stays.
        view.Bind(_session.Turns[index], _session.GetScriptBaseline(index));
    }

    private void ResizeTurnViews()
    {
        var width = GetTurnViewWidth();
        foreach (Control control in _transcriptPanel.Controls)
        {
            control.Width = width;
        }
    }

    /// <summary>
    /// Gets the width a turn view should take. A <see cref="FlowLayoutPanel"/> gives its
    /// children no width of their own, so it is set here — and shrunk by the scrollbar so
    /// adding a turn cannot introduce a horizontal one.
    /// </summary>
    private int GetTurnViewWidth()
    {
        var width = _transcriptPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4;
        return Math.Max(width, 50);
    }

    private void SetStatus(string status)
    {
        _statusLabel.Text = status;
    }

    private void UpdateEnabledState()
    {
        var canSend = IsReady && !_turnInFlight;
        _sendButton.Enabled = canSend;
        _inputTextBox.Enabled = canSend;
    }
}
