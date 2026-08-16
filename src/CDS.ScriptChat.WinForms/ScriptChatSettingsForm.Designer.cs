namespace CDS.ScriptChat.WinForms;

public partial class ScriptChatSettingsForm
{
    private ScriptChatSettingsPanel _settingsPanel = null!;
    private Button _closeButton = null!;

    private void InitializeComponent()
    {
        _settingsPanel = new ScriptChatSettingsPanel();
        _closeButton = new Button();
        SuspendLayout();
        //
        // _settingsPanel
        //
        _settingsPanel.Dock = DockStyle.Top;
        _settingsPanel.Location = new Point(12, 12);
        _settingsPanel.Name = "_settingsPanel";
        _settingsPanel.Size = new Size(420, 160);
        _settingsPanel.TabIndex = 0;
        //
        // _closeButton
        //
        _closeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _closeButton.DialogResult = DialogResult.Cancel;
        _closeButton.Location = new Point(357, 178);
        _closeButton.Name = "_closeButton";
        _closeButton.Size = new Size(75, 25);
        _closeButton.TabIndex = 1;
        _closeButton.Text = "Close";
        _closeButton.UseVisualStyleBackColor = true;
        //
        // ScriptChatSettingsForm
        //
        AcceptButton = _closeButton;
        CancelButton = _closeButton;
        ClientSize = new Size(444, 215);
        Controls.Add(_closeButton);
        Controls.Add(_settingsPanel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ScriptChatSettingsForm";
        Padding = new Padding(12);
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "AI Script Chat Settings";
        ResumeLayout(false);
    }
}
