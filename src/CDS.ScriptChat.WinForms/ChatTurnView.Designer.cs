namespace CDS.ScriptChat.WinForms;

partial class ChatTurnView
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
        if (disposing && (components != null))
        {
            components.Dispose();
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
        _layout = new System.Windows.Forms.TableLayoutPanel();
        _roleLabel = new System.Windows.Forms.Label();
        _messageLabel = new System.Windows.Forms.Label();
        _diffTextBox = new System.Windows.Forms.RichTextBox();
        _actionsPanel = new System.Windows.Forms.FlowLayoutPanel();
        _acceptButton = new System.Windows.Forms.Button();
        _rejectButton = new System.Windows.Forms.Button();
        _layout.SuspendLayout();
        _actionsPanel.SuspendLayout();
        SuspendLayout();
        //
        // _layout
        //
        _layout.AutoSize = true;
        _layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _layout.ColumnCount = 1;
        _layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _layout.Controls.Add(_roleLabel, 0, 0);
        _layout.Controls.Add(_messageLabel, 0, 1);
        _layout.Controls.Add(_diffTextBox, 0, 2);
        _layout.Controls.Add(_actionsPanel, 0, 3);
        _layout.Dock = System.Windows.Forms.DockStyle.Top;
        _layout.Location = new System.Drawing.Point(0, 0);
        _layout.Margin = new System.Windows.Forms.Padding(0);
        _layout.Name = "_layout";
        _layout.Padding = new System.Windows.Forms.Padding(8);
        _layout.RowCount = 4;
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.Size = new System.Drawing.Size(400, 68);
        _layout.TabIndex = 0;
        //
        // _roleLabel
        //
        _roleLabel.AutoSize = true;
        _roleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold);
        _roleLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _roleLabel.Location = new System.Drawing.Point(8, 8);
        _roleLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
        _roleLabel.Name = "_roleLabel";
        _roleLabel.Size = new System.Drawing.Size(30, 15);
        _roleLabel.TabIndex = 0;
        _roleLabel.Text = "You";
        //
        // _messageLabel
        //
        _messageLabel.AutoSize = true;
        _messageLabel.Location = new System.Drawing.Point(8, 27);
        _messageLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
        _messageLabel.Name = "_messageLabel";
        _messageLabel.Size = new System.Drawing.Size(0, 15);
        _messageLabel.TabIndex = 1;
        //
        // _diffTextBox
        //
        _diffTextBox.BackColor = System.Drawing.SystemColors.Window;
        _diffTextBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        _diffTextBox.DetectUrls = false;
        _diffTextBox.Font = new System.Drawing.Font("Cascadia Mono", 9F);
        _diffTextBox.Location = new System.Drawing.Point(8, 46);
        _diffTextBox.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
        _diffTextBox.Name = "_diffTextBox";
        _diffTextBox.ReadOnly = true;
        _diffTextBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Both;
        _diffTextBox.Size = new System.Drawing.Size(384, 200);
        _diffTextBox.TabIndex = 2;
        _diffTextBox.Text = "";
        _diffTextBox.Visible = false;
        _diffTextBox.WordWrap = false;
        //
        // _actionsPanel
        //
        _actionsPanel.AutoSize = true;
        _actionsPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _actionsPanel.Controls.Add(_acceptButton);
        _actionsPanel.Controls.Add(_rejectButton);
        _actionsPanel.Location = new System.Drawing.Point(8, 250);
        _actionsPanel.Margin = new System.Windows.Forms.Padding(0);
        _actionsPanel.Name = "_actionsPanel";
        _actionsPanel.Size = new System.Drawing.Size(160, 29);
        _actionsPanel.TabIndex = 3;
        _actionsPanel.Visible = false;
        _actionsPanel.WrapContents = false;
        //
        // _acceptButton
        //
        _acceptButton.AutoSize = true;
        _acceptButton.Location = new System.Drawing.Point(0, 3);
        _acceptButton.Margin = new System.Windows.Forms.Padding(0, 3, 6, 3);
        _acceptButton.Name = "_acceptButton";
        _acceptButton.Size = new System.Drawing.Size(75, 25);
        _acceptButton.TabIndex = 0;
        _acceptButton.Text = "Accept";
        _acceptButton.UseVisualStyleBackColor = true;
        _acceptButton.Click += OnAcceptButtonClick;
        //
        // _rejectButton
        //
        _rejectButton.AutoSize = true;
        _rejectButton.Location = new System.Drawing.Point(81, 3);
        _rejectButton.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
        _rejectButton.Name = "_rejectButton";
        _rejectButton.Size = new System.Drawing.Size(75, 25);
        _rejectButton.TabIndex = 1;
        _rejectButton.Text = "Reject";
        _rejectButton.UseVisualStyleBackColor = true;
        _rejectButton.Click += OnRejectButtonClick;
        //
        // ChatTurnView
        //
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        Controls.Add(_layout);
        Margin = new System.Windows.Forms.Padding(0);
        Name = "ChatTurnView";
        Size = new System.Drawing.Size(400, 68);
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        _actionsPanel.ResumeLayout(false);
        _actionsPanel.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.Label _roleLabel;
    private System.Windows.Forms.Label _messageLabel;
    private System.Windows.Forms.RichTextBox _diffTextBox;
    private System.Windows.Forms.FlowLayoutPanel _actionsPanel;
    private System.Windows.Forms.Button _acceptButton;
    private System.Windows.Forms.Button _rejectButton;
}
