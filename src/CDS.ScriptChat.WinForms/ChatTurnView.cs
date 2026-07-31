using System.ComponentModel;

using CDS.ScriptChat.Core;

namespace CDS.ScriptChat.WinForms;

/// <summary>
/// Renders one <see cref="ChatTurn"/> in the transcript: who spoke, their prose, and — when
/// the turn proposed an edit — the proposed script.
/// </summary>
/// <remarks>
/// Milestone 1 shows a proposed edit as plain text in a read-only box. The inline diff with
/// Accept and Reject buttons arrives with the next build-order step.
/// </remarks>
public partial class ChatTurnView : UserControl
{
    private const int CodeBoxHeight = 160;

    /// <summary>Initialises a new instance of the <see cref="ChatTurnView"/> class.</summary>
    public ChatTurnView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Populates the view from a turn.
    /// </summary>
    /// <param name="turn">The turn to render.</param>
    /// <exception cref="ArgumentNullException"><paramref name="turn"/> is <see langword="null"/>.</exception>
    public void Bind(ChatTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);

        _roleLabel.Text = BuildRoleCaption(turn);
        _messageLabel.Text = turn.Text ?? string.Empty;
        _messageLabel.Visible = !string.IsNullOrWhiteSpace(turn.Text);

        if (turn.ProposedCode is null)
        {
            _codeTextBox.Visible = false;
            _codeTextBox.Text = string.Empty;
        }
        else
        {
            // WinForms text boxes want CRLF; a script assembled elsewhere may use bare LF.
            _codeTextBox.Text = turn.ProposedCode.ReplaceLineEndings("\r\n");
            _codeTextBox.Visible = true;
            _codeTextBox.Height = CodeBoxHeight;
        }

        ApplyWrapWidth();
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyWrapWidth();
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

    /// <summary>
    /// Constrains the wrapping controls to the available width. An autosizing label inside a
    /// scrolling flow panel has no natural width to wrap against, so it is set here rather
    /// than in the Designer.
    /// </summary>
    private void ApplyWrapWidth()
    {
        var available = _layout.ClientSize.Width - _layout.Padding.Horizontal;
        if (available <= 0)
        {
            return;
        }

        _messageLabel.MaximumSize = new Size(available, 0);
        _codeTextBox.Width = available;
    }
}
