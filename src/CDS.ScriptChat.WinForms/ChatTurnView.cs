using System.ComponentModel;

using CDS.ScriptChat.Core;

namespace CDS.ScriptChat.WinForms;

/// <summary>
/// Renders one <see cref="ChatTurn"/> in the transcript: who spoke, their prose, and — when
/// the turn proposed an edit — the change as a diff with Accept and Reject buttons.
/// </summary>
public partial class ChatTurnView : UserControl
{
    private const int DiffBoxHeight = 200;

    private static readonly Color s_addedBackColour = Color.FromArgb(223, 245, 226);
    private static readonly Color s_removedBackColour = Color.FromArgb(255, 226, 226);

    /// <summary>Guards the height change <see cref="ApplyContentLayout"/> makes re-entering it.</summary>
    private bool _applyingContentLayout;

    /// <summary>Initialises a new instance of the <see cref="ChatTurnView"/> class.</summary>
    public ChatTurnView()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the user accepts the edit proposed by this turn.</summary>
    public event EventHandler? EditAccepted;

    /// <summary>Raised when the user rejects the edit proposed by this turn.</summary>
    public event EventHandler? EditRejected;

    /// <summary>
    /// Gets or sets this turn's index in the owning session's transcript, or
    /// <see langword="null"/> for a locally generated turn such as an error notice.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int? SessionTurnIndex { get; set; }

    /// <summary>
    /// Populates the view from a turn.
    /// </summary>
    /// <param name="turn">The turn to render.</param>
    /// <param name="baselineScript">
    /// The script as it stood when this turn was made, used to render a proposed edit as a
    /// diff. Pass <see langword="null"/> when it is not known — the proposal is then shown in
    /// full instead, which is what happens for turns restored from an existing session.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="turn"/> is <see langword="null"/>.</exception>
    public void Bind(ChatTurn turn, string? baselineScript = null)
    {
        ArgumentNullException.ThrowIfNull(turn);

        _roleLabel.Text = BuildRoleCaption(turn);
        _messageLabel.Text = turn.Text ?? string.Empty;
        _messageLabel.Visible = !string.IsNullOrWhiteSpace(turn.Text);

        if (turn.ProposedCode is null)
        {
            _diffTextBox.Visible = false;
            _diffTextBox.Clear();
            _actionsPanel.Visible = false;
        }
        else
        {
            RenderProposal(turn.ProposedCode, baselineScript);
            _diffTextBox.Visible = true;
            _diffTextBox.Height = DiffBoxHeight;

            // Only a proposal still awaiting a decision is actionable.
            _actionsPanel.Visible = turn.Disposition == EditDisposition.PendingReview;
        }

        ApplyContentLayout();
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyContentLayout();
    }

    /// <summary>Raises the <see cref="EditAccepted"/> event.</summary>
    private void OnAcceptButtonClick(object? sender, EventArgs e)
    {
        EditAccepted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raises the <see cref="EditRejected"/> event.</summary>
    private void OnRejectButtonClick(object? sender, EventArgs e)
    {
        EditRejected?.Invoke(this, EventArgs.Empty);
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

    private void RenderProposal(string proposedCode, string? baselineScript)
    {
        _diffTextBox.Clear();

        if (baselineScript is null)
        {
            // Nothing to compare against, so show the proposal as it stands.
            _diffTextBox.Text = proposedCode.ReplaceLineEndings("\r\n");
            return;
        }

        var diff = ScriptDiff.Compute(baselineScript, proposedCode);

        if (!ScriptDiff.HasChanges(diff))
        {
            _diffTextBox.Text = "(The proposal is identical to the current script.)";
            return;
        }

        foreach (var line in diff)
        {
            AppendDiffLine(line);
        }

        _diffTextBox.SelectionStart = 0;
        _diffTextBox.SelectionLength = 0;
    }

    private void AppendDiffLine(ScriptDiffLine line)
    {
        var (marker, backColour) = line.Kind switch
        {
            ScriptDiffLineKind.Added => ("+ ", s_addedBackColour),
            ScriptDiffLineKind.Removed => ("- ", s_removedBackColour),
            _ => ("  ", _diffTextBox.BackColor),
        };

        _diffTextBox.SelectionStart = _diffTextBox.TextLength;
        _diffTextBox.SelectionLength = 0;
        _diffTextBox.SelectionBackColor = backColour;
        _diffTextBox.AppendText(marker + line.Text + Environment.NewLine);
    }

    /// <summary>
    /// Constrains the wrapping controls to the available width, then takes the height that
    /// width implies.
    /// </summary>
    /// <remarks>
    /// The owning panel decides how wide a turn is, so this view must not autosize: its only
    /// child is docked, and a docked child contributes no preferred width, which would collapse
    /// the whole view to nothing. Height is the half it can work out for itself — and only once
    /// the width is known, because that is what the prose wraps against.
    /// </remarks>
    private void ApplyContentLayout()
    {
        var available = ClientSize.Width - _layout.Padding.Horizontal;
        if (available <= 0 || _applyingContentLayout)
        {
            return;
        }

        _applyingContentLayout = true;
        try
        {
            _messageLabel.Width = available;
            _messageLabel.Height = MeasureMessageHeight(available);
            _diffTextBox.Width = available;
            Height = _layout.PreferredSize.Height;
        }
        finally
        {
            _applyingContentLayout = false;
        }
    }

    /// <summary>
    /// Computes the height <see cref="_messageLabel"/> needs to show its text wrapped to
    /// <paramref name="width"/>, since — unlike a <see cref="Label"/> — a <see cref="TextBox"/>
    /// does not size itself to fit wrapped content.
    /// </summary>
    private int MeasureMessageHeight(int width)
    {
        if (!_messageLabel.Visible || string.IsNullOrEmpty(_messageLabel.Text))
        {
            return 0;
        }

        var size = TextRenderer.MeasureText(
            _messageLabel.Text,
            _messageLabel.Font,
            new Size(width, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        return size.Height;
    }
}
