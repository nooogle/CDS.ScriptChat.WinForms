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
        _transcriptPanel = new System.Windows.Forms.FlowLayoutPanel();
        _statusLabel = new System.Windows.Forms.Label();
        _inputLayout = new System.Windows.Forms.TableLayoutPanel();
        _inputTextBox = new System.Windows.Forms.TextBox();
        _sendButton = new System.Windows.Forms.Button();
        _rootLayout.SuspendLayout();
        _inputLayout.SuspendLayout();
        SuspendLayout();
        //
        // _rootLayout
        //
        _rootLayout.ColumnCount = 1;
        _rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _rootLayout.Controls.Add(_transcriptPanel, 0, 0);
        _rootLayout.Controls.Add(_statusLabel, 0, 1);
        _rootLayout.Controls.Add(_inputLayout, 0, 2);
        _rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        _rootLayout.Location = new System.Drawing.Point(0, 0);
        _rootLayout.Name = "_rootLayout";
        _rootLayout.RowCount = 3;
        _rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _rootLayout.Size = new System.Drawing.Size(420, 560);
        _rootLayout.TabIndex = 0;
        //
        // _transcriptPanel
        //
        _transcriptPanel.AutoScroll = true;
        _transcriptPanel.BackColor = System.Drawing.SystemColors.Window;
        _transcriptPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        _transcriptPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        _transcriptPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
        _transcriptPanel.Location = new System.Drawing.Point(3, 3);
        _transcriptPanel.Name = "_transcriptPanel";
        _transcriptPanel.Size = new System.Drawing.Size(414, 470);
        _transcriptPanel.TabIndex = 0;
        _transcriptPanel.WrapContents = false;
        //
        // _statusLabel
        //
        _statusLabel.AutoSize = true;
        _statusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _statusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _statusLabel.Location = new System.Drawing.Point(3, 476);
        _statusLabel.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new System.Drawing.Size(414, 15);
        _statusLabel.TabIndex = 1;
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
        _inputLayout.Location = new System.Drawing.Point(0, 495);
        _inputLayout.Margin = new System.Windows.Forms.Padding(0);
        _inputLayout.Name = "_inputLayout";
        _inputLayout.RowCount = 1;
        _inputLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _inputLayout.Size = new System.Drawing.Size(420, 65);
        _inputLayout.TabIndex = 2;
        //
        // _inputTextBox
        //
        _inputTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
        _inputTextBox.Location = new System.Drawing.Point(3, 3);
        _inputTextBox.Multiline = true;
        _inputTextBox.Name = "_inputTextBox";
        _inputTextBox.PlaceholderText = "Ask about the script, or describe a change…";
        _inputTextBox.Size = new System.Drawing.Size(334, 59);
        _inputTextBox.TabIndex = 0;
        _inputTextBox.KeyDown += OnInputTextBoxKeyDown;
        //
        // _sendButton
        //
        _sendButton.Dock = System.Windows.Forms.DockStyle.Fill;
        _sendButton.Location = new System.Drawing.Point(343, 3);
        _sendButton.Name = "_sendButton";
        _sendButton.Size = new System.Drawing.Size(74, 59);
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
        _inputLayout.ResumeLayout(false);
        _inputLayout.PerformLayout();
        ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _rootLayout;
    private System.Windows.Forms.FlowLayoutPanel _transcriptPanel;
    private System.Windows.Forms.Label _statusLabel;
    private System.Windows.Forms.TableLayoutPanel _inputLayout;
    private System.Windows.Forms.TextBox _inputTextBox;
    private System.Windows.Forms.Button _sendButton;
}
