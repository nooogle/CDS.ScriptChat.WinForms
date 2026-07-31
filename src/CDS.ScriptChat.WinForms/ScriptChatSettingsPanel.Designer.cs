namespace CDS.ScriptChat.WinForms;

partial class ScriptChatSettingsPanel
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
        _providerLabel = new System.Windows.Forms.Label();
        _providerComboBox = new System.Windows.Forms.ComboBox();
        _modelLabel = new System.Windows.Forms.Label();
        _modelComboBox = new System.Windows.Forms.ComboBox();
        _apiKeyLabel = new System.Windows.Forms.Label();
        _apiKeyTextBox = new System.Windows.Forms.TextBox();
        _buttonsPanel = new System.Windows.Forms.FlowLayoutPanel();
        _applyButton = new System.Windows.Forms.Button();
        _testButton = new System.Windows.Forms.Button();
        _forgetKeyButton = new System.Windows.Forms.Button();
        _statusLabel = new System.Windows.Forms.Label();
        _layout.SuspendLayout();
        _buttonsPanel.SuspendLayout();
        SuspendLayout();
        //
        // _layout
        //
        _layout.AutoSize = true;
        _layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _layout.ColumnCount = 2;
        _layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _layout.Controls.Add(_providerLabel, 0, 0);
        _layout.Controls.Add(_providerComboBox, 1, 0);
        _layout.Controls.Add(_modelLabel, 0, 1);
        _layout.Controls.Add(_modelComboBox, 1, 1);
        _layout.Controls.Add(_apiKeyLabel, 0, 2);
        _layout.Controls.Add(_apiKeyTextBox, 1, 2);
        _layout.Controls.Add(_buttonsPanel, 1, 3);
        _layout.Controls.Add(_statusLabel, 1, 4);
        _layout.Dock = System.Windows.Forms.DockStyle.Fill;
        _layout.Location = new System.Drawing.Point(0, 0);
        _layout.Name = "_layout";
        _layout.Padding = new System.Windows.Forms.Padding(8);
        _layout.RowCount = 5;
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.Size = new System.Drawing.Size(420, 160);
        _layout.TabIndex = 0;
        //
        // _providerLabel
        //
        _providerLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _providerLabel.AutoSize = true;
        _providerLabel.Location = new System.Drawing.Point(11, 12);
        _providerLabel.Name = "_providerLabel";
        _providerLabel.Size = new System.Drawing.Size(55, 15);
        _providerLabel.TabIndex = 0;
        _providerLabel.Text = "Provider:";
        //
        // _providerComboBox
        //
        _providerComboBox.Dock = System.Windows.Forms.DockStyle.Fill;
        _providerComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        _providerComboBox.Location = new System.Drawing.Point(72, 11);
        _providerComboBox.Name = "_providerComboBox";
        _providerComboBox.Size = new System.Drawing.Size(337, 23);
        _providerComboBox.TabIndex = 1;
        _providerComboBox.SelectedIndexChanged += OnProviderSelectedIndexChanged;
        //
        // _modelLabel
        //
        _modelLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _modelLabel.AutoSize = true;
        _modelLabel.Location = new System.Drawing.Point(11, 41);
        _modelLabel.Name = "_modelLabel";
        _modelLabel.Size = new System.Drawing.Size(43, 15);
        _modelLabel.TabIndex = 2;
        _modelLabel.Text = "Model:";
        //
        // _modelComboBox
        //
        _modelComboBox.Dock = System.Windows.Forms.DockStyle.Fill;
        _modelComboBox.Location = new System.Drawing.Point(72, 40);
        _modelComboBox.Name = "_modelComboBox";
        _modelComboBox.Size = new System.Drawing.Size(337, 23);
        _modelComboBox.TabIndex = 3;
        //
        // _apiKeyLabel
        //
        _apiKeyLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _apiKeyLabel.AutoSize = true;
        _apiKeyLabel.Location = new System.Drawing.Point(11, 70);
        _apiKeyLabel.Name = "_apiKeyLabel";
        _apiKeyLabel.Size = new System.Drawing.Size(51, 15);
        _apiKeyLabel.TabIndex = 4;
        _apiKeyLabel.Text = "API key:";
        //
        // _apiKeyTextBox
        //
        _apiKeyTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
        _apiKeyTextBox.Location = new System.Drawing.Point(72, 69);
        _apiKeyTextBox.Name = "_apiKeyTextBox";
        _apiKeyTextBox.PlaceholderText = "Your own provider API key";
        _apiKeyTextBox.Size = new System.Drawing.Size(337, 23);
        _apiKeyTextBox.TabIndex = 5;
        _apiKeyTextBox.UseSystemPasswordChar = true;
        //
        // _buttonsPanel
        //
        _buttonsPanel.AutoSize = true;
        _buttonsPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _buttonsPanel.Controls.Add(_applyButton);
        _buttonsPanel.Controls.Add(_testButton);
        _buttonsPanel.Controls.Add(_forgetKeyButton);
        _buttonsPanel.Location = new System.Drawing.Point(72, 98);
        _buttonsPanel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        _buttonsPanel.Name = "_buttonsPanel";
        _buttonsPanel.Size = new System.Drawing.Size(260, 31);
        _buttonsPanel.TabIndex = 6;
        _buttonsPanel.WrapContents = false;
        //
        // _applyButton
        //
        _applyButton.AutoSize = true;
        _applyButton.Location = new System.Drawing.Point(0, 3);
        _applyButton.Margin = new System.Windows.Forms.Padding(0, 3, 6, 3);
        _applyButton.Name = "_applyButton";
        _applyButton.Size = new System.Drawing.Size(75, 25);
        _applyButton.TabIndex = 0;
        _applyButton.Text = "Apply";
        _applyButton.UseVisualStyleBackColor = true;
        _applyButton.Click += OnApplyButtonClick;
        //
        // _testButton
        //
        _testButton.AutoSize = true;
        _testButton.Location = new System.Drawing.Point(81, 3);
        _testButton.Margin = new System.Windows.Forms.Padding(0, 3, 6, 3);
        _testButton.Name = "_testButton";
        _testButton.Size = new System.Drawing.Size(100, 25);
        _testButton.TabIndex = 1;
        _testButton.Text = "Test connection";
        _testButton.UseVisualStyleBackColor = true;
        _testButton.Click += OnTestButtonClick;
        //
        // _forgetKeyButton
        //
        _forgetKeyButton.AutoSize = true;
        _forgetKeyButton.Location = new System.Drawing.Point(187, 3);
        _forgetKeyButton.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
        _forgetKeyButton.Name = "_forgetKeyButton";
        _forgetKeyButton.Size = new System.Drawing.Size(85, 25);
        _forgetKeyButton.TabIndex = 2;
        _forgetKeyButton.Text = "Forget key";
        _forgetKeyButton.UseVisualStyleBackColor = true;
        _forgetKeyButton.Click += OnForgetKeyButtonClick;
        //
        // _statusLabel
        //
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _statusLabel.Location = new System.Drawing.Point(72, 135);
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new System.Drawing.Size(0, 15);
        _statusLabel.TabIndex = 7;
        //
        // ScriptChatSettingsPanel
        //
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        Controls.Add(_layout);
        Name = "ScriptChatSettingsPanel";
        Size = new System.Drawing.Size(420, 160);
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        _buttonsPanel.ResumeLayout(false);
        _buttonsPanel.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.Label _providerLabel;
    private System.Windows.Forms.ComboBox _providerComboBox;
    private System.Windows.Forms.Label _modelLabel;
    private System.Windows.Forms.ComboBox _modelComboBox;
    private System.Windows.Forms.Label _apiKeyLabel;
    private System.Windows.Forms.TextBox _apiKeyTextBox;
    private System.Windows.Forms.FlowLayoutPanel _buttonsPanel;
    private System.Windows.Forms.Button _applyButton;
    private System.Windows.Forms.Button _testButton;
    private System.Windows.Forms.Button _forgetKeyButton;
    private System.Windows.Forms.Label _statusLabel;
}
