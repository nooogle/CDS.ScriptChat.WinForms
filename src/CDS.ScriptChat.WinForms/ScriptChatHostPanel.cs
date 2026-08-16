using System.ComponentModel;

using CDS.ScriptChat.Core;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.WinForms;

/// <summary>
/// A host's wrapper around <see cref="ScriptChatPanel"/> for the case where there is more than
/// one script to talk about: one transcript, with a selector above it choosing which
/// <see cref="ScriptChatTarget"/> the conversation is currently about.
/// </summary>
/// <remarks>
/// <para>
/// There is one panel but N conversations — a <see cref="ScriptChatSession"/> per target, kept
/// alive independently. Switching target swaps which session the transcript renders and which
/// target's read/write delegates the inner panel points at, so each conversation keeps its own
/// history and switching back and forth loses nothing.
/// </para>
/// <para>
/// Every target shares a single <see cref="IChatClient"/>, which this panel owns and disposes.
/// The client is stateless between calls; all the conversation state lives in the sessions.
/// </para>
/// </remarks>
public partial class ScriptChatHostPanel : UserControl
{
    private TargetState[] _targets = [];
    private IChatClient? _chatClient;
    private ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Why the feature is switched off, or <see langword="null"/> when it is configured. Held so
    /// that switching target re-shows the reason rather than silently reverting to "Not
    /// configured."
    /// </summary>
    private string? _unavailableReason;

    /// <summary>Initialises a new instance of the <see cref="ScriptChatHostPanel"/> class.</summary>
    public ScriptChatHostPanel()
    {
        InitializeComponent();

        _targetSelector.SelectedIndexChanged += (_, _) => ShowSelectedTarget();
        _newConversationButton.Click += (_, _) => StartNewConversation();
        _settingsButton.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the user asks for the provider/key settings dialogue.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>
    /// Raised after an accepted edit has been written into an editor, so the host can report it.
    /// </summary>
    public event EventHandler<ScriptEditAcceptedEventArgs>? EditAccepted
    {
        add => _chatPanel.EditAccepted += value;
        remove => _chatPanel.EditAccepted -= value;
    }

    /// <summary>
    /// Gets or sets the factory the chat library logs through. Set it before
    /// <see cref="Configure"/> so the client and sessions it builds are instrumented too.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ILoggerFactory? LoggerFactory
    {
        get => _loggerFactory;
        set
        {
            _loggerFactory = value;
            _chatPanel.LoggerFactory = value;
        }
    }

    /// <summary>Gets the target the selector is currently on.</summary>
    private TargetState? SelectedState =>
        _targetSelector.SelectedIndex is var index && index >= 0 && index < _targets.Length
            ? _targets[index]
            : null;

    /// <summary>
    /// Tells the panel which scripts it can talk about. Call this before <see cref="Configure"/>.
    /// </summary>
    /// <param name="targets">The targets to offer, in the order they appear in the selector.</param>
    /// <exception cref="ArgumentNullException"><paramref name="targets"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="targets"/> is empty.</exception>
    public void SetTargets(params ScriptChatTarget[] targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Length == 0)
        {
            throw new ArgumentException("At least one target is required.", nameof(targets));
        }

        _targets = [.. targets.Select(target => new TargetState(target))];

        _targetSelector.BeginUpdate();
        _targetSelector.Items.Clear();
        foreach (var target in targets)
        {
            _targetSelector.Items.Add(target.DisplayName);
        }

        _targetSelector.EndUpdate();

        // Driven explicitly rather than relying on SelectedIndexChanged, so the first render
        // does not depend on exactly when the combo box chooses to raise it.
        _targetSelector.SelectedIndex = 0;
        ShowSelectedTarget();
    }

    /// <summary>
    /// Builds a chat client from a provider configuration and starts a fresh conversation for
    /// every target. This is what an applied settings change, and the startup restore of a
    /// stored key, both feed into.
    /// </summary>
    /// <param name="clientOptions">The provider, key, and model to use.</param>
    /// <remarks>
    /// A configuration that cannot produce a client leaves the panel switched off with the reason
    /// shown, rather than throwing at the caller — the same behaviour the library's own
    /// <c>Configure</c> has.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="clientOptions"/> is <see langword="null"/>.</exception>
    public void Configure(ScriptChatClientOptions clientOptions)
    {
        ArgumentNullException.ThrowIfNull(clientOptions);

        IChatClient client;
        try
        {
            client = ScriptChatClientFactory.Create(clientOptions, _loggerFactory);
        }
        catch (Exception exception)
        {
            SetUnavailable($"That configuration could not be used: {exception.Message}");
            return;
        }

        // Only replace the client once the new one is in hand.
        var previous = _chatClient;
        _chatClient = client;
        previous?.Dispose();

        _unavailableReason = null;
        RestartConversations();
    }

    /// <summary>
    /// Restarts every target's conversation from scratch. Call this whenever the open document
    /// changes — a different document loaded, or a new one created — since an existing session's
    /// counterpart-script snapshot, and its whole history, belong to whichever document was open
    /// when it started.
    /// </summary>
    public void RestartConversations()
    {
        if (_chatClient is null)
        {
            return;
        }

        foreach (var state in _targets)
        {
            state.Session = CreateSession(state.Target);
        }

        ShowSelectedTarget();
    }

    /// <summary>
    /// Switches the feature off, showing why. Used when no API key is configured, so the panel
    /// reads as unconfigured rather than broken.
    /// </summary>
    /// <param name="reason">A short explanation to show the user.</param>
    public void SetUnavailable(string reason)
    {
        _unavailableReason = reason;
        foreach (var state in _targets)
        {
            state.Session = null;
        }

        var previous = _chatClient;
        _chatClient = null;
        previous?.Dispose();

        ShowSelectedTarget();
    }

    /// <summary>
    /// Discards the selected target's conversation and starts another. Also the way to refresh
    /// the context a session was built with — the counterpart script's snapshot is taken when the
    /// session is created, so a conversation started before another target's script was written
    /// would otherwise never see it.
    /// </summary>
    private void StartNewConversation()
    {
        if (_chatClient is null || SelectedState is not { } state)
        {
            return;
        }

        state.Session = CreateSession(state.Target);
        ShowSelectedTarget();
    }

    private ScriptChatSession? CreateSession(ScriptChatTarget target)
    {
        if (_chatClient is null)
        {
            return null;
        }

        var options = target.CreateSessionOptions();
        return new ScriptChatSession(_chatClient, options with { LoggerFactory = options.LoggerFactory ?? _loggerFactory });
    }

    /// <summary>
    /// Points the chat panel at the selected target's script and conversation.
    /// </summary>
    private void ShowSelectedTarget()
    {
        var state = SelectedState;

        // Before AttachSession/SetUnavailable, which report whether they have a script to work with.
        _chatPanel.ScriptTextProvider = state?.Target.ScriptTextProvider;
        _chatPanel.ScriptTextSetter = state?.Target.ScriptTextSetter;

        if (_unavailableReason is not null)
        {
            _chatPanel.SetUnavailable(_unavailableReason);
        }
        else
        {
            _chatPanel.AttachSession(state?.Session);
        }

        _newConversationButton.Enabled = state?.Session is not null;
    }

    /// <summary>A target together with the conversation it currently has, if any.</summary>
    private sealed class TargetState(ScriptChatTarget target)
    {
        public ScriptChatTarget Target { get; } = target;

        public ScriptChatSession? Session { get; set; }
    }
}
