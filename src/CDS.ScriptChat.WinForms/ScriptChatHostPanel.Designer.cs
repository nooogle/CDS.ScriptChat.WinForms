namespace CDS.ScriptChat.WinForms;

public partial class ScriptChatHostPanel
{
    private Panel _headerPanel = null!;
    private ComboBox _targetSelector = null!;
    private Button _newConversationButton = null!;
    private Button _settingsButton = null!;
    private ScriptChatPanel _chatPanel = null!;

    private void InitializeComponent()
    {
        _headerPanel = new Panel();
        _targetSelector = new ComboBox();
        _newConversationButton = new Button();
        _settingsButton = new Button();
        _chatPanel = new ScriptChatPanel();
        _headerPanel.SuspendLayout();
        SuspendLayout();
        //
        // _headerPanel
        //
        _headerPanel.Controls.Add(_targetSelector);
        _headerPanel.Controls.Add(_newConversationButton);
        _headerPanel.Controls.Add(_settingsButton);
        _headerPanel.Dock = DockStyle.Top;
        _headerPanel.Location = new Point(0, 0);
        _headerPanel.Name = "_headerPanel";
        _headerPanel.Size = new Size(400, 32);
        _headerPanel.TabIndex = 0;
        //
        // _targetSelector
        //
        _targetSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _targetSelector.Location = new Point(8, 5);
        _targetSelector.Name = "_targetSelector";
        _targetSelector.Size = new Size(150, 23);
        _targetSelector.TabIndex = 0;
        //
        // _newConversationButton
        //
        _newConversationButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _newConversationButton.Location = new Point(198, 4);
        _newConversationButton.Name = "_newConversationButton";
        _newConversationButton.Size = new Size(110, 25);
        _newConversationButton.TabIndex = 1;
        _newConversationButton.Text = "New conversation";
        _newConversationButton.UseVisualStyleBackColor = true;
        //
        // _settingsButton
        //
        _settingsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _settingsButton.Location = new Point(314, 4);
        _settingsButton.Name = "_settingsButton";
        _settingsButton.Size = new Size(80, 25);
        _settingsButton.TabIndex = 2;
        _settingsButton.Text = "Settings...";
        _settingsButton.UseVisualStyleBackColor = true;
        //
        // _chatPanel
        //
        _chatPanel.Dock = DockStyle.Fill;
        _chatPanel.Location = new Point(0, 32);
        _chatPanel.Name = "_chatPanel";
        _chatPanel.Size = new Size(400, 568);
        _chatPanel.TabIndex = 1;
        //
        // ScriptChatHostPanel
        //
        Controls.Add(_chatPanel);
        Controls.Add(_headerPanel);
        Name = "ScriptChatHostPanel";
        Size = new Size(400, 600);
        _headerPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Owned rather than any single session's, because Configure builds one client shared
            // by every target's session rather than letting ScriptChatPanel build its own.
            _chatClient?.Dispose();
        }

        base.Dispose(disposing);
    }
}
