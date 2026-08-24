namespace CDS.ScriptChat.SampleApp;

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
        _leftLayout = new System.Windows.Forms.TableLayoutPanel();
        _scriptLabel = new System.Windows.Forms.Label();
        _scriptTextBox = new System.Windows.Forms.TextBox();
        _runButton = new System.Windows.Forms.Button();
        _outputTextBox = new System.Windows.Forms.TextBox();
        _chatPanel = new CDS.ScriptChat.WinForms.ScriptChatHostPanel();
        ((System.ComponentModel.ISupportInitialize)_splitContainer).BeginInit();
        _splitContainer.Panel1.SuspendLayout();
        _splitContainer.Panel2.SuspendLayout();
        _splitContainer.SuspendLayout();
        _leftLayout.SuspendLayout();
        SuspendLayout();
        //
        // _splitContainer
        //
        _splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
        _splitContainer.Location = new System.Drawing.Point(0, 0);
        _splitContainer.Name = "_splitContainer";
        _splitContainer.Panel1.Controls.Add(_leftLayout);
        _splitContainer.Panel2.Controls.Add(_chatPanel);
        _splitContainer.Size = new System.Drawing.Size(1180, 720);
        _splitContainer.SplitterDistance = 700;
        _splitContainer.TabIndex = 0;
        //
        // _leftLayout
        //
        _leftLayout.ColumnCount = 1;
        _leftLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _leftLayout.Controls.Add(_scriptLabel, 0, 0);
        _leftLayout.Controls.Add(_scriptTextBox, 0, 1);
        _leftLayout.Controls.Add(_runButton, 0, 2);
        _leftLayout.Controls.Add(_outputTextBox, 0, 3);
        _leftLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        _leftLayout.Location = new System.Drawing.Point(0, 0);
        _leftLayout.Name = "_leftLayout";
        _leftLayout.RowCount = 4;
        _leftLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _leftLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _leftLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _leftLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 180F));
        _leftLayout.Size = new System.Drawing.Size(700, 720);
        _leftLayout.TabIndex = 0;
        //
        // _scriptLabel
        //
        _scriptLabel.AutoSize = true;
        _scriptLabel.Location = new System.Drawing.Point(3, 4);
        _scriptLabel.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
        _scriptLabel.Name = "_scriptLabel";
        _scriptLabel.Size = new System.Drawing.Size(300, 15);
        _scriptLabel.TabIndex = 0;
        _scriptLabel.Text = "Inspection script — a plain TextBox; the library never touches the editor directly";
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
        _scriptTextBox.Size = new System.Drawing.Size(694, 470);
        _scriptTextBox.TabIndex = 1;
        _scriptTextBox.WordWrap = false;
        //
        // _runButton
        //
        _runButton.AutoSize = true;
        _runButton.Location = new System.Drawing.Point(3, 502);
        _runButton.Name = "_runButton";
        _runButton.Size = new System.Drawing.Size(120, 27);
        _runButton.TabIndex = 2;
        _runButton.Text = "Run script";
        _runButton.UseVisualStyleBackColor = true;
        _runButton.Click += OnRunButtonClick;
        //
        // _outputTextBox
        //
        _outputTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
        _outputTextBox.Font = new System.Drawing.Font("Cascadia Mono", 9F);
        _outputTextBox.Location = new System.Drawing.Point(3, 535);
        _outputTextBox.Multiline = true;
        _outputTextBox.Name = "_outputTextBox";
        _outputTextBox.ReadOnly = true;
        _outputTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        _outputTextBox.Size = new System.Drawing.Size(694, 174);
        _outputTextBox.TabIndex = 3;
        //
        // _chatPanel
        //
        _chatPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        _chatPanel.Location = new System.Drawing.Point(0, 0);
        _chatPanel.Name = "_chatPanel";
        _chatPanel.Size = new System.Drawing.Size(476, 720);
        _chatPanel.TabIndex = 0;
        //
        // MainForm
        //
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(1180, 720);
        Controls.Add(_splitContainer);
        Name = "MainForm";
        StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        Text = "Widget inspection — CDS.ScriptChat sample";
        _splitContainer.Panel1.ResumeLayout(false);
        _splitContainer.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_splitContainer).EndInit();
        _splitContainer.ResumeLayout(false);
        _leftLayout.ResumeLayout(false);
        _leftLayout.PerformLayout();
        ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.SplitContainer _splitContainer;
    private System.Windows.Forms.TableLayoutPanel _leftLayout;
    private System.Windows.Forms.Label _scriptLabel;
    private System.Windows.Forms.TextBox _scriptTextBox;
    private System.Windows.Forms.Button _runButton;
    private System.Windows.Forms.TextBox _outputTextBox;
    private CDS.ScriptChat.WinForms.ScriptChatHostPanel _chatPanel;
}
