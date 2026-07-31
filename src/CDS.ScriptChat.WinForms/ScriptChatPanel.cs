using System.ComponentModel;

using CDS.ScriptChat.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDS.ScriptChat.WinForms;

/// <summary>
/// The script+chat panel: one scrolling transcript of user and assistant turns, an input box,
/// and a send button (D6).
/// </summary>
/// <remarks>
/// <para>
/// The panel never touches an editor control directly. It reads the script through
/// <see cref="ScriptTextProvider"/>, so the host can back that with Scintilla, a plain
/// <see cref="TextBox"/>, or anything else (D15).
/// </para>
/// <para>
/// Milestone 1 renders a proposed edit as read-only text. The inline diff with Accept and
/// Reject buttons — and the setter that applies it — arrive with the next build-order step.
/// </para>
/// </remarks>
public partial class ScriptChatPanel : UserControl
{
    private ScriptChatSession? _session;
    private ILogger _logger = NullLogger.Instance;
    private bool _turnInFlight;

    /// <summary>Initialises a new instance of the <see cref="ScriptChatPanel"/> class.</summary>
    public ScriptChatPanel()
    {
        InitializeComponent();
        UpdateEnabledState();
    }

    /// <summary>
    /// Gets or sets the callback that returns the script currently open in the host's editor.
    /// Called once per turn, so the model always sees the live buffer.
    /// </summary>
    /// <remarks>
    /// Deliberately a delegate rather than an editor interface: the library must not presume
    /// which editor the host uses (D15).
    /// </remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string>? ScriptTextProvider { get; set; }

    /// <summary>
    /// Gets or sets the logger. Turn structure and failures are logged; prompt and response
    /// content never are (D3).
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ILogger Logger
    {
        get => _logger;
        set => _logger = value ?? NullLogger.Instance;
    }

    /// <summary>
    /// Gets a value indicating whether the panel is ready to send a turn.
    /// </summary>
    [Browsable(false)]
    public bool IsReady => _session is not null && ScriptTextProvider is not null;

    /// <summary>
    /// Attaches the conversation this panel drives, clearing whatever was shown before.
    /// </summary>
    /// <param name="session">
    /// The session to drive, or <see langword="null"/> to detach — which is how the panel is
    /// left when no API key is configured.
    /// </param>
    public void AttachSession(ScriptChatSession? session)
    {
        _session = session;
        ClearTranscript();

        if (session is null)
        {
            SetStatus("Not configured.");
        }
        else
        {
            SetStatus(ScriptTextProvider is null
                ? "No script source configured."
                : "Ready.");

            foreach (var turn in session.Turns)
            {
                AppendTurn(turn);
            }
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
    }

    /// <summary>Removes every turn from the transcript.</summary>
    public void ClearTranscript()
    {
        foreach (Control control in _transcriptPanel.Controls)
        {
            control.Dispose();
        }

        _transcriptPanel.Controls.Clear();
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
        // Enter sends; Shift+Enter inserts a newline.
        if (e.KeyCode != Keys.Enter || e.Shift)
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
            return;
        }

        var userMessage = _inputTextBox.Text.Trim();
        if (userMessage.Length == 0)
        {
            return;
        }

        _turnInFlight = true;
        _inputTextBox.Clear();
        UpdateEnabledState();
        SetStatus("Thinking…");

        try
        {
            var script = ScriptTextProvider() ?? string.Empty;

            // Render the user's turn straight away rather than waiting for the round trip.
            AppendTurn(new ChatTurn(ChatTurnRole.User, userMessage, null, null, EditDisposition.None));

            await _session.SendAsync(userMessage, script).ConfigureAwait(true);

            // The session appended both turns; the user's is already on screen.
            AppendTurn(_session.Turns[^1]);
            SetStatus("Ready.");
        }
        catch (Exception ex)
        {
            // The UI is the end of the line for this exception — rethrowing from an event
            // handler would take the host app down, so it is surfaced and logged instead.
            _logger.LogError(ex, "Script chat turn failed.");
            AppendTurn(new ChatTurn(
                ChatTurnRole.Assistant,
                $"That turn failed: {ex.Message}",
                null,
                null,
                EditDisposition.None));
            SetStatus("Last turn failed.");
        }
        finally
        {
            _turnInFlight = false;
            UpdateEnabledState();
        }
    }

    private void AppendTurn(ChatTurn turn)
    {
        var view = new ChatTurnView { Width = GetTurnViewWidth() };
        view.Bind(turn);

        _transcriptPanel.Controls.Add(view);
        _transcriptPanel.ScrollControlIntoView(view);
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
