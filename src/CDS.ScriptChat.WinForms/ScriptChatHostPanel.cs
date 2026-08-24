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
    /// The settings dialogue <see cref="UseStoredKey(string)"/> owns, created on first use and
    /// kept for the session — the panel exposes no way to preselect a provider, so a fresh
    /// instance would always open on its defaults and silently switch a user who had chosen
    /// something else.
    /// </summary>
    private ScriptChatSettingsForm? _settingsForm;

    private Func<ScriptChatProviderPreference?>? _loadPreference;
    private Action<ScriptChatProviderPreference>? _savePreference;

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

    /// <summary>
    /// Gets where API keys are kept, once <see cref="UseStoredKey(string)"/> has been called.
    /// <see langword="null"/> when the host manages keys itself.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IApiKeyStore? ApiKeyStore { get; private set; }

    /// <summary>
    /// Takes over the whole API-key story: loads the user's stored key on startup, opens the
    /// settings dialogue when they ask for it, and remembers the provider and model they choose.
    /// </summary>
    /// <param name="applicationName">
    /// The host application's name, used to keep its keys and preferences separate from other
    /// apps that embed this panel.
    /// </param>
    /// <remarks>
    /// <para>
    /// This is the one-line replacement for the load-key/show-settings/persist-choice sequence
    /// every host previously wrote by hand. It can be called before or after the scripts are
    /// added. Bring your own key: nothing ships with the app, and the panel stays switched off
    /// with a pointer at the settings dialogue until the user supplies one (D3).
    /// </para>
    /// <para>
    /// The provider and model are remembered in a small file beside the encrypted key store. A
    /// host that would rather keep them in its own settings uses the overload that takes a
    /// load/save pair.
    /// </para>
    /// <para>
    /// This subscribes to <see cref="SettingsRequested"/> and shows its own dialogue. A host
    /// that wants a different settings experience should not call this.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="applicationName"/> is empty or whitespace.</exception>
    public void UseStoredKey(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        var file = ProviderPreferenceFile.ForApplication(applicationName);
        UseStoredKey(applicationName, file.Load, file.Save);
    }

    /// <summary>
    /// Takes over the API-key story, but leaves the host to persist which provider and model the
    /// user chose — for an app that already has a settings file of its own.
    /// </summary>
    /// <param name="applicationName">The host application's name, scoping the encrypted key store.</param>
    /// <param name="loadPreference">
    /// Returns the provider and model last applied, or <see langword="null"/> for none — in which
    /// case the panel starts on <see cref="ScriptChatProvider.Claude"/>.
    /// </param>
    /// <param name="savePreference">
    /// Records the provider and model each time the user applies a configuration. Never receives
    /// the API key.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="applicationName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Either delegate is <see langword="null"/>.</exception>
    public void UseStoredKey(
        string applicationName,
        Func<ScriptChatProviderPreference?> loadPreference,
        Action<ScriptChatProviderPreference> savePreference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        UseStoredKey(
            DpapiApiKeyStore.ForApplication(
                applicationName,
                _loggerFactory?.CreateLogger(typeof(DpapiApiKeyStore))),
            loadPreference,
            savePreference);
    }

    /// <summary>
    /// Takes over the API-key story over a key store the host supplies — for an app that keeps
    /// keys somewhere other than the default per-user location, and for tests.
    /// </summary>
    /// <param name="keyStore">Where API keys are kept between sessions.</param>
    /// <param name="loadPreference">
    /// Returns the provider and model last applied, or <see langword="null"/> for none — in which
    /// case the panel starts on <see cref="ScriptChatProvider.Claude"/>.
    /// </param>
    /// <param name="savePreference">
    /// Records the provider and model each time the user applies a configuration. Never receives
    /// the API key.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public void UseStoredKey(
        IApiKeyStore keyStore,
        Func<ScriptChatProviderPreference?> loadPreference,
        Action<ScriptChatProviderPreference> savePreference)
    {
        ArgumentNullException.ThrowIfNull(keyStore);
        ArgumentNullException.ThrowIfNull(loadPreference);
        ArgumentNullException.ThrowIfNull(savePreference);

        _loadPreference = loadPreference;
        _savePreference = savePreference;
        ApiKeyStore = keyStore;

        // Unsubscribed first so calling this twice — a host reconfiguring, or a test — does not
        // open two dialogues on one click.
        SettingsRequested -= OnStoredKeySettingsRequested;
        SettingsRequested += OnStoredKeySettingsRequested;

        RestoreStoredConfiguration();
    }

    /// <summary>Gets the target the selector is currently on.</summary>
    private TargetState? SelectedState =>
        _targetSelector.SelectedIndex is var index && index >= 0 && index < _targets.Length
            ? _targets[index]
            : null;

    /// <summary>
    /// Tells the panel which scripts it can talk about, replacing any already added.
    /// </summary>
    /// <remarks>
    /// The lower-level counterpart of <see cref="AddScript(string, Func{string}, Action{string}, Type, Type[])"/>,
    /// for a host that builds its own <see cref="ScriptChatTarget"/> list. Order does not matter:
    /// calling this after the panel is already configured gives every target a conversation
    /// straight away.
    /// </remarks>
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

        _targets = [.. targets.Select(target => new TargetState(target) { Session = CreateSession(target) })];
        RebuildSelector(selectedIndex: 0);
    }

    /// <summary>
    /// Adds one script for the assistant to talk about, wiring its symbol lookup and orientation
    /// from the host's own API types. This is the easy path; call it once per script.
    /// </summary>
    /// <param name="name">The label shown on the target selector, e.g. <c>"Processing"</c>.</param>
    /// <param name="read">Reads the script currently in the host's editor.</param>
    /// <param name="write">Replaces the script, once the user has accepted an edit (D5).</param>
    /// <param name="api">
    /// The globals type the script is compiled against, or — for a host with no globals
    /// indirection — its API class. Drives <em>both</em> the orientation index and
    /// <c>lookup_symbol</c>, so what the model is told exists and what it can then ask about
    /// cannot drift apart.
    /// </param>
    /// <param name="additionalTypes">
    /// Types the script works with that <paramref name="api"/> does not itself expose — typically
    /// the panel and component types its properties hand results out through.
    /// </param>
    /// <remarks>
    /// <para>
    /// The orientation blurb is the host's own prose followed by an index generated from
    /// <paramref name="api"/> by reflection, so the list of what exists cannot fall behind the
    /// code. The prose comes from <c>scriptchat.&lt;name&gt;.context.md</c> beside the
    /// executable if that file is deployed, falling back to a shared
    /// <c>scriptchat.context.md</c> — so a host with several scripts writes one file until a
    /// script actually needs its own.
    /// </para>
    /// <para>
    /// Symbol lookup resolves against a metadata-only compilation over the host's own assemblies,
    /// built lazily on the first lookup rather than here, so wiring the panel up costs nothing at
    /// startup. A host whose editor produces a real <see cref="Microsoft.CodeAnalysis.Compilation"/>
    /// should use the overload taking a session-options factory and supply
    /// <see cref="RoslynSymbolLookupProvider"/> over that instead — it reflects what the script
    /// can currently see, which a metadata compilation cannot.
    /// </para>
    /// <para>
    /// Order does not matter: calling this after the panel is already configured gives the new
    /// script a conversation straight away.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Any other argument is <see langword="null"/>.</exception>
    public void AddScript(
        string name,
        Func<string> read,
        Action<string> write,
        Type api,
        params Type[] additionalTypes)
    {
        // Checked here as well as in the overload below, so a blank name is reported against
        // this method's own parameter rather than surfacing from the orientation lookup.
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(additionalTypes);

        // Built once, not per conversation: the orientation and the reachable API are fixed for
        // the lifetime of the host, and rebuilding would discard the deferred compilation.
        var options = ScriptChatSessionOptions.ForHostApi(api, name, _loggerFactory, additionalTypes);

        AddScript(name, read, write, () => options);
    }

    /// <summary>
    /// Adds one script, with the host deciding for itself what each new conversation about it is
    /// given — its own symbol engine, or an orientation blurb built per conversation.
    /// </summary>
    /// <param name="name">The label shown on the target selector.</param>
    /// <param name="read">Reads the script currently in the host's editor.</param>
    /// <param name="write">Replaces the script, once the user has accepted an edit (D5).</param>
    /// <param name="createSessionOptions">
    /// Builds the options for a new conversation about this script. A factory rather than a fixed
    /// value so a host can capture something that is only true when the conversation starts — a
    /// snapshot of a counterpart script, say.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Any other argument is <see langword="null"/>.</exception>
    public void AddScript(
        string name,
        Func<string> read,
        Action<string> write,
        Func<ScriptChatSessionOptions> createSessionOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(createSessionOptions);

        var state = new TargetState(new ScriptChatTarget
        {
            DisplayName = name,
            ScriptTextProvider = read,
            ScriptTextSetter = write,
            CreateSessionOptions = createSessionOptions,
        });

        // A script added after the panel is already configured gets a conversation immediately,
        // so a host is free to call AddScript and UseStoredKey in either order.
        state.Session = CreateSession(state.Target);

        var wasEmpty = _targets.Length == 0;
        _targets = [.. _targets, state];

        RebuildSelector(selectedIndex: wasEmpty ? 0 : _targetSelector.SelectedIndex);
    }

    /// <summary>Repopulates the selector from <see cref="_targets"/> and re-renders.</summary>
    private void RebuildSelector(int selectedIndex)
    {
        _targetSelector.BeginUpdate();
        _targetSelector.Items.Clear();
        foreach (var state in _targets)
        {
            _targetSelector.Items.Add(state.Target.DisplayName);
        }

        _targetSelector.EndUpdate();

        // Driven explicitly rather than relying on SelectedIndexChanged, so the first render
        // does not depend on exactly when the combo box chooses to raise it.
        _targetSelector.SelectedIndex = _targets.Length == 0
            ? -1
            : Math.Clamp(selectedIndex, 0, _targets.Length - 1);

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
    /// Configures the panel from the stored key for the remembered provider, or switches it off
    /// with a pointer at the settings dialogue when there is no usable key.
    /// </summary>
    private void RestoreStoredConfiguration()
    {
        if (ApiKeyStore is null || _loadPreference is null)
        {
            return;
        }

        var preference = _loadPreference() ?? new ScriptChatProviderPreference(ScriptChatProvider.Claude, null);

        string? apiKey;
        try
        {
            apiKey = ApiKeyStore.Load(preference.Provider);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or System.Security.Cryptography.CryptographicException)
        {
            // A key file that exists but cannot be read is worth saying out loud: silently
            // prompting for a key the user believes they already entered is baffling.
            SetUnavailable("The stored API key could not be read. Choose Settings… to enter it again.");
            return;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            SetUnavailable("No API key yet. Choose Settings… to enter your own.");
            return;
        }

        Configure(new ScriptChatClientOptions
        {
            Provider = preference.Provider,
            ApiKey = apiKey,
            ModelId = string.IsNullOrWhiteSpace(preference.ModelId)
                ? ScriptChatModels.DefaultForProvider(preference.Provider)
                : preference.ModelId,
        });
    }

    /// <summary>Opens the settings dialogue this panel owns, creating it on first use.</summary>
    private void OnStoredKeySettingsRequested(object? sender, EventArgs e)
    {
        if (_settingsForm is null)
        {
            // LoggerFactory before KeyStore, so the store's own load is logged as well.
            _settingsForm = new ScriptChatSettingsForm
            {
                LoggerFactory = _loggerFactory,
                KeyStore = ApiKeyStore,
            };

            _settingsForm.ConfigurationApplied += OnStoredKeyConfigurationApplied;
        }

        _settingsForm.ShowDialog(this);
    }

    private void OnStoredKeyConfigurationApplied(object? sender, ScriptChatConfigurationEventArgs e)
    {
        // Provider and model only. The key stays in the store the settings panel wrote it to,
        // and never passes through the preference delegates (D3).
        _savePreference?.Invoke(
            new ScriptChatProviderPreference(e.ClientOptions.Provider, e.ClientOptions.ModelId));

        Configure(e.ClientOptions);
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
