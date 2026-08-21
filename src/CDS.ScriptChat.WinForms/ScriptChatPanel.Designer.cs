namespace CDS.ScriptChat.WinForms;

partial class ScriptChatPanel
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null!;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();

            // Only set when Configure built the client; an attached session's client belongs
            // to the caller.
            _ownedChatClient?.Dispose();
            _ownedChatClient = null;
        }

        base.Dispose(disposing);
    }

    #region Component Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        _rootLayout = new System.Windows.Forms.TableLayoutPanel();
        _transcriptTextBox = new CDS.Markdown.MarkdownTextBox();
        _decisionPanel = new System.Windows.Forms.FlowLayoutPanel();
        _acceptButton = new System.Windows.Forms.Button();
        _rejectButton = new System.Windows.Forms.Button();
        _statusLabel = new System.Windows.Forms.Label();
        _inputLayout = new System.Windows.Forms.TableLayoutPanel();
        _inputTextBox = new System.Windows.Forms.TextBox();
        _sendButton = new System.Windows.Forms.Button();
        _rootLayout.SuspendLayout();
        _decisionPanel.SuspendLayout();
        _inputLayout.SuspendLayout();
        SuspendLayout();
        //
        // _rootLayout
        //
        _rootLayout.ColumnCount = 1;
        _rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _rootLayout.Controls.Add(_transcriptTextBox, 0, 0);
        _rootLayout.Controls.Add(_decisionPanel, 0, 1);
        _rootLayout.Controls.Add(_statusLabel, 0, 2);
        _rootLayout.Controls.Add(_inputLayout, 0, 3);
        _rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        _rootLayout.Location = new System.Drawing.Point(0, 0);
        _rootLayout.Name = "_rootLayout";
        _rootLayout.RowCount = 4;
        _rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _rootLayout.Size = new System.Drawing.Size(420, 560);
        _rootLayout.TabIndex = 0;
        //
        // _transcriptTextBox
        //
        _transcriptTextBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        _transcriptTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
        _transcriptTextBox.Location = new System.Drawing.Point(3, 3);
        _transcriptTextBox.Name = "_transcriptTextBox";
        _transcriptTextBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
        _transcriptTextBox.Size = new System.Drawing.Size(414, 439);
        _transcriptTextBox.TabIndex = 0;
        _transcriptTextBox.Text = "";
        //
        // _decisionPanel
        //
        _decisionPanel.AutoSize = true;
        _decisionPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _decisionPanel.Controls.Add(_acceptButton);
        _decisionPanel.Controls.Add(_rejectButton);
        _decisionPanel.Location = new System.Drawing.Point(3, 448);
        _decisionPanel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 6);
        _decisionPanel.Name = "_decisionPanel";
        _decisionPanel.Size = new System.Drawing.Size(160, 29);
        _decisionPanel.TabIndex = 1;
        _decisionPanel.WrapContents = false;
        //
        // _acceptButton
        //
        _acceptButton.AutoSize = true;
        _acceptButton.Location = new System.Drawing.Point(0, 0);
        _acceptButton.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
        _acceptButton.Name = "_acceptButton";
        _acceptButton.Size = new System.Drawing.Size(75, 25);
        _acceptButton.TabIndex = 0;
        _acceptButton.Text = "Accept edit";
        _acceptButton.UseVisualStyleBackColor = true;
        _acceptButton.Click += OnAcceptButtonClick;
        //
        // _rejectButton
        //
        _rejectButton.AutoSize = true;
        _rejectButton.Location = new System.Drawing.Point(81, 0);
        _rejectButton.Margin = new System.Windows.Forms.Padding(0);
        _rejectButton.Name = "_rejectButton";
        _rejectButton.Size = new System.Drawing.Size(75, 25);
        _rejectButton.TabIndex = 1;
        _rejectButton.Text = "Reject edit";
        _rejectButton.UseVisualStyleBackColor = true;
        _rejectButton.Click += OnRejectButtonClick;
        //
        // _statusLabel
        //
        _statusLabel.AutoSize = true;
        _statusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _statusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _statusLabel.Location = new System.Drawing.Point(3, 483);
        _statusLabel.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new System.Drawing.Size(414, 15);
        _statusLabel.TabIndex = 2;
        _statusLabel.Text = "Not configured.";
        //
        // _inputLayout
        //
        _inputLayout.ColumnCount = 2;
        _inputLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _inputLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        _inputLayout.Controls.Add(_inputTextBox, 0, 0);
        _inputLayout.Controls.Add(_sendButton, 1, 0);
        _inputLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        _inputLayout.Location = new System.Drawing.Point(0, 502);
        _inputLayout.Margin = new System.Windows.Forms.Padding(0);
        _inputLayout.Name = "_inputLayout";
        _inputLayout.RowCount = 1;
        _inputLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _inputLayout.Size = new System.Drawing.Size(420, 58);
        _inputLayout.TabIndex = 3;
        //
        // _inputTextBox
        //
        _inputTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
        _inputTextBox.Location = new System.Drawing.Point(3, 3);
        _inputTextBox.Multiline = true;
        _inputTextBox.Name = "_inputTextBox";
        _inputTextBox.PlaceholderText = "Ask about the script, or describe a change…";
        _inputTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        _inputTextBox.Size = new System.Drawing.Size(334, 52);
        _inputTextBox.TabIndex = 0;
        _inputTextBox.KeyDown += OnInputTextBoxKeyDown;
        //
        // _sendButton
        //
        _sendButton.Dock = System.Windows.Forms.DockStyle.Fill;
        _sendButton.Location = new System.Drawing.Point(343, 3);
        _sendButton.Name = "_sendButton";
        _sendButton.Size = new System.Drawing.Size(74, 52);
        _sendButton.TabIndex = 1;
        _sendButton.Text = "Send";
        _sendButton.UseVisualStyleBackColor = true;
        _sendButton.Click += OnSendButtonClick;
        //
        // ScriptChatPanel
        //
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Controls.Add(_rootLayout);
        Name = "ScriptChatPanel";
        Size = new System.Drawing.Size(420, 560);
        _rootLayout.ResumeLayout(false);
        _rootLayout.PerformLayout();
        _decisionPanel.ResumeLayout(false);
        _decisionPanel.PerformLayout();
        _inputLayout.ResumeLayout(false);
        _inputLayout.PerformLayout();
        ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _rootLayout;
    private CDS.Markdown.MarkdownTextBox _transcriptTextBox;
    private System.Windows.Forms.FlowLayoutPanel _decisionPanel;
    private System.Windows.Forms.Button _acceptButton;
    private System.Windows.Forms.Button _rejectButton;
    private System.Windows.Forms.Label _statusLabel;
    private System.Windows.Forms.TableLayoutPanel _inputLayout;
    private System.Windows.Forms.TextBox _inputTextBox;
    private System.Windows.Forms.Button _sendButton;
}
