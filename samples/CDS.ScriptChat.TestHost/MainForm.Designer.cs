namespace CDS.ScriptChat.TestHost;

partial class MainForm
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

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        _splitContainer = new System.Windows.Forms.SplitContainer();
        _editorLayout = new System.Windows.Forms.TableLayoutPanel();
        _editorLabel = new System.Windows.Forms.Label();
        _scriptTextBox = new System.Windows.Forms.TextBox();
        _rightLayout = new System.Windows.Forms.TableLayoutPanel();
        _settingsPanel = new CDS.ScriptChat.WinForms.ScriptChatSettingsPanel();
        _chatPanel = new CDS.ScriptChat.WinForms.ScriptChatPanel();
        _lookupGroupBox = new System.Windows.Forms.GroupBox();
        _lookupListBox = new System.Windows.Forms.ListBox();
        _logFileLinkLabel = new System.Windows.Forms.LinkLabel();
        ((System.ComponentModel.ISupportInitialize)_splitContainer).BeginInit();
        _splitContainer.Panel1.SuspendLayout();
        _splitContainer.Panel2.SuspendLayout();
        _splitContainer.SuspendLayout();
        _editorLayout.SuspendLayout();
        _rightLayout.SuspendLayout();
        _lookupGroupBox.SuspendLayout();
        SuspendLayout();
        //
        // _splitContainer
        //
        _splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
        _splitContainer.Location = new System.Drawing.Point(0, 0);
        _splitContainer.Name = "_splitContainer";
        _splitContainer.Panel1.Controls.Add(_editorLayout);
        _splitContainer.Panel2.Controls.Add(_rightLayout);
        _splitContainer.Size = new System.Drawing.Size(1180, 720);
        _splitContainer.SplitterDistance = 620;
        _splitContainer.TabIndex = 0;
        //
        // _editorLayout
        //
        _editorLayout.ColumnCount = 1;
        _editorLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _editorLayout.Controls.Add(_editorLabel, 0, 0);
        _editorLayout.Controls.Add(_scriptTextBox, 0, 1);
        _editorLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        _editorLayout.Location = new System.Drawing.Point(0, 0);
        _editorLayout.Name = "_editorLayout";
        _editorLayout.RowCount = 2;
        _editorLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _editorLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _editorLayout.Size = new System.Drawing.Size(620, 720);
        _editorLayout.TabIndex = 0;
        //
        // _editorLabel
        //
        _editorLabel.AutoSize = true;
        _editorLabel.Location = new System.Drawing.Point(3, 4);
        _editorLabel.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
        _editorLabel.Name = "_editorLabel";
        _editorLabel.Size = new System.Drawing.Size(300, 15);
        _editorLabel.TabIndex = 0;
        _editorLabel.Text = "Script — a plain TextBox, so the library stays editor-agnostic";
        //
        // _scriptTextBox
        //
        _scriptTextBox.AcceptsTab = true;
        _scriptTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
        _scriptTextBox.Font = new System.Drawing.Font("Cascadia Mono", 10F);
        _scriptTextBox.Location = new System.Drawing.Point(3, 26);
        _scriptTextBox.Multiline = true;
        _scriptTextBox.Name = "_scriptTextBox";
        _scriptTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
        _scriptTextBox.Size = new System.Drawing.Size(614, 691);
        _scriptTextBox.TabIndex = 1;
        _scriptTextBox.WordWrap = false;
        //
        // _rightLayout
        //
        _rightLayout.ColumnCount = 1;
        _rightLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _rightLayout.Controls.Add(_settingsPanel, 0, 0);
        _rightLayout.Controls.Add(_chatPanel, 0, 1);
        _rightLayout.Controls.Add(_lookupGroupBox, 0, 2);
        _rightLayout.Controls.Add(_logFileLinkLabel, 0, 3);
        _rightLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        _rightLayout.Location = new System.Drawing.Point(0, 0);
        _rightLayout.Name = "_rightLayout";
        _rightLayout.RowCount = 4;
        _rightLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _rightLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _rightLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 150F));
        _rightLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _rightLayout.Size = new System.Drawing.Size(556, 720);
        _rightLayout.TabIndex = 0;
        //
        // _settingsPanel
        //
        _settingsPanel.AutoSize = true;
        _settingsPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _settingsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        _settingsPanel.Location = new System.Drawing.Point(3, 3);
        _settingsPanel.Name = "_settingsPanel";
        _settingsPanel.Size = new System.Drawing.Size(550, 160);
        _settingsPanel.TabIndex = 0;
        //
        // _chatPanel
        //
        _chatPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        _chatPanel.Location = new System.Drawing.Point(3, 169);
        _chatPanel.Name = "_chatPanel";
        _chatPanel.Size = new System.Drawing.Size(550, 392);
        _chatPanel.TabIndex = 1;
        //
        // _lookupGroupBox
        //
        _lookupGroupBox.Controls.Add(_lookupListBox);
        _lookupGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
        _lookupGroupBox.Location = new System.Drawing.Point(3, 567);
        _lookupGroupBox.Name = "_lookupGroupBox";
        _lookupGroupBox.Size = new System.Drawing.Size(550, 144);
        _lookupGroupBox.TabIndex = 2;
        _lookupGroupBox.TabStop = false;
        _lookupGroupBox.Text = "lookup_symbol calls (this host's own provider)";
        //
        // _lookupListBox
        //
        _lookupListBox.Dock = System.Windows.Forms.DockStyle.Fill;
        _lookupListBox.Font = new System.Drawing.Font("Cascadia Mono", 9F);
        _lookupListBox.FormattingEnabled = true;
        _lookupListBox.IntegralHeight = false;
        _lookupListBox.Location = new System.Drawing.Point(3, 19);
        _lookupListBox.Name = "_lookupListBox";
        _lookupListBox.Size = new System.Drawing.Size(544, 122);
        _lookupListBox.TabIndex = 0;
        //
        // _logFileLinkLabel
        //
        _logFileLinkLabel.AutoEllipsis = true;
        _logFileLinkLabel.AutoSize = true;
        _logFileLinkLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        _logFileLinkLabel.Location = new System.Drawing.Point(3, 717);
        _logFileLinkLabel.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
        _logFileLinkLabel.Name = "_logFileLinkLabel";
        _logFileLinkLabel.Size = new System.Drawing.Size(550, 15);
        _logFileLinkLabel.TabIndex = 3;
        _logFileLinkLabel.TabStop = true;
        _logFileLinkLabel.Text = "Log:";
        _logFileLinkLabel.LinkClicked += OnLogFileLinkClicked;
        //
        // MainForm
        //
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(1180, 720);
        Controls.Add(_splitContainer);
        Name = "MainForm";
        StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        Text = "CDS.ScriptChat test host";
        _splitContainer.Panel1.ResumeLayout(false);
        _splitContainer.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_splitContainer).EndInit();
        _splitContainer.ResumeLayout(false);
        _editorLayout.ResumeLayout(false);
        _editorLayout.PerformLayout();
        _rightLayout.ResumeLayout(false);
        _rightLayout.PerformLayout();
        _lookupGroupBox.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.SplitContainer _splitContainer;
    private System.Windows.Forms.TableLayoutPanel _editorLayout;
    private System.Windows.Forms.Label _editorLabel;
    private System.Windows.Forms.TextBox _scriptTextBox;
    private System.Windows.Forms.TableLayoutPanel _rightLayout;
    private CDS.ScriptChat.WinForms.ScriptChatSettingsPanel _settingsPanel;
    private CDS.ScriptChat.WinForms.ScriptChatPanel _chatPanel;
    private System.Windows.Forms.GroupBox _lookupGroupBox;
    private System.Windows.Forms.ListBox _lookupListBox;
    private System.Windows.Forms.LinkLabel _logFileLinkLabel;
}
